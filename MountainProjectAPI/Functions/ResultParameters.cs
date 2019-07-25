using System.Text.RegularExpressions;
using static MountainProjectAPI.Route;

namespace MountainProjectAPI
{
    public class ResultParameters
    {
        public GradeSystem GradeSystem { get; set; }

        public static ResultParameters ParseParameters(ref string input)
        {
            ResultParameters parameters = new ResultParameters();
            if (Regex.IsMatch(input, "-grade", RegexOptions.IgnoreCase))
            {
                string system = Regex.Match(input, @"-grade:([^-\n]*)", RegexOptions.IgnoreCase).Groups[1].Value;
                switch (system.ToLower().Replace(" ", ""))
                {
                    case "yds":
                    case "usa":
                    case "us":
                        parameters.GradeSystem = GradeSystem.YDS;
                        break;
                    case "french":
                        parameters.GradeSystem = GradeSystem.French;
                        break;
                    case "ewbanks":
                    case "australia":
                    case "nz":
                        parameters.GradeSystem = GradeSystem.Ewbanks;
                        break;
                    case "uiaa":
                        parameters.GradeSystem = GradeSystem.UIAA;
                        break;
                    case "southafrica":
                    case "za":
                        parameters.GradeSystem = GradeSystem.SouthAfrica;
                        break;
                    case "british":
                    case "uk":
                        parameters.GradeSystem = GradeSystem.Britsh;
                        break;
                    case "hueco":
                        parameters.GradeSystem = GradeSystem.Hueco;
                        break;
                    case "fontainebleau":
                    case "font":
                        parameters.GradeSystem = GradeSystem.Fontainebleau;
                        break;
                }

                input = Regex.Replace(input, @"-grade:[^-\n]*", "", RegexOptions.IgnoreCase).Trim();
            }

            return parameters;
        }
    }
}
