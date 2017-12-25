using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    public interface IFileInfo
    {
        string GetString();

        void Parse(string[] parts);
    }
}
