using Rampastring.Updater;
using Rampastring.Updater.BuildInfo;
using Rampastring.Updater.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VersionWriter
{
    class Program
    {
        private static bool incrementVersion = true;
        private static bool generateVersionFromDate = false;

        private const string EXE_NAME = "VersionWriter";
        private const string BUILD_DIRECTORY = "Updates";

        public static void Main(string[] args)
        {
            Console.WriteLine("VersionWriter for Rampastring.Updater");
            Console.WriteLine();

            foreach (string arg in args)
            {
                switch (arg.ToUpperInvariant())
                {
                    case "-VERSIONFROMDATE":
                        Console.WriteLine("Command-line argument: generate new version " +
                            "display string based on the current system date and time.");
                        generateVersionFromDate = true;
                        break;
                    case "-NOINCREMENT":
                        Console.WriteLine("Command-line argument: don't increment " +
                            "the internal version.");
                        incrementVersion = false;
                        break;
                    case "-GENERATECONFIG":
                        Console.WriteLine("Command-line argument: generate new version " +  
                            "configuration file including files from the current directory.");
                        Console.WriteLine();
                        GenerateVersionConfigFile();
                        // Exit the program instead of continuing
                        return;
                    case "-UPDATECONFIG":
                        Console.WriteLine("Command-line argument: update version " +
                            "configuration file, updating it to include new files missing from the version configuration " + 
                            "and removing deleted files from the versionn configuration.");
                        Console.WriteLine();
                        UpdateVersionConfigFile();
                        // Exit the program instead of continuing
                        return;
                    case "-PURGE":
                        Console.WriteLine("Command-line argument: purge non-existent files from version configuration.");
                        Console.WriteLine();
                        PurgeFileList();
                        // Exit the program instead of continuing
                        return;
                    case "-HELP":
                    case "-?":
                    case "?":
                    case "HELP":
                        Console.WriteLine("Default behaviour (no command-line arg): increment version and generate new build files");
                        Console.WriteLine("Possible arguments:");
                        Console.WriteLine("-VERSIONFROMDATE: Generate new version display string based on current system date and time");
                        Console.WriteLine("-NOINCREMENT: Don't increment internal version");
                        Console.WriteLine("-GENERATECONFIG: Generate new configuration file including files from the current directory");
                        Console.WriteLine("-UPDATECONFIG: Update configuration file with new and deleted files");
                        Console.WriteLine("-PURGE: Purge non-existent files from configuration");
                        return;
                    default:
                        Console.WriteLine("Unknown command line argument " + arg);
                        Console.WriteLine("Press ENTER to continue.");
                        Console.ReadLine();
                        break;
                }
            }

            try
            {
                GenerateBuild();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("An error occured while processing the build. Message: " + ex.Message);
                Console.WriteLine("Stacktrace: " + Environment.NewLine + ex.StackTrace);
            }

            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }

        private static string[] ignoredFiles = new string[]
        {
            "VersionConfig.ini",
            "VersionWriter.deps.json",
            "VersionWriter.dll",
            "VersionWriter.exe",
            "VersionWriter.pdb",
            "VersionWriter.runtimeconfig.json",
            "LocalVersion",
            "ServerVersion",
        };

        private static void GenerateVersionConfigFile()
        {
            Console.WriteLine("Warning: this will overwrite the current version " +
                "configuration, including the build version information. Press " +
                "ENTER to continue.");
            Console.WriteLine();
            Console.WriteLine("If you'd like to update the configuration with new files instead " +
                "of generating a new configuration from scratch, it's recommended to run -UPDATECONFIG instead.");
            Console.ReadLine();

            VersionConfig versionConfig = new VersionConfig();

            string[] files = Directory.GetFiles(Environment.CurrentDirectory, "*", SearchOption.AllDirectories);

            var component = new Component<VersionWriterFileInfo>("Main", "Main Component", false);
            versionConfig.Components.Add(component);

            foreach (string file in files)
            {
                string relativePath = file.Substring(Environment.CurrentDirectory.Length + 1);

                if (relativePath.StartsWith(BUILD_DIRECTORY))
                    continue;

                if (Array.Exists(ignoredFiles, relativePath.EndsWith))
                    continue;

                Console.WriteLine("Including " + relativePath);
                component.AddFileInfo(new VersionWriterFileInfo(relativePath, false));
            }

            versionConfig.DisplayedVersion = "Undefined version";

            versionConfig.BuildDirectory = BUILD_DIRECTORY;

            versionConfig.Write();

            Console.WriteLine();
            Console.WriteLine("Configuration generation finished.");
        }

        private static void UpdateVersionConfigFile()
        {
            Console.WriteLine();

            Console.WriteLine("Reading configuration...");
            VersionConfig versionConfig = new VersionConfig();
            versionConfig.Parse();

            var mainComponent = versionConfig.Components.Find(c => c.InternalName.Equals("Main", StringComparison.OrdinalIgnoreCase));

            if (mainComponent == null)
            {
                Console.WriteLine("\"Main\" component not found! Unable to update version configuration! Press ENTER to exit.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Gathering list of new files (files not included in the build)...");

            string[] files = Directory.GetFiles(Environment.CurrentDirectory, "*", SearchOption.AllDirectories);

            var existingFileEntries = new List<VersionWriterFileInfo>();
            versionConfig.Components.ForEach(c => existingFileEntries.AddRange(c.FileInfos));

            foreach (string file in files)
            {
                string relativePath = file.Substring(Environment.CurrentDirectory.Length + 1).Replace('\\', '/');

                if (relativePath.StartsWith(BUILD_DIRECTORY))
                    continue;

                if (Array.Exists(ignoredFiles, relativePath.EndsWith))
                    continue;

                if (versionConfig.IgnoredFiles.Contains(relativePath))
                    continue;

                if (existingFileEntries.Exists(fileEntry => fileEntry.FilePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)))
                    continue;

                Console.WriteLine("Including " + relativePath);

                mainComponent.AddFileInfo(new VersionWriterFileInfo(relativePath, false));
            }

            var orderedFileInfos = mainComponent.FileInfos.OrderBy(fileEntry => fileEntry.FilePath.Count(c => c == '/')).ThenBy(fileEntry => fileEntry.FilePath);
            mainComponent.FileInfos.Clear();
            mainComponent.FileInfos.AddRange(orderedFileInfos);

            Console.WriteLine("Looking for deleted files...");

            foreach (var component in versionConfig.Components)
            {
                for (int i = 0; i < component.FileInfos.Count; i++)
                {
                    string path = Path.Combine(Environment.CurrentDirectory, component.FileInfos[i].FilePath);

                    if (!File.Exists(path))
                    {
                        Console.WriteLine("Removing entry for " + component.FileInfos[i].FilePath);
                        component.FileInfos.RemoveAt(i);
                        i--;
                    }
                }

                if (component.FileInfos.Count == 0)
                {
                    LogWarning($"Warning: Component {component.InternalName} has 0 files in it.");
                }
            }

            versionConfig.Write();

            Console.WriteLine();
            Console.WriteLine("Updating configuration finished.");
        }

        private static void LogWarning(string str)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(str);
            Console.ForegroundColor = oldColor;
        }

        private static void GenerateBuild()
        {
            char dsc = Path.DirectorySeparatorChar;

            Console.WriteLine("Reading configuration...");
            VersionConfig versionConfig = new VersionConfig();
            versionConfig.Parse();

            Console.WriteLine("Gathering list of outdated files in the build directory...");
            var filesToProcess = versionConfig.GetOutdatedFileList();

            Directory.CreateDirectory(Environment.CurrentDirectory + dsc + versionConfig.BuildDirectory);

            foreach (VersionWriterFileInfo fileEntry in filesToProcess)
            {
                string filePath = Environment.CurrentDirectory + dsc + versionConfig.BuildDirectory + dsc + fileEntry.FilePath;
                string originalFilePath = Environment.CurrentDirectory + dsc + fileEntry.FilePath;

                if (!File.Exists(originalFilePath))
                {
                    Console.WriteLine($"Warning: file {fileEntry.FilePath} included in version configuration does not exist. Press ENTER to continue.");
                    Console.ReadLine();
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                if (!fileEntry.Compressed)
                {
                    Console.WriteLine("Copying " + fileEntry.FilePath);
                    File.Copy(originalFilePath, filePath, true);
                }
                else
                {
                    Console.WriteLine("Compressing " + fileEntry.FilePath);
                    CompressionHelper.CompressFile(originalFilePath, filePath + RemoteFileInfo.COMPRESSED_FILE_EXTENSION);
                }
            }

            Console.WriteLine("Generating new version files...");

            if (incrementVersion)
                versionConfig.InternalVersion++;

            if (generateVersionFromDate)
                versionConfig.GenerateVersionDisplayStringFromCurrentDate();

            versionConfig.WriteVersionFiles();

            Console.WriteLine("Cleaning build directory from potential leftover files...");
            versionConfig.CleanBuildDirectory();

            Console.WriteLine("Refreshing version configuration...");
            versionConfig.Write();

            Console.WriteLine();

            Console.WriteLine("List of modified files:");
            Console.ForegroundColor = ConsoleColor.Green;

            foreach (VersionWriterFileInfo fileEntry in filesToProcess)
            {
                Console.WriteLine(fileEntry.FilePath);
            }
        }

        private static void PurgeFileList()
        {
            Console.WriteLine("Reading configuration...");
            VersionConfig versionConfig = new VersionConfig();
            versionConfig.Parse();

            Console.WriteLine("Purging non-existent files...");

            foreach (var component in versionConfig.Components)
            {
                for (int i = 0; i < component.FileInfos.Count; i++)
                {
                    var entry = component.FileInfos[i];
                    string path = Path.Combine(Environment.CurrentDirectory, entry.FilePath);

                    if (!File.Exists(path))
                    {
                        Console.WriteLine($"Removing non-existent file {entry.FilePath} from component {component.InternalName}");
                        component.FileInfos.RemoveAt(i);
                        i--;
                    }
                }

                if (component.FileInfos.Count == 0)
                {
                    LogWarning($"Warning: Component {component.InternalName} has 0 files in it.");
                }
            }

            versionConfig.Write();

            Console.WriteLine();
            Console.WriteLine("Configuration purging finished.");
        }
    }
}
