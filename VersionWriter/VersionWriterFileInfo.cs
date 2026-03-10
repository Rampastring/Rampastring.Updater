using Rampastring.Updater;
using Rampastring.Updater.BuildInfo;
using System;

namespace VersionWriter
{
    public class VersionWriterFileInfo : IFileInfo
    {
        public VersionWriterFileInfo()
        {
        }

        public VersionWriterFileInfo(string filePath, bool compressed)
        {
            FilePath = filePath;
            Compressed = compressed;
        }

        public string FilePath { get; private set; }

        public bool Compressed { get; private set; }

        public string GetString()
        {
            return FilePath.Replace('\\', '/') + "," + (Convert.ToInt32(Compressed)).ToString();
        }

        public void Parse(string[] parts)
        {
            if (parts.Length != 2)
                throw new ParseException("Failed to parse file entry. Info: " + string.Join(',', parts));

            FilePath = parts[0];
            Compressed = Convert.ToBoolean(int.Parse(parts[1]));
        }
    }
}
