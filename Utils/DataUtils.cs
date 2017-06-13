using System;
using System.Security.Cryptography;
using System.Text;

namespace Utils
{
    public static class DataUtils
    {
        public static string MD5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var result = md5.ComputeHash(Encoding.ASCII.GetBytes(input));
                return BitConverter.ToString(result);
            }
        }
    }
}
