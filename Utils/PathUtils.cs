using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utils
{
    public static class PathUtils
    {
        public static string DirectoryName(string directoryPath)
        {
            return new DirectoryInfo(directoryPath).Name;
        }
    }
}
