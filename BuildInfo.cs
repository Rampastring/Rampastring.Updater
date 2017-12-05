using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    /// <summary>
    /// Represents a product build. Includes product version information 
    /// and file information.
    /// </summary>
    class BuildInfo
    {
        public ProductVersionInfo ProductVersionInfo { get; private set; }

    }
}
