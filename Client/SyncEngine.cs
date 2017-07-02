using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ServerApi.Database;
using ServerApi.Interfaces;
using Utils;
using static Utils.PathUtils;

namespace Client
{
    internal class SyncEngine
    {
        private readonly TimeSpan httpClientTimeout = new TimeSpan(0, 10, 0);
        private DataContractJsonSerializer directorySerializer = new DataContractJsonSerializer(typeof(BackedUpDirectory));
        private DataContractJsonSerializer fileSerializer = new DataContractJsonSerializer(typeof(BackedUpFile));
        private string _serverUri;
        private string _user;

        public SyncEngine(string serverUri, string user)
        {
            _serverUri = serverUri;
            if (_serverUri[_serverUri.Length - 1] == '/')
            {
                _serverUri = _serverUri.Remove(_serverUri.Length - 1);
            }
            _user = user;
        }

        public async Task<bool> PingAsync()
        {
            bool success = false;
            try
            {
                using (var client = new HttpClient())
                {
                    var reply = await client.GetAsync($"{_serverUri}/{WebApi.Ping}").ConfigureAwait(false);
                    success = reply.IsSuccessStatusCode;
                }
            }
            catch (Exception) { }

            return success;
        }

        public async Task<bool> SynchroniseAsync(string directoryPath, int? parentId = null)
        {
            var directoryName = DirectoryName(directoryPath);
            var lastWrite = Directory.GetLastWriteTimeUtc(directoryPath);
            Console.WriteLine($"Press any key to start {directoryName} ({directoryPath})");
            Console.ReadKey();
            var backedupDirectory = await GetDirectoryInfo(directoryPath, parentId).ConfigureAwait(false) ??
                await AddDirectory(directoryName, parentId).ConfigureAwait(false);
            var dirWasModified = backedupDirectory.Modified != lastWrite;
            if (dirWasModified)
            {
                backedupDirectory = await GetDirectoryInfo(directoryPath, parentId, true).ConfigureAwait(false);
                var filesToBackup = Directory.GetFiles(directoryPath);

                await CheckForDeletedFiles(backedupDirectory, filesToBackup).ConfigureAwait(false);
                foreach (var file in filesToBackup)
                {
                    await UpdateFile(backedupDirectory, file).ConfigureAwait(false);
                }
            }
            foreach (var dir in Directory.GetDirectories(directoryPath))
            {
                await SynchroniseAsync(dir, backedupDirectory.Id)
                    .ConfigureAwait(false);
            }
            if (dirWasModified)
            {
                backedupDirectory.Modified = lastWrite;
                await UpdateDirectory(backedupDirectory).ConfigureAwait(false);
            }

            return true;
        }

        private async Task<BackedUpFile> UpdateFile(BackedUpDirectory directory, string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var backedUpFile = directory.Files?.FirstOrDefault(f => f.Name == fileName);
            if (backedUpFile == null)
            {
                backedUpFile = new BackedUpFile()
                {
                    Name = fileName,
                    Modified = Directory.GetLastWriteTimeUtc(filePath),
                    ParentId = directory.Id
                };
                // Upload file and object, link object to parent
                var result = await Upload($"{_serverUri}/{WebApi.UploadFile}", filePath, backedUpFile)
                    .ConfigureAwait(false);
                backedUpFile = JsonConvert.DeserializeObject<BackedUpFile>(result);
            }
            else
            {
                var lastWrite = Directory.GetLastWriteTimeUtc(filePath);
                if (lastWrite > backedUpFile.Modified)
                {
                    backedUpFile.Modified = lastWrite;
                    var result = await Upload($"{_serverUri}/{WebApi.UploadFile}", filePath, backedUpFile)
                        .ConfigureAwait(false);
                    backedUpFile = JsonConvert.DeserializeObject<BackedUpFile>(result);
                }
            }
            return backedUpFile;
        }

        private async Task CheckForDeletedFiles(BackedUpDirectory directory, string[] existingFiles)
        {
            List<string> existingFileNames = new List<string>(existingFiles.Length);
            foreach (var filePath in existingFiles)
            {
                existingFileNames.Add(Path.GetFileName(filePath));
            }
            var filesToDelete = new List<BackedUpFile>();
            foreach (var backedUpFile in directory.Files)
            {
                if (!existingFileNames.Contains(backedUpFile.Name))
                {
                    filesToDelete.Add(backedUpFile);
                }
            }
            if (filesToDelete.Count > 0)
            {
                var result = await GetFromPost($"{_serverUri}/{WebApi.DeleteFiles}", JsonConvert.SerializeObject(filesToDelete))
                    .ConfigureAwait(false);
            }
        }

        private async Task<BackedUpDirectory> GetDirectoryInfo(string directory, int? parentId = null, bool includeFiles = false)
        {
            var di = parentId == null ?
                new BUDirectoryInfo(path: directory) :
                new BUDirectoryInfo(parentId, DirectoryName(directory));
            return await GetFromPost($"{_serverUri}/"
                + (includeFiles ? WebApi.GetDirectoryWithFiles : WebApi.GetDirectory),
                JsonConvert.SerializeObject(di)).ConfigureAwait(false) as BackedUpDirectory;
        }

        private async Task<BackedUpDirectory> AddDirectory(string directoryName, int? parentId = null)
        {
            var dir = new BackedUpDirectory()
            {
                Name = directoryName,
                ParentId = parentId
            };
            dir = await GetFromPost($"{_serverUri}/{WebApi.AddDirectory}", JsonConvert.SerializeObject(dir))
                .ConfigureAwait(false) as BackedUpDirectory;
            return dir;
        }

        private async Task<BackedUpDirectory> UpdateDirectory(BackedUpDirectory directory)
        {
            directory = await GetFromPost($"{_serverUri}/{WebApi.UpdateDirectory}", JsonConvert.SerializeObject(directory))
                .ConfigureAwait(false) as BackedUpDirectory;
            return directory;
        }

        private async Task<string> Upload(string actionUrl, string filePath, BackedUpFile backedUpFile)
        {
            using (var client = new HttpClient() { Timeout = httpClientTimeout })
            using (var formData = new MultipartFormDataContent())
            using (var fileStream = File.OpenRead(filePath))
            {
                formData.Add(new StringContent(JsonConvert.SerializeObject(backedUpFile)), "backedUpFile");
                formData.Add(new StringContent(filePath), "path");
                //var streamContent = new StreamContent(fileStream);
                //streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("file")
                //{
                //    FileName = backedUpFile.Name
                //};
                formData.Add(new StreamContent(fileStream), "file", backedUpFile.Name);
                using (var response = await client.PostAsync(actionUrl, formData))
                {
                    var input = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        private async Task<object> GetFromPost(string requestUri, string stringContent)
        {
            using (var client = new HttpClient() { Timeout = httpClientTimeout })
            {
                var httpContent = new StringContent(stringContent, Encoding.UTF8, "application/json");
                var result = await client.PostAsync(requestUri, httpContent).ConfigureAwait(false);
                if (!result.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Invalid request to {requestUri}. Content: {stringContent}");
                }
                var stringResult = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var dir = JsonConvert.DeserializeObject(stringResult, typeof(BackedUpDirectory));
                return dir;
            }
        }
    }
}
