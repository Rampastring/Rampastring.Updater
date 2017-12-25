using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    public class RemoteBuildInfo : BuildInfo<RemoteFileInfo>
    {
        /// <summary>
        /// Gets or sets the base directory path of the build.
        /// </summary>
        public string LocalBuildPath { get; set; }

        /// <summary>
        /// Gets or sets the server path of the build.
        /// </summary>
        public string ServerBuildPath { get; set; }
    }
}
