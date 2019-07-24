using System.Text.RegularExpressions;

namespace MountainProjectAPI
{
    public class SearchParameters
    {
        public bool OnlyAreas { get; set; }
        public bool OnlyRoutes { get; set; }
        public string SpecificLocation { get; set; }

        public static SearchParameters ParseParameters(ref string input)
        {
            SearchParameters parameters = new SearchParameters();
            if (Regex.IsMatch(input, "-area", RegexOptions.IgnoreCase))
            {
                parameters.OnlyAreas = true;
                input = Regex.Replace(input, "-area", "", RegexOptions.IgnoreCase).Trim();
            }

            if (Regex.IsMatch(input, "-route", RegexOptions.IgnoreCase))
            {
                parameters.OnlyRoutes = true;
                input = Regex.Replace(input, "-route", "", RegexOptions.IgnoreCase).Trim();
            }

            if (Regex.IsMatch(input, "-location", RegexOptions.IgnoreCase))
            {
                parameters.SpecificLocation = Regex.Match(input, @"-location:([^-\n]*)", RegexOptions.IgnoreCase).Groups[1].Value;
                input = Regex.Replace(input, @"-location:[^-\n]*", "", RegexOptions.IgnoreCase).Trim();
            }

            return parameters;
        }
    }
}
