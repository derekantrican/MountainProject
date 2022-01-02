using System.Collections.Generic;
using System.Net;
using System.Xml.Serialization;

namespace MountainProjectAPI
{
    public class MPObject
    {
        private string url;
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

        public string Name { get; set; }
        public string NameForMatch { get; set; }
        public bool IsNameRedacted { get; set; }
        public string ID { get; set; }
        public int Popularity { get; set; }
        public List<string> ParentIDs { get; set; }
        [XmlIgnore]
        public List<MPObject> Parents { get; set; }

        public void PopulateParents()
        {
            foreach (string id in ParentIDs)
            {
                MPObject matchingObject = MountainProjectDataSearch.GetItemWithMatchingID(id);
                if (!Parents.Contains(matchingObject))
                {
                    Parents.Add(matchingObject);
                }
            }
        }

        [XmlIgnore]
        public string URL
        {
            get
            {
                if (!string.IsNullOrEmpty(url))
                {
                    return url;
                }

                if (this is Route)
                    return Url.BuildFullUrl($"{Utilities.MPROUTEURL}/{ID}");
                else if (this is Area)
                    return Url.BuildFullUrl($"{Utilities.MPAREAURL}/{ID}");

                return null;
            }
            set
            {
                url = value;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
