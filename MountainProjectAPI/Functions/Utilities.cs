using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MountainProjectAPI
{
    public static class Utilities
    {
        public const string MPBASEURL = "https://www.mountainproject.com";
        public const string ALLLOCATIONSURL = "https://www.mountainproject.com/route-guide";
        public const string INTERNATIONALURL = "https://www.mountainproject.com/area/105907743/international";
        public const string AUSTRALIAURL = "https://www.mountainproject.com/area/105907756/australia";

        public static bool MatchesStateUrlRegex(string urlToMatch)
        {
            List<string> states = new List<string>()
            {
                "Alabama",
                "Alaska",
                "Arizona",
                "Arkansas",
                "California",
                "Colorado",
                "Connecticut",
                "Delaware",
                "Florida",
                "Georgia",
                "Hawaii",
                "Idaho",
                "Illinois",
                "Indiana",
                "Iowa",
                "Kansas",
                "Kentucky",
                "Louisiana",
                "Maine",
                "Maryland",
                "Massachusetts",
                "Michigan",
                "Minnesota",
                "Mississippi",
                "Missouri",
                "Montana",
                "Nebraska",
                "Nevada",
                "New-Hampshire",
                "New-Jersey",
                "New-Mexico",
                "New-York",
                "North-Carolina",
                "North-Dakota",
                "Ohio",
                "Oklahoma",
                "Oregon",
                "Pennsylvania",
                "Rhode-Island",
                "South-Carolina",
                "South-Dakota",
                "Tennessee",
                "Texas",
                "Utah",
                "Vermont",
                "Virginia",
                "Washington",
                "West-Virginia",
                "Wisconsin",
                "Wyoming",
                "International"
            };

            foreach (string state in states)
            {
                string sanitizedString = MPBASEURL.Replace("/", "\\/").Replace(".", "\\.");
                Regex stateRegex = new Regex(sanitizedString + "\\/area\\/\\d*\\/" + state.ToLower() + "$");
                if (stateRegex.IsMatch(urlToMatch))
                    return true;
            }

            return false;
        }

        public static IHtmlDocument GetHtmlDoc(string url)
        {
            HtmlParser parser = new HtmlParser();
            string html = "";
            using (WebClient client = new WebClient() { Encoding = Encoding.UTF8 })
            {
                html = client.DownloadString(url);
            }

            IHtmlDocument doc = parser.ParseDocument(html);

            return doc;
        }

        public static async Task<IHtmlDocument> GetHtmlDocAsync(string url)
        {
            HtmlParser parser = new HtmlParser();
            string html = "";
            using (WebClient client = new WebClient() { Encoding = Encoding.UTF8 })
            {
                html = await client.DownloadStringTaskAsync(url);
            }

            IHtmlDocument doc = parser.ParseDocument(html);

            return doc;
        }

        public static string FilterStringForMatch(string input)
        {
            return Regex.Replace(input, @"\P{L}", "");
        }

        public static string GetRedirectURL(string url)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                return ((HttpWebResponse)req.GetResponse()).ResponseUri.ToString();
            }
            catch
            {
                return "";
            }
        }
    }
}
