using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MountainProjectBot
{
    public static class Markdown
    {
        public static string Bold(string textToBold)
        {
            return $"**{textToBold}**";
        }

        public static string Link(string linkText, string linkUrl)
        {
            return $"[{linkText}]({linkUrl})";
        }

        public static string HRule { get { return "\n\n-----\n\n"; } }
    }
}
