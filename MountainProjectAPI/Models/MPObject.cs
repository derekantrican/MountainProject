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
                    value = value.Replace(", The", "");
                    value = "The " + value;
                }

                name = value.Trim();

                //Remove any special characters (spaces, apostrophes, etc) and leave only letter characters (of any language)
                //Ideally, we would do this during MountainProjectDataSearch.StringMatch but Regex.Replace takes a significant 
                //amount of time. So running it during the DBBuild saves time for the Reddit bot
                NameForMatch = Utilities.FilterStringForMatch(name);
            }
        }

        public string NameForMatch { get; set; }

        public string URL { get; set; }
        public int Popularity { get; set; }
        public List<string> ParentUrls = new List<string>();

        public override string ToString()
        {
            return Name;
        }
    }
}
