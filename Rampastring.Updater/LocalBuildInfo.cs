using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    /// <summary>
    /// Represents a local product build. Includes product version information 
    /// and file information.
    /// </summary>
    public class LocalBuildInfo
    {
        private const string VERSION_SECTION = "Version";
        private const string FILES_SECTION = "Files";

        public LocalBuildInfo()
        {
            BuildPath = Environment.CurrentDirectory + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Gets information about the product version.
        /// </summary>
        public ProductVersionInfo ProductVersionInfo { get; private set; }

        /// <summary>
        /// Gets or sets the base directory path of the build.
        /// </summary>
        public string BuildPath { get; set; }

        private List<LocalFileInfo> localFileInfos = new List<LocalFileInfo>();



        /// <summary>
        /// Parses local build information from an INI file in the specified path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public void Parse(string filePath)
        {
            INIParse(new IniFile(filePath));
        }

        /// <summary>
        /// Parses local build information from an INI file.
        /// </summary>
        /// <param name="iniFile">The INI file.</param>
        private void INIParse(IniFile iniFile)
        {
            var versionSection = iniFile.GetSection(VERSION_SECTION);

            if (versionSection == null)
                throw new ParseException("[" + VERSION_SECTION + "] section not found from " + iniFile.FileName);

            ProductVersionInfo = new ProductVersionInfo();
            ProductVersionInfo.Parse(versionSection);

            var fileKeys = iniFile.GetSectionKeys(FILES_SECTION);

            if (fileKeys == null)
                return;

            foreach (string key in fileKeys)
            {
                string[] parts = iniFile.GetStringValue(FILES_SECTION, key, string.Empty).Split(',');

                try
                {
                    var localFileInfo = LocalFileInfo.Parse(parts);

                    localFileInfos.Add(localFileInfo);
                }
                catch (FormatException) { UpdaterLogger.Log("FormatException when parsing local file information, INI key " + key); }
                catch (ParseException) { UpdaterLogger.Log("ParseException when parsing local file information, INI key: " + key); }
            }
        }

        /// <summary>
        /// Writes build information into the specified file path.
        /// Erases the file first if it already exists.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public void Write(string filePath)
        {
            File.Delete(filePath);

            var iniFile = new IniFile(filePath);

            var versionSection = new IniSection(VERSION_SECTION);
            iniFile.AddSection(versionSection);
            ProductVersionInfo.Write(versionSection);

            var filesSection = new IniSection(FILES_SECTION);
            iniFile.AddSection(filesSection);

            for (int i = 0; i < localFileInfos.Count; i++)
            {
                filesSection.SetStringValue(i.ToString(), localFileInfos[i].GetString());
            }
        }

        /// <summary>
        /// Verifies the files of the local build. Returns a list of files that
        /// have different hashes from specified.
        /// </summary>
        /// <returns>A list of files that have different hashes from specified.</returns>
        public List<LocalFileInfo> Verify()
        {
            var differentFiles = new List<LocalFileInfo>();

            localFileInfos.ForEach(fi => 
            {
                if (!fi.MatchesActualFile(BuildPath)) differentFiles.Add(fi);
            });

            return differentFiles;
        }
    }
}
