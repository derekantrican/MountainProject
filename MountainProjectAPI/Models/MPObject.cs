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

        public string Name { get; set; }
        public string NameForMatch { get; set; }
        public string ID { get; set; }
        public int Popularity { get; set; }
        public List<string> ParentIDs { get; set; }
        [XmlIgnore]
        public List<MPObject> Parents { get; set; }

        [XmlIgnore]
        public string URL
        {
            get
            {
                if (this is Route)
                    return $"{Utilities.MPROUTEURL}/{ID}";
                else if (this is Area)
                    return $"{Utilities.MPAREAURL}/{ID}";

                return null;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
