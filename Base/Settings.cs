using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Base
{
    public static class Settings
    {
        public static string ReadSettingValue(string filePath, string settingName)
        {
            List<string> fileLines = File.ReadAllLines(filePath).ToList();
            return fileLines.FirstOrDefault(p => p.StartsWith(settingName)).Split(new[] { ':' }, 2)[1]; //Split on first occurence only because requestForApprovalURL also contains ':'
        }
    }
}
