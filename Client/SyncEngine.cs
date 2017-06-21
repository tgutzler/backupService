using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ServerApi.Database;
using ServerApi.Interfaces;
using Utils;

namespace Client
{
    internal class SyncEngine
    {
        private HttpClient _client = new HttpClient();
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
//                var reply = await _client.GetStringAsync($"{_serverUri}/{WebApi.Ping}").ConfigureAwait(false);
                var reply = await _client.GetAsync($"{_serverUri}/{WebApi.Ping}").ConfigureAwait(false);
                success = reply.IsSuccessStatusCode;
            }
            catch (Exception) { }

            return success;
        }

        public async Task<bool> SynchroniseAsync(string directoryPath, int? parentId = null)
        {
            Console.WriteLine($"Press any key to start {directoryPath}");
            Console.ReadKey();
            var backedupDirectory = await GetDirectoryInfo(directoryPath, parentId).ConfigureAwait(false);
            if (backedupDirectory == null) return false;

            var lastWrite = Directory.GetLastWriteTimeUtc(directoryPath);

            var files = Directory.GetFiles(directoryPath);
            foreach (var file in files)
            {
                var backedUpFile = new BackedUpFile()
                {
                    Name = Path.GetFileName(file),
                    Modified = Directory.GetLastWriteTimeUtc(file),
                    ParentId = backedupDirectory.Id
                };
                // Upload file and object, link object to parent
                var result = await Upload($"{_serverUri}/{WebApi.Upload}", file, backedUpFile)
                    .ConfigureAwait(false);
                backedUpFile = JsonConvert.DeserializeObject<BackedUpFile>(result);
                if (backedUpFile == null) return false;
            }
            foreach (var dir in Directory.GetDirectories(directoryPath))
            {
                await SynchroniseAsync(dir, backedupDirectory.Id).ConfigureAwait(false);
            }
            backedupDirectory.Modified = lastWrite;

            // update dir on server
            //if (lastWrite > backedupDirectory.Modified)
            //{
            //    update = true;
            //}
            //if (update)
            //{

            //}
            return true;
        }

        private async Task<BackedUpDirectory> GetDirectoryInfo(string directory, int? parentId = null)
        {
            //_client.DefaultRequestHeaders.Accept.Clear();
            //_client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var di = parentId == null ?
                new BUDirectoryInfo(path: directory) :
                new BUDirectoryInfo(parentId, Path.GetDirectoryName(directory));
            return await GetFromPost($"{_serverUri}/{WebApi.GetDirectory}", JsonConvert.SerializeObject(di))
                .ConfigureAwait(false) as BackedUpDirectory;
        }

        private async Task<string> Upload(string actionUrl, string filePath, BackedUpFile backedUpFile)
        {
            using (var client = new HttpClient())
            using (var formData = new MultipartFormDataContent())
            using (var fileStream = File.OpenRead(filePath))
            {
                client.Timeout = new TimeSpan(0, 10, 0);
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
            try
            {
                var httpContent = new StringContent(stringContent, Encoding.UTF8, "application/json");
                var result = await _client.PostAsync(requestUri, httpContent).ConfigureAwait(false);
                var stringResult = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var dir = JsonConvert.DeserializeObject(stringResult, typeof(BackedUpDirectory));
                return dir;
            }
            catch (Exception ex)
            {
                //TODO: tracesource
                return null;
            }
        }
    }
}
