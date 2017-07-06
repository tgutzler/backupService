using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using ServerApi.Database;
using ServerApi.Options;
using Utils;

namespace ServerApi.Controllers
{
    public class FileController : Controller
    {
        private ApiOptions _apiOptions;
        private StorageOptions _storageOptions;
        private DatabaseService _dbService;

        public FileController(IOptions<ApiOptions> apiOptions, IOptions<StorageOptions> storageOptions)
        {
            _apiOptions = apiOptions.Value;
            _storageOptions = storageOptions.Value;
            _dbService = DatabaseService.Instance;
        }

        [HttpGet(WebApi.GetFile)]
        public string Get()
        {
            var count = _dbService.FileCount;
            if (count == 1)
                return $"There is {_dbService.FileCount} file backed up";
            else
                return $"There are {_dbService.FileCount} files backed up";
        }

        [HttpGet(WebApi.GetFile + "/{id}")]
        public IActionResult Get(int id)
        {
            try
            {
                return Json(_dbService.GetFile(id));
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        [HttpPost(WebApi.UpdateFile)]
        public IActionResult Post([FromBody] BackedUpFile file)
        {
            if (file == null)
            {
                return BadRequest();
            }

            try
            {
                _dbService.UpdateFile(file);
                return Json(file);
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        // 1. Disable the form value model binding here to take control of handling 
        //    potentially large files.
        // 2. Typically antiforgery tokens are sent in request body, but since we 
        //    do not want to read the request body early, the tokens are made to be 
        //    sent via headers. The antiforgery token filter first looks for tokens
        //    in the request header and then falls back to reading the body.
        [HttpPost(WebApi.UploadFile)]
        [DisableFormValueModelBinding]
//        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload()
        {
            if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
            {
                return BadRequest($"Expected a multipart request, but got {Request.ContentType}");
            }

            // Used to accumulate all the form url encoded key value pairs in the 
            // request.
            var formAccumulator = new KeyValueAccumulator();
            string tempFilePath = null;

            var boundary = MultipartRequestHelper.GetBoundary(
                MediaTypeHeaderValue.Parse(Request.ContentType),
                _apiOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);

            var section = await reader.ReadNextSectionAsync();
            while (section != null)
            {
                var hasContentDispositionHeader = ContentDispositionHeaderValue
                    .TryParse(section.ContentDisposition, out ContentDispositionHeaderValue contentDisposition);

                if (hasContentDispositionHeader)
                {
                    if (MultipartRequestHelper.HasFileContentDisposition(contentDisposition))
                    {
                        tempFilePath = Path.GetTempFileName();
                        using (var targetStream = System.IO.File.Create(tempFilePath))
                        {
                            await section.Body.CopyToAsync(targetStream);
                        }
                    }
                    else if (MultipartRequestHelper.HasFormDataContentDisposition(contentDisposition))
                    {
                        var key = HeaderUtilities.RemoveQuotes(contentDisposition.Name);
                        var encoding = GetEncoding(section);
                        using (var streamReader = new StreamReader(
                            section.Body,
                            encoding,
                            detectEncodingFromByteOrderMarks: true,
                            bufferSize: 1024,
                            leaveOpen: true))
                        {
                            // The value length limit is enforced by MultipartBodyLengthLimit
                            var value = await streamReader.ReadToEndAsync();
                            if (String.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
                            {
                                value = String.Empty;
                            }
                            formAccumulator.Append(key, value);

                            if (formAccumulator.ValueCount > _apiOptions.ValueCountLimit)
                            {
                                throw new InvalidDataException($"Form key count limit {_apiOptions.ValueCountLimit} exceeded.");
                            }
                        }
                    }
                }

                // Drains any remaining section body that has not been consumed and
                // reads the headers for the next section.
                section = await reader.ReadNextSectionAsync();
            }

            // Bind form data to a model
            var formData = formAccumulator.GetResults();
            BackedUpFile backedUpFile = null;
            if (formData.TryGetValue("backedUpFile", out var values))
            {
                var jsonString = values[0];
                backedUpFile = JsonConvert.DeserializeObject<BackedUpFile>(jsonString);
                if (backedUpFile.ParentId == 0)
                {
                    if (formData.TryGetValue("path", out var path))
                    {
                        path = Path.GetDirectoryName(path)
                            .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        var dir = _dbService.GetDirectory(path);
                        backedUpFile.ParentId = dir.Id;
                    }
                }
            }
            if (backedUpFile.ParentId == 0)
            {
                throw new Exception($"{backedUpFile.Name} has no parent");
            }
            var formValueProvider = new FormValueProvider(
                BindingSource.Form,
                new FormCollection(formAccumulator.GetResults()),
                CultureInfo.CurrentCulture);

            var bindingSuccessful = await TryUpdateModelAsync(backedUpFile, prefix: "",
                valueProvider: formValueProvider);
            if (!bindingSuccessful)
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
            }
            else
            {
                try
                {
                    FileHistory hist = null;
                    if (backedUpFile.Id > 0)
                    {
                        hist = _dbService.UpdateFile(backedUpFile);
                    }
                    else
                    {
                        hist = _dbService.AddFile(backedUpFile);
                    }
                    if (!Directory.Exists(_storageOptions.BackupRoot))
                    {
                        Directory.CreateDirectory(_storageOptions.BackupRoot);
                    }
                    var destPath = Path.Combine(_storageOptions.BackupRoot, $"{hist.Id}_{backedUpFile.Name}");
                    if (System.IO.File.Exists(destPath))
                    {
                        throw new IOException($"file '{destPath}' exists when it shouldn't");
                    }
                    System.IO.File.Move(tempFilePath, destPath);
                }
                catch (Exception ex)
                {
                    return BadRequest(ex);
                }
            }

            // filePath is where the file is
            return Json(backedUpFile);
        }

        [HttpPost(WebApi.DeleteFiles)]
        public IActionResult DeleteMany([FromBody] List<BackedUpFile> files)
        {
            if (files == null)
            {
                return BadRequest();
            }

            try
            {
                _dbService.DeleteFiles(files);
                return Json(true);
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        private static Encoding GetEncoding(MultipartSection section)
        {
            var hasMediaTypeHeader = MediaTypeHeaderValue.TryParse(
                section.ContentType, out MediaTypeHeaderValue mediaType);
            // UTF-7 is insecure and should not be honored. UTF-8 will succeed in 
            // most cases.
            if (!hasMediaTypeHeader || Encoding.UTF7.Equals(mediaType.Encoding))
            {
                return Encoding.UTF8;
            }
            return mediaType.Encoding;
        }
    }

    public static class MultipartRequestHelper
    {
        // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
        // The spec says 70 characters is a reasonable limit.
        public static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
        {
            var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary);
            if (string.IsNullOrWhiteSpace(boundary))
            {
                throw new InvalidDataException("Missing content-type boundary.");
            }

            if (boundary.Length > lengthLimit)
            {
                throw new InvalidDataException(
                    $"Multipart boundary length limit {lengthLimit} exceeded.");
            }

            return boundary;
        }

        public static bool IsMultipartContentType(string contentType)
        {
            return !string.IsNullOrEmpty(contentType)
                && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool HasFormDataContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="key";
            return contentDisposition != null
                && contentDisposition.DispositionType.Equals("form-data")
                && string.IsNullOrEmpty(contentDisposition.FileName)
                && string.IsNullOrEmpty(contentDisposition.FileNameStar);
        }

        public static bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
            return contentDisposition != null
                && contentDisposition.DispositionType.Equals("form-data")
                && (!string.IsNullOrEmpty(contentDisposition.FileName)
                    || !string.IsNullOrEmpty(contentDisposition.FileNameStar));
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var formValueProviderFactory = context.ValueProviderFactories
                .OfType<FormValueProviderFactory>()
                .FirstOrDefault();
            if (formValueProviderFactory != null)
            {
                context.ValueProviderFactories.Remove(formValueProviderFactory);
            }

            var jqueryFormValueProviderFactory = context.ValueProviderFactories
                .OfType<JQueryFormValueProviderFactory>()
                .FirstOrDefault();
            if (jqueryFormValueProviderFactory != null)
            {
                context.ValueProviderFactories.Remove(jqueryFormValueProviderFactory);
            }
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
        }
    }
}
