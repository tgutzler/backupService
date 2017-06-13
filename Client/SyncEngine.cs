using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using ServerApi.Database;
using Utils;

namespace Client
{
    public static class WebApi
    {
        public static readonly string GetDirectory = "getdir";
        public static readonly string Ping = "ping";
        public static readonly string Upload = "upload";
    }

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
                var reply = await _client.GetStringAsync($"{_serverUri}/{WebApi.Ping}").ConfigureAwait(false);
                success = reply == "Pong";
            }
            catch (Exception) { }

            return success;
        }

        public async Task SynchroniseAsync(string directoryPath)
        {
            var backedupDirectory = await GetDirectoryInfo(directoryPath).ConfigureAwait(false);
            var lastWrite = Directory.GetLastWriteTimeUtc(directoryPath);
            var update = false;
            if (backedupDirectory == null)
            {
                update = true;
                var files = Directory.GetFiles(directoryPath);
                var backedUpFiles = new List<BackedUpFile>(files.Length);
                backedupDirectory = new BackedUpDirectory();
                foreach (var file in files)
                {
                    // Upload()
                    var s = await Upload($"{_serverUri}/{WebApi.Upload}", file).ConfigureAwait(false);

                    backedUpFiles.Add(new BackedUpFile()
                    {
                        Name = Path.GetFileName(file),
                        Modified = Directory.GetLastWriteTimeUtc(file),
                        ParentId = backedupDirectory.Id
                    });
                }
                backedupDirectory.Modified = lastWrite;
                backedupDirectory.Name = Path.GetDirectoryName(directoryPath);
                backedupDirectory.Files = backedUpFiles;
            }
            if (lastWrite > backedupDirectory.Modified)
            {
                update = true;
            }
            if (update)
            {

            }
        }

        private async Task<BackedUpDirectory> GetDirectoryInfo(string directory)
        {
            //_client.DefaultRequestHeaders.Accept.Clear();
            //_client.DefaultRequestHeaders.Accept.Add(
            //    new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            //_client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

            var md5 = DataUtils.MD5Hash(directory);
            var streamTask = _client.GetStreamAsync($"{_serverUri}/{WebApi.GetDirectory}/{md5}");
            BackedUpDirectory backedUpDirectory = null;
            try
            {
                var stream = await streamTask.ConfigureAwait(false);
                backedUpDirectory = directorySerializer.ReadObject(stream)
                as BackedUpDirectory;

            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }

            return backedUpDirectory;
        }

        private async Task<string> Upload(string actionUrl, string filePath)
        {
            using (var client = new HttpClient())
            using (var formData = new MultipartFormDataContent())
            using (var fileStream = File.OpenRead(filePath))
            {
                formData.Add(new StringContent(filePath), "path");
                formData.Add(new StreamContent(fileStream), "file");
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
    }
}
