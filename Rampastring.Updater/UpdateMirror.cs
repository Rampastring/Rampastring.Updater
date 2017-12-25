using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rampastring.Updater
{
    public class UpdateMirror
    {
        public UpdateMirror() { }

        public UpdateMirror(string url, string uiName)
        {
            URL = url;
            UIName = uiName;
        }

        public string URL { get; private set; }
        public string UIName { get; private set; }
        public int Rating { get; set; }

        public static UpdateMirror Parse(string info)
        {
            string[] parts = info.Split(',');

            if (parts.Length < 2)
                throw new ParseException("Info string needs to have at least 2 parts separated by a colon.");

            return new UpdateMirror(parts[0], parts[1]);
        }
    }
}
