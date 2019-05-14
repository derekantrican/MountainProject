using System.Collections.Generic;

namespace MountainProjectAPI
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

        public double Rating { get; set; }
        public string Grade { get; set; }
        public List<RouteType> Types { get; set; }
        public string TypeString { get { return string.Join(", ", Types); } }
        public string AdditionalInfo { get; set; }

        public override string ToString()
        {
            string result = "";
            result += $"{this.Name} [{this.TypeString} {this.Grade}";

            if (!string.IsNullOrEmpty(this.AdditionalInfo))
                result += ", " + this.AdditionalInfo;

            result += "]";

            return result;
        }
    }
}
