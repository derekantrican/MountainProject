using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace MountainProjectAPI
{
    public class MPObject
    {
        public MPObject(string name, string url)
        {
            this.Name = WebUtility.HtmlDecode(name);
            this.URL = url;
            this.ParentUrls = new List<string>();
        }

        public MPObject()
        {
            this.ParentUrls = new List<string>();
        }

        private string name { get; set; }
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                if (Regex.Match(value, ", The", RegexOptions.IgnoreCase).Success)
                {
                    value = Regex.Replace(value, ", The", "", RegexOptions.IgnoreCase);
                    value = "The " + value;
                }

                name = value.Trim();
            }
        }
        public string URL { get; set; }
        public int Popularity { get; set; }
        public List<string> ParentUrls = new List<string>();

        public override string ToString()
        {
            return Name;
        }
    }
}
