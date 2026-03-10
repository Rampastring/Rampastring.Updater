using Rampastring.Updater;
using Rampastring.Updater.BuildInfo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VersionWriter
{
    /// <summary>
    /// Manages information on the generated build version and the build's files.
    /// </summary>
    class VersionConfig
    {
        public const string VERSIONCONFIG_INI = "VersionConfig.ini";
        private const string VERSION_SECTION = "Version";

        /// <summary>
        /// The internal version number of the product build.
        /// </summary>
        public int InternalVersion { get; set; }

        /// <summary>
        /// The version string of the product build for the UI.
        /// </summary>
        public string DisplayedVersion { get; set; }

        /// <summary>
        /// The directory where the build's finalized files will be stored,
        /// relative to Environment.CurrentDirectory.
        /// </summary>
        public string BuildDirectory { get; set; }

        public List<Component<VersionWriterFileInfo>> Components = new List<Component<VersionWriterFileInfo>>();

        public List<string> IgnoredFiles = new List<string>();

        public void Parse()
        {
            if (!File.Exists(GetVersionConfigIniPath()))
                throw new FileNotFoundException(VERSIONCONFIG_INI + " not found!");

            var versionConfigIni = new IniFile(GetVersionConfigIniPath());
            
            InternalVersion = versionConfigIni.GetIntValue(VERSION_SECTION, "InternalVersion", 0);
            DisplayedVersion = versionConfigIni.GetStringValue(VERSION_SECTION, "DisplayString", InternalVersion.ToString());
            BuildDirectory = versionConfigIni.GetStringValue(VERSION_SECTION, "BuildDirectory", string.Empty);

            if (string.IsNullOrEmpty(BuildDirectory))
                throw new InvalidDataException("No BuildDirectory specified in " + VERSIONCONFIG_INI + "!");

            IniSection componentsSection = versionConfigIni.GetSection("Components");

            if (componentsSection == null)
            {
                throw new InvalidDataException("No Components section found in " + VERSIONCONFIG_INI + "!");
            }

            foreach (var kvp in componentsSection.Keys)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value))
                    throw new InvalidDataException("Component name cannot be empty!");

                var component = new Component<VersionWriterFileInfo>();

                var componentIniSection = versionConfigIni.GetSection(kvp.Value);

                if (componentIniSection == null)
                    throw new InvalidDataException("No INI section found for component " + kvp.Value + "!");

                component.ReadOptionsFromIniSection(componentIniSection);

                Components.Add(component);

                IniSection filesSection = versionConfigIni.GetSection(component.InternalName + ".Files");

                if (filesSection != null)
                {
                    List<string> keys = filesSection.GetKeys();

                    foreach (string key in keys)
                    {
                        string entryInfo = filesSection.GetStringValue(key, string.Empty);
                        string[] parts = entryInfo.Split(',');

                        VersionWriterFileInfo fileInfo = new VersionWriterFileInfo();
                        fileInfo.Parse(parts);
                        component.AddFileInfo(fileInfo);
                    }
                }
            }

            IniSection ignoredFilesSection = versionConfigIni.GetSection("Ignore");
            if (ignoredFilesSection != null)
            {
                List<string> keys = ignoredFilesSection.GetKeys();

                foreach (string key in keys)
                {
                    IgnoredFiles.Add(ignoredFilesSection.GetStringValue(key, string.Empty));
                }
            }
        }

        public void GenerateVersionDisplayStringFromCurrentDate()
        {
            var dtn = DateTime.Now;
            DisplayedVersion = dtn.ToString("ddMMmmss");
        }

        public void Write()
        {
            IniFile versionConfigIni = new IniFile();
            versionConfigIni.SetIntValue(VERSION_SECTION, "InternalVersion", InternalVersion);
            versionConfigIni.SetStringValue(VERSION_SECTION, "DisplayedVersion", DisplayedVersion);
            versionConfigIni.SetStringValue(VERSION_SECTION, "BuildDirectory", BuildDirectory);

            for (int i = 0; i < IgnoredFiles.Count; i++)
            {
                versionConfigIni.SetStringValue("Ignore", i.ToString(CultureInfo.InvariantCulture), IgnoredFiles[i]);
            }

            for (int i = 0; i < Components.Count; i++)
            {
                var component = Components[i];

                versionConfigIni.SetStringValue("Components", i.ToString(CultureInfo.InvariantCulture), component.InternalName);

                var componentOptionsSection = new IniSection(component.InternalName);
                component.WriteOptionsToIniSection(componentOptionsSection);
                versionConfigIni.AddSection(componentOptionsSection);

                var componentFilesSection = new IniSection(component.InternalName + ".Files");
                component.WriteFilesToIniSection(componentFilesSection);
                versionConfigIni.AddSection(componentFilesSection);
            }

            versionConfigIni.WriteIniFile(GetVersionConfigIniPath());

            string data = File.ReadAllText(GetVersionConfigIniPath());

            // Add comments to the beginning of the file
            using (StreamWriter sw = new StreamWriter(File.OpenWrite(GetVersionConfigIniPath())))
            {
                sw.WriteLine("; Generated by VersionWriter of Rampastring.Updater");
                sw.WriteLine("; All comments have been truncated and will be truncated when the tool is run.");
                sw.WriteLine();
                sw.Write(data);
            }
        }

        public static string GetVersionConfigIniPath()
        {
            return Environment.CurrentDirectory + Path.DirectorySeparatorChar + VERSIONCONFIG_INI;
        }

        /// <summary>
        /// Gathers a list of files that do not exist or are outdated in the
        /// build directory.
        /// </summary>
        public List<VersionWriterFileInfo> GetOutdatedFileList()
        {
            string targetDirectory = Environment.CurrentDirectory + 
                Path.DirectorySeparatorChar + BuildDirectory;

            // If the build directory doesn't exist, we need to process all the files
            if (!Directory.Exists(targetDirectory))
                return new List<VersionWriterFileInfo>();

            if (targetDirectory[targetDirectory.Length - 1] != Path.DirectorySeparatorChar)
                targetDirectory = targetDirectory + Path.DirectorySeparatorChar;

            RemoteBuildInfo remoteBuildInfo = new RemoteBuildInfo();

            if (File.Exists(targetDirectory + BuildHandler.REMOTE_BUILD_INFO_FILE))
                remoteBuildInfo.Parse(targetDirectory + BuildHandler.REMOTE_BUILD_INFO_FILE);

            var outdatedList = new List<VersionWriterFileInfo>();

            var allFiles = new List<VersionWriterFileInfo>();
            Components.ForEach(c => allFiles.AddRange(c.FileInfos));

            foreach (VersionWriterFileInfo file in allFiles)
            {
                string filePath = targetDirectory + file.FilePath;
                string originalFilePath = Environment.CurrentDirectory + Path.DirectorySeparatorChar + file.FilePath;

                if (file.Compressed)
                {
                    // Compressed files have an additional file extension
                    filePath = filePath + RemoteFileInfo.COMPRESSED_FILE_EXTENSION;
                }

                if (!File.Exists(filePath))
                {
                    // If the file doesn't exist in the build, we obviously need
                    // to process it
                    outdatedList.Add(file);
                    continue;
                }

                if (file.Compressed)
                {
                    // If the file is compressed, check if the uncompressed hash 
                    // in RemoteVersion matches the hash of the original (uncompressed) file
                    // If not (or there is no record of the file in RemoteVersion), we need
                    // to process the file

                    RemoteFileInfo existingFileInfo = remoteBuildInfo.GetFilesForAllComponents().Find(
                        f => f.FilePath == file.FilePath);

                    if (existingFileInfo == null || 
                        !HashHelper.ByteArraysMatch(existingFileInfo.UncompressedHash, 
                        HashHelper.ComputeHashForFile(originalFilePath)))
                    {
                        outdatedList.Add(file);
                    }
                }
                else
                {
                    // For uncompressed files we can just compare its hash
                    // to the original file's hash directly

                    if (!HashHelper.ByteArraysMatch(HashHelper.ComputeHashForFile(filePath),
                        HashHelper.ComputeHashForFile(originalFilePath)))
                    {
                        outdatedList.Add(file);
                    }
                }
            }

            return outdatedList;
        }

        /// <summary>
        /// Cleans the build directory from files that don't exist in the list
        /// of files for the build.
        /// </summary>
        public void CleanBuildDirectory()
        {
            char dsc = Path.DirectorySeparatorChar;
            string buildDirectory = Environment.CurrentDirectory + dsc + BuildDirectory;
            string[] files = Directory.GetFiles(buildDirectory, "*", SearchOption.AllDirectories);

            RemoteBuildInfo remoteBuildInfo = new RemoteBuildInfo();
            var remoteFiles = remoteBuildInfo.GetFilesForAllComponents();

            if (File.Exists(buildDirectory + dsc + BuildHandler.REMOTE_BUILD_INFO_FILE))
                remoteBuildInfo.Parse(buildDirectory + dsc + BuildHandler.REMOTE_BUILD_INFO_FILE);

            foreach (string path in files)
            {
                string relativePath = path.Substring(buildDirectory.Length + 1);

                if (relativePath == BuildHandler.LOCAL_BUILD_INFO_FILE ||
                    relativePath == BuildHandler.REMOTE_BUILD_INFO_FILE)
                {
                    continue;
                }

                if (!remoteFiles.Exists(f => f.GetFilePathWithCompression().Replace('\\', '/') ==
                                                           relativePath.Replace('\\', '/')))
                {
                    Console.WriteLine("Deleting " + relativePath);
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Writes new local and remote version files into the build directory.
        /// </summary>
        public void WriteVersionFiles()
        {
            var localBuildInfo = new LocalBuildInfo();
            localBuildInfo.ProductVersionInfo = new ProductVersionInfo(InternalVersion, DisplayedVersion);

            var remoteBuildInfo = new RemoteBuildInfo();
            remoteBuildInfo.ProductVersionInfo = new ProductVersionInfo(InternalVersion, DisplayedVersion);

            string buildPath = Environment.CurrentDirectory +
                Path.DirectorySeparatorChar + BuildDirectory + Path.DirectorySeparatorChar;

            foreach (var component in Components)
            {
                var remoteComponent = new Component<RemoteFileInfo>(component.InternalName, component.UIName, component.Optional);
                remoteBuildInfo.Components.Add(remoteComponent);

                var localComponent = new Component<LocalFileInfo>(component.InternalName, component.UIName, component.Optional);
                localBuildInfo.Components.Add(localComponent);

                foreach (VersionWriterFileInfo file in component.FileInfos)
                {
                    string originalFilePath = Environment.CurrentDirectory +
                        Path.DirectorySeparatorChar + file.FilePath;

                    byte[] hash = HashHelper.ComputeHashForFile(originalFilePath);
                    long size = new FileInfo(originalFilePath).Length;
                    localComponent.AddFileInfo(new LocalFileInfo(file.FilePath, hash, size));

                    RemoteFileInfo remoteFileInfo;

                    if (file.Compressed)
                    {
                        string compressedFilePath = buildPath + file.FilePath + RemoteFileInfo.COMPRESSED_FILE_EXTENSION;
                        remoteFileInfo = new RemoteFileInfo(file.FilePath, hash, size, true,
                            HashHelper.ComputeHashForFile(compressedFilePath),
                            new FileInfo(compressedFilePath).Length);
                    }
                    else
                    {
                        remoteFileInfo = new RemoteFileInfo(file.FilePath, hash, size, false);
                    }

                    remoteComponent.AddFileInfo(remoteFileInfo);
                }
            }

            localBuildInfo.Write(buildPath + BuildHandler.LOCAL_BUILD_INFO_FILE);
            remoteBuildInfo.Write(buildPath + BuildHandler.REMOTE_BUILD_INFO_FILE);
        }
    }
}
