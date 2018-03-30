using Rampastring.Updater;
using Rampastring.Updater.BuildInfo;
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
        static void Main(string[] args)
        {
            new Program().Start();
        }

        private void Start()
        {
            Console.WriteLine("VersionWriter for Rampastring.Updater");
            Console.WriteLine();

            Console.WriteLine("Reading version configuration...");
            VersionConfig versionConfig = new VersionConfig();
            versionConfig.Parse();

            // Create new build infos,
            // parse existing version files to figure out which files are new or changed,
            // copy and compress new files, clean target directory...
        }
    }
}
