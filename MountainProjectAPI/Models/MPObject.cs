using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace MountainProjectAPI
{
    public class MPObject
    {
        public MPObject(string name, string id)
        {
            this.Name = WebUtility.HtmlDecode(name);
            this.ID = id;
            this.ParentIDs = new List<string>();
            this.Parents = new List<MPObject>();
        }

        public MPObject()
        {
            this.ParentIDs = new List<string>();
            this.Parents = new List<MPObject>();
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
                NameForMatch = Utilities.FilterStringForMatch(Utilities.EnfoceWordConsistency(name));
            }
        }

        public string NameForMatch { get; set; }

        [XmlIgnore]
        public string URL
        {
            get
            {
                if (this is Route)
                    return $"{Utilities.MPBASEURL}/route/{ID}";
                else if (this is Area)
                    return $"{Utilities.MPBASEURL}/area/{ID}";

                return null;
            }
        }

        public string ID { get; set; }
        public int Popularity { get; set; }
        public List<string> ParentIDs { get; set; }
        [XmlIgnore]
        public List<MPObject> Parents { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
