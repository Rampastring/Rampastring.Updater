using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater.BuildInfo
{
    /// <summary>
    /// A base class for build information.
    /// </summary>
    /// <typeparam name="T">A type that implements IFileInfo and has a default constructor.</typeparam>
    public abstract class BuildInfo<T> where T : IFileInfo, new()
    {
        private const string VERSION_SECTION = "Version";
        private const string FILES_SECTION = "Files";

        public BuildInfo()
        {
            FileInfos = new List<T>();
        }

        /// <summary>
        /// Gets information about the product version.
        /// </summary>
        public ProductVersionInfo ProductVersionInfo { get; set; }

        /// <summary>
        /// Gets the list of file information.
        /// </summary>
        public List<T> FileInfos { get; private set; }


        public void AddFileInfo(T fileInfo)
        {
            FileInfos.Add(fileInfo);
        }

        /// <summary>
        /// Parses build information from an INI file in the specified path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public void Parse(string filePath)
        {
            INIParse(new IniFile(filePath));
        }

        /// <summary>
        /// Parses build information from an INI file.
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
                    var fileInfo = new T();
                    fileInfo.Parse(parts);

                    FileInfos.Add(fileInfo);
                }
                catch (FormatException) { UpdaterLogger.Log("FormatException when parsing file information, INI key " + key); }
                catch (ParseException) { UpdaterLogger.Log("ParseException when parsing file information, INI key: " + key); }
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

            for (int i = 0; i < FileInfos.Count; i++)
            {
                filesSection.SetStringValue(i.ToString(), FileInfos[i].GetString());
            }

            iniFile.WriteIniFile();
        }
    }
}
