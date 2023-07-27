using System.Collections.Generic;

namespace Base
{
    public static class ExtensionMethods
    {
        public static void ExtendDictionaryList(this Dictionary<string, List<List<string>>> dict, string key, List<string> additionalList)
        {
            if (dict.ContainsKey(key))
            {
                dict[key].Add(additionalList);
            }
            else
            {
                dict[key] = new List<List<string>>
                {
                    additionalList
                };
            }
        }
    }
}
