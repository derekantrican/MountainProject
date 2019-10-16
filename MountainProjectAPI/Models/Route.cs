using System.Collections.Generic;
using System.Linq;
using static MountainProjectAPI.Grade;

namespace MountainProjectAPI
{
    public class Route : MPObject
    {
        public Route(string name, string id) : base(name, id)
        {
            this.Grades = new List<Grade>();
            this.Types = new List<RouteType>();
        }

        public Route()
        {
            this.Grades = new List<Grade>();
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
        public List<Grade> Grades { get; set; }
        public List<RouteType> Types { get; set; }
        public string TypeString { get { return string.Join(", ", Types); } }
        public string AdditionalInfo { get; set; }
        public Dimension Height { get; set; }

        public Grade GetRouteGrade(ResultParameters parameters)
        {
            GradeSystem gradeSystem = GradeSystem.YDS;
            if (parameters != null)
                gradeSystem = parameters.GradeSystem;

            return GetRouteGrade(gradeSystem);
        }

        public Grade GetRouteGrade(GradeSystem requestedSystem = GradeSystem.YDS)
        {
            Grade matchingGrade = this.Grades.Find(g => g.System == requestedSystem);

            if (matchingGrade != null)
                return matchingGrade;
            else if (requestedSystem == GradeSystem.Hueco && this.Grades.Any(g => g.System == GradeSystem.YDS)) //If the user wanted hueco, but we only have YDS
                return this.Grades.Find(g => g.System == GradeSystem.YDS);
            else if (requestedSystem == GradeSystem.YDS && this.Grades.Any(g => g.System == GradeSystem.Hueco)) //If the user wanted YDS, but we only have Hueco
                return this.Grades.Find(g => g.System == GradeSystem.Hueco);
            else if (requestedSystem == GradeSystem.French && this.Grades.Any(g => g.System == GradeSystem.Fontainebleau)) //If the user wanted French, but we only have Fontainebleau
                return this.Grades.Find(g => g.System == GradeSystem.Fontainebleau);
            else if (requestedSystem == GradeSystem.Fontainebleau && this.Grades.Any(g => g.System == GradeSystem.French)) //If the user wanted Fontainebleau, but we only have French
                return this.Grades.Find(g => g.System == GradeSystem.French);
            else if (this.Grades.Any(g => g.System == GradeSystem.Unlabled))
                return this.Grades.Find(g => g.System == GradeSystem.Unlabled);
            else if (this.Grades.Any(g => g.System == GradeSystem.YDS)) //Default to YDS
                return this.Grades.Find(g => g.System == GradeSystem.YDS);
            else
                return null;
        }

        public override string ToString()
        {
            string result = "";
            result += $"{this.Name} [{this.TypeString} {this.GetRouteGrade(GradeSystem.YDS)}";

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