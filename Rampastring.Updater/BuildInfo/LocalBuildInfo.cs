using System;
using System.Collections.Generic;
using System.IO;

namespace Rampastring.Updater.BuildInfo
{
    public interface IComponentInstallationInfoContainer
    {
        void MarkComponentAsInstalled(string component);
        bool IsComponentInstalled<T>(Component<T> component) where T : IFileInfo, new();
        bool IsComponentInstalled(string componentName);
    }

    /// <summary>
    /// Represents a local product build. Includes product version information 
    /// and file information.
    /// </summary>
    public class LocalBuildInfo : BuildInfo<LocalFileInfo>, IComponentInstallationInfoContainer
    {
        public LocalBuildInfo()
        {
            BuildPath = Environment.CurrentDirectory + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Gets or sets the base directory path of the build.
        /// </summary>
        public string BuildPath { get; set; }

        public List<string> InstalledComponents { get; } = new List<string>();

        /// <summary>
        /// Verifies the files of the local build. Returns a list of files that
        /// have different hashes from specified.
        /// </summary>
        /// <returns>A list of files that have different hashes from specified.</returns>
        public List<LocalFileInfo> Verify()
        {
            var differentFiles = new List<LocalFileInfo>();

            foreach (var component in Components)
            {
                if (component.Optional && !IsComponentInstalled(component))
                    continue;

                foreach (var fileInfo in component.FileInfos)
                {
                    if (!fileInfo.MatchesActualFile(BuildPath))
                        differentFiles.Add(fileInfo);
                }
            }

            return differentFiles;
        }

        public void MarkComponentAsInstalled(string component)
        {
            InstalledComponents.Add(component);
        }

        public bool IsComponentInstalled<T>(Component<T> component) where T : IFileInfo, new()
        {
            return InstalledComponents.Contains(component.InternalName);
        }

        public bool IsComponentInstalled(string componentName)
        {
            return InstalledComponents.Contains(componentName);
        }
    }
}
