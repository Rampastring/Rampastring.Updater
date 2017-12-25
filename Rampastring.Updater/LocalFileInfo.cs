using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    /// <summary>
    /// Represents a file on the local system.
    /// </summary>
    public class LocalFileInfo
    {
        public string FilePath { get; private set; }
        public byte[] Hash { get; private set; }
        public long Size { get; private set; }

        /// <summary>
        /// Parses a string array that represents a LocalFileInfo object
        /// and returns a new LocalFileInfo object based on the given string array.
        /// </summary>
        /// <param name="parts">The string array.</param>
        /// <returns>A LocalFileInfo object.</returns>
        public static LocalFileInfo Parse(string[] parts)
        {
            if (parts.Length != 3)
                throw new ParseException("The input string array has an invalid number of items.");

            LocalFileInfo fInfo = new LocalFileInfo();
            fInfo.FilePath = parts[0];
            fInfo.Hash = HashHelper.BytesFromHexString(parts[1]);
            fInfo.Size = long.Parse(parts[2]);

            return null;
        }

        /// <summary>
        /// Gets a string representation of this object in a format
        /// that can be parsed by the static <see cref="Parse"/> method.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public string GetString()
        {
            return String.Join(",",
                FilePath,
                HashHelper.BytesToString(Hash),
                Size.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Checks whether this file information matches an actual file on the
        /// file system.
        /// </summary>
        /// <param name="buildPath">The base path of the build.</param>
        public bool MatchesActualFile(string buildPath)
        {
            if (!File.Exists(buildPath + FilePath))
                return false;

            using (SHA1 sha1 = new SHA1CryptoServiceProvider())
            {
                using (Stream stream = File.OpenRead(buildPath + FilePath))
                {
                    byte[] hash = sha1.ComputeHash(stream);
                    return HashHelper.ByteArraysMatch(hash, this.Hash);
                }
            }
        }
    }
}
