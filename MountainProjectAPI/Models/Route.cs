using System.Collections.Generic;

namespace MountainProjectAPI
{
    public class Route : MPObject
    {
        public Route(string name, string url) : base(name, url)
        {
            this.Grades = new SerializableDictionary<GradeSystem, string>();
            this.Types = new List<RouteType>();
        }

        public Route()
        {
            this.Grades = new SerializableDictionary<GradeSystem, string>();
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

        public enum GradeSystem
        {
            YDS,
            French,
            Ewbanks,
            UIAA,
            SouthAfrica,
            Britsh,
            Hueco,
            Fontainebleau,
            Unlabled
        }

        public double Rating { get; set; }
        public SerializableDictionary<GradeSystem, string> Grades { get; set; }
        public List<RouteType> Types { get; set; }
        public string TypeString { get { return string.Join(", ", Types); } }
        public string AdditionalInfo { get; set; }

        public override string ToString()
        {
            string result = "";
            result += $"{this.Name} [{this.TypeString} {this.Grades[GradeSystem.YDS]}";

            if (!string.IsNullOrEmpty(this.AdditionalInfo))
                result += ", " + this.AdditionalInfo;

            result += "]";

            return result;
        }
    }
}
