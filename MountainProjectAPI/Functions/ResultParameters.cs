using System.Text.RegularExpressions;
using static MountainProjectAPI.Grade;

namespace MountainProjectAPI
{
    public class ResultParameters
    {
        public GradeSystem GradeSystem { get; set; }

        public static ResultParameters ParseParameters(ref string input)
        {
            ResultParameters parameters = null;
            if (Regex.IsMatch(input, "-grade", RegexOptions.IgnoreCase))
            {
                string system = Regex.Match(input, @"-grade:([^-\n]*)", RegexOptions.IgnoreCase).Groups[1].Value;
                switch (system.ToLower().Replace(" ", ""))
                {
                    case "yds":
                    case "usa":
                    case "us":
                        (parameters ??= new ResultParameters()).GradeSystem = GradeSystem.YDS;
                        break;
                    case "french":
                        (parameters ??= new ResultParameters()).GradeSystem = GradeSystem.French;
                        break;
                    case "ewbanks":
                    case "australia":
                    case "nz":
                        (parameters ??= new ResultParameters()).GradeSystem = GradeSystem.Ewbanks;
                        break;
                    case "uiaa":
                        (parameters ??= new ResultParameters()).GradeSystem = GradeSystem.UIAA;
                        break;
                    case "southafrica":
                    case "za":
                        (parameters ??= new ResultParameters()).GradeSystem = GradeSystem.SouthAfrica;
                        break;
                    case "british":
                    case "uk":
                        (parameters ??= new ResultParameters()).GradeSystem = GradeSystem.Britsh;
                        break;
                    case "hueco":
                        (parameters ??= new ResultParameters()).GradeSystem = GradeSystem.Hueco;
                        break;
                    case "fontainebleau":
                    case "font":
                        (parameters ??= new ResultParameters()).GradeSystem = GradeSystem.Fontainebleau;
                        break;
                }

                input = Regex.Replace(input, @"-grade:[^-\n]*", "", RegexOptions.IgnoreCase).Trim();
            }

            return parameters;
        }
    }
}