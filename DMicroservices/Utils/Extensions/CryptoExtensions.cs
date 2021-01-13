using System;
using System.Security.Cryptography;
using System.Text;

namespace DMicroservices.Utils.Extensions
{
    public class CryptoExtensions
    {
        /// <summary>
        /// Verilen değerin MD5 özetini alır.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string Md5Encrypt(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                string val = string.Empty;
                MD5CryptoServiceProvider md5Cyripto = new MD5CryptoServiceProvider();
                byte[] bytes = Encoding.ASCII.GetBytes(value);
                byte[] arrays = md5Cyripto.ComputeHash(bytes);
                int capacity = (int)Math.Round(arrays.Length * 3 + (double)arrays.Length / 8);
                StringBuilder builder = new StringBuilder(capacity);
                int num = arrays.Length - 1;
                for (int i = 0; i <= num; i++)
                {
                    builder.Append(BitConverter.ToString(arrays, i, 1));
                }
                val = builder.ToString().TrimEnd(new char[] { ' ' });
                return val;
            }
            return null;
        }
    }
}
