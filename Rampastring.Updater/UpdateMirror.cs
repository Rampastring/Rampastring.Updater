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

        public static UpdateMirror FromString(string input)
        {
            string[] parts = input.Split(',');

            if (parts.Length != 2)
            {
                throw new ParseException("Update mirror input string \"" + input +
                        "\" did not match the expected format of \"URL, UI display string\"");
            }

            return new UpdateMirror(parts[0], parts[1].Trim());
        }
    }
}
