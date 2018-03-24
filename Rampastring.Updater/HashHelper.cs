using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Rampastring.Updater
{
    /// <summary>
    /// Provides static functions that can help with parsing hashes.
    /// </summary>
    static class HashHelper
    {
        /// <summary>
        /// Generates a hex-formatted string from an array of bytes.
        /// </summary>
        /// <param name="bytes">The array of bytes.</param>
        /// <returns>A hex-formatted string that represents the array of bytes.</returns>
        public static string BytesToString(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Generates an array of bytes from a hex-formatted string representation of bytes.
        /// </summary>
        /// <param name="hexString">A hex-formatted string representation of bytes.</param>
        /// <returns>A byte array.</returns>
        public static byte[] BytesFromHexString(string hexString)
        {
            if (hexString.Length % 2 != 0)
                throw new ArgumentException("hexString needs to have an even number of characters.");

            byte[] bytes = new byte[hexString.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                string substring = hexString.Substring(i * 2, 2);
                bytes[i] = byte.Parse(substring, NumberStyles.HexNumber);
            }

            return bytes;
        }

        /// <summary>
        /// Checks whether two byte arrays have identical contents.
        /// </summary>
        /// <param name="array1">The first byte array.</param>
        /// <param name="array2">The second byte array.</param>
        /// <returns>True if the arrays match, otherwise false.</returns>
        public static bool ByteArraysMatch(byte[] array1, byte[] array2)
        {
            // There are faster ways of doing this, see
            // https://stackoverflow.com/questions/43289/comparing-two-byte-arrays-in-net/8808245#8808245
            // - but this should be fast enough for our use case

            if (array1 == null)
            {
                return array2 == null;
            }
            else if (array2 == null)
                return false;

            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Computes a SHA1 hash for a file.
        /// Returns null if the file does not exist.
        /// </summary>
        /// <param name="filePath">The path of the file.</param>
        /// <returns>A byte array, or null.</returns>
        public static byte[] ComputeHashForFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            using (SHA1 sha1 = new SHA1CryptoServiceProvider())
            {
                using (Stream stream = File.OpenRead(filePath))
                {
                    return sha1.ComputeHash(stream);
                }
            }
        }

        /// <summary>
        /// Checks whether the SHA1 hash of a specific file matches a given hash.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="hash">The hash that the file's hash will be compared against.</param>
        public static bool FileHashMatches(string filePath, byte[] hash)
        {
            return ByteArraysMatch(hash, ComputeHashForFile(filePath));
        }
    }
}
