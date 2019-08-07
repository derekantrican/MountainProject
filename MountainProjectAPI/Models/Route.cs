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

        public string GetRouteGrade(ResultParameters parameters)
        {
            GradeSystem gradeSystem = GradeSystem.YDS;
            if (parameters != null)
                gradeSystem = parameters.GradeSystem;

            return GetRouteGrade(gradeSystem);
        }

        public string GetRouteGrade(GradeSystem requestedSystem = GradeSystem.YDS)
        {
            if (this.Grades.ContainsKey(requestedSystem))
                return $"{this.Grades[requestedSystem]} ({requestedSystem.ToString()})";
            else if (requestedSystem == GradeSystem.Hueco && this.Grades.ContainsKey(GradeSystem.YDS)) //If the user wanted hueco, but we only have YDS
                return $"{this.Grades[GradeSystem.YDS]} ({GradeSystem.YDS.ToString()})";
            else if (requestedSystem == GradeSystem.YDS && this.Grades.ContainsKey(GradeSystem.Hueco)) //If the user wanted YDS, but we only have Hueco
                return $"{this.Grades[GradeSystem.Hueco]} ({GradeSystem.Hueco.ToString()})";
            else if (requestedSystem == GradeSystem.French && this.Grades.ContainsKey(GradeSystem.Fontainebleau)) //If the user wanted French, but we only have Fontainebleau
                return $"{this.Grades[GradeSystem.Fontainebleau]} ({GradeSystem.Fontainebleau.ToString()})";
            else if (requestedSystem == GradeSystem.Fontainebleau && this.Grades.ContainsKey(GradeSystem.French)) //If the user wanted Fontainebleau, but we only have French
                return $"{this.Grades[GradeSystem.French]} ({GradeSystem.French.ToString()})";
            else if (this.Grades.ContainsKey(GradeSystem.Unlabled))
                return this.Grades[GradeSystem.Unlabled];
            else if (this.Grades.ContainsKey(GradeSystem.YDS))
                return $"{this.Grades[GradeSystem.YDS]} ({GradeSystem.YDS.ToString()})";

            return "";
        }

        public override string ToString()
        {
            string result = "";
            result += $"{this.Name} [{this.TypeString} {this.GetRouteGrade()}";

            if (!string.IsNullOrEmpty(this.AdditionalInfo))
                result += ", " + this.AdditionalInfo;

            result += "]";

            return result;
        }

        public string ToString(ResultParameters resultParameters)
        {
            string result = "";
            result += $"{this.Name} [{this.TypeString} {this.GetRouteGrade(resultParameters)}";

            if (!string.IsNullOrEmpty(this.AdditionalInfo))
                result += ", " + this.AdditionalInfo;

            result += "]";

            return result;
        }
    }
}
