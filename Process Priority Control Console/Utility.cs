using System.Security.Cryptography;
using System.Text;

namespace ProcessPriorityControl.Cmd
{
    static class Utility
    {
        /// <summary>
        /// Hash prefix to make the hash more unique.
        /// </summary>
        private static readonly string HashPrefix = "gcifQ;B*bP|*yMWgyv=6RQ7Oa~WC;S#+cuYlbTc5Zw~e\"3I9vHn5Kv:f+!;sJks6";

        /// <summary>
        /// Get the MD5 hash of a string.
        /// </summary>
        /// <param name="input">String to hash</param>
        /// <returns>Hash value as hexadecimal</returns>
        /// <seealso cref="https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.md5"/>
        public static string GetMd5Hash(string input)
        {
            MD5 md5Hash = MD5.Create();

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data  and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        /// <summary>
        /// Get the MD5 hash of a string with the hash prefix prepended.
        /// </summary>
        /// <param name="input">String to hash</param>
        /// <returns>Hash value as hexadecimal</returns>
        public static string GetMd5HashPrefixed(string input)
        {
            return GetMd5Hash(HashPrefix + input);
        }
    }
}
