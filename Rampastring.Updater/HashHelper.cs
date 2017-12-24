using System;
using System.Globalization;
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
    }
}
