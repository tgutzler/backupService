using System;
using System.Collections.Generic;
using System.Text;

namespace Utils
{
    public static class WebApi
    {
        public const string Ping = "api/Utils/ping";

        public const string AddDirectory = "api/Directory/add";
        public const string GetDirectory = "api/Directory";
        public const string GetDirectoryWithFiles = "api/Directory/withFiles";
        public const string UpdateDirectory = "api/Directory/update";

        public const string GetFile = "api/File";
        public const string UploadFile = "api/File/upload";
        public const string UpdateFile = "api/File/update";
        public const string DeleteFiles = "api/File/deleteMany";
    }
}
