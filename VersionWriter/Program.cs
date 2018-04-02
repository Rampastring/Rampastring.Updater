using Rampastring.Updater;
using Rampastring.Updater.BuildInfo;
using Rampastring.Updater.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VersionWriter
{
    class Program
    {
        private static bool incrementVersion = true;
        private static bool generateVersionFromDate = false;

        public static void Main(string[] args)
        {
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
                    default:
                        Console.WriteLine("Unknown command line argument " + arg);
                        Console.WriteLine("Press ENTER to continue.");
                        Console.ReadLine();
                        break;
                }
            }

            try
            {
                Process();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("An error occured while processing the build. Message: " + ex.Message);
            }

            Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine();
            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }

        private static void Process()
        {
            char dsc = Path.DirectorySeparatorChar;

            Console.WriteLine("VersionWriter for Rampastring.Updater");
            Console.WriteLine();

            Console.WriteLine("Reading configuration...");
            VersionConfig versionConfig = new VersionConfig();
            versionConfig.Parse();

            Console.WriteLine("Gathering list of outdated files in the build directory...");
            List<FileEntry> filesToProcess = versionConfig.GetOutdatedFileList();

            Directory.CreateDirectory(Environment.CurrentDirectory + dsc + versionConfig.BuildDirectory);

            foreach (FileEntry fileEntry in filesToProcess)
            {
                string filePath = Environment.CurrentDirectory + dsc + versionConfig.BuildDirectory + dsc + fileEntry.FilePath;
                string originalFilePath = Environment.CurrentDirectory + dsc + fileEntry.FilePath;

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                if (!fileEntry.Compressed)
                {
                    Console.WriteLine("Copying " + fileEntry.FilePath);
                    File.Copy(originalFilePath,
                        filePath, true);
                }
                else
                {
                    Console.WriteLine("Compressing " + fileEntry.FilePath);
                    CompressionHelper.CompressFile(originalFilePath, filePath + RemoteFileInfo.COMPRESSED_FILE_EXTENSION);
                }
            }

            Console.WriteLine("Cleaning build directory from potential leftover files...");
            versionConfig.CleanBuildDirectory();

            Console.WriteLine("Generating new version files...");

            if (incrementVersion)
                versionConfig.InternalVersion++;

            if (generateVersionFromDate)
                versionConfig.GenerateVersionDisplayStringFromCurrentDate();

            versionConfig.WriteVersionFiles();

            Console.WriteLine();

            Console.WriteLine("List of modified files:");
            Console.ForegroundColor = ConsoleColor.Green;

            foreach (FileEntry fileEntry in filesToProcess)
            {
                Console.WriteLine(fileEntry.FilePath);
            }
        }
    }
}
