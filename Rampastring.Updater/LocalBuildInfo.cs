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
    public class LocalBuildInfo : BuildInfo<LocalFileInfo>
    {
        public LocalBuildInfo()
        {
            BuildPath = Environment.CurrentDirectory + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Gets or sets the base directory path of the build.
        /// </summary>
        public string BuildPath { get; set; }

        /// <summary>
        /// Verifies the files of the local build. Returns a list of files that
        /// have different hashes from specified.
        /// </summary>
        /// <returns>A list of files that have different hashes from specified.</returns>
        public List<LocalFileInfo> Verify()
        {
            var differentFiles = new List<LocalFileInfo>();

            FileInfos.ForEach(fi => 
            {
                if (!fi.MatchesActualFile(BuildPath)) differentFiles.Add(fi);
            });

            return differentFiles;
        }
    }
}
