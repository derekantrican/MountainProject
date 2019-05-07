using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MountainProjectModels
{
    public class Route : MPObject
    {
        public Route(string name, string grade, RouteType type, string url) : base(name, url)
        {
            this.Grade = grade;
            this.Types = new List<RouteType>();
        }

        public Route()
        {
            this.Types = new List<RouteType>();
        }

        public enum RouteType
        {
            Boulder,
            TopRope,
            Sport,
            Trad,
            Aid,
            Ice,
            Mixed,
            Alpine,
            Snow
        }

        public string Grade { get; set; }
        public List<RouteType> Types { get; set; }
        public string TypeString { get { return string.Join(", ", Types); } }
        public string AdditionalInfo { get; set; }

        public override string ToString()
        {
            return Name + " (" + TypeString + ", " + Grade + ")";
        }
    }
}
