using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common
{
    public static class Utilities
    {
        public const string MPBASEURL = "https://www.mountainproject.com";
        public const string ALLLOCATIONSURL = "https://www.mountainproject.com/route-guide";
        public const string INTERNATIONALURL = "https://www.mountainproject.com/area/105907743/international";
        public static string LogPath;
        public static string LogString = "";
        public static bool ShowLogLines = true;

        public static void Log(string itemToLog)
        {
            LogString += itemToLog + "\n";

            if (ShowLogLines)
                Console.WriteLine(itemToLog);
        }

        public static void SaveLogToFile()
        {
            File.AppendAllText(LogPath, LogString);
        }

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
                Regex stateRegex = new Regex(RegexSanitize(MPBASEURL) + "\\/area\\/\\d*\\/" + state.ToLower() + "$");
                if (stateRegex.IsMatch(urlToMatch))
                    return true;
            }

            return false;
        }

        public static string RegexSanitize(string input)
        {
            return input.Replace("/", "\\/").Replace(".", "\\.");
        }

        public static bool StringMatch(string inputString, string targetString, bool caseInsensitive = true)
        {
            //Match regardless of # of spaces
            string input = inputString.Replace(" ", "");
            string target = targetString.Replace(" ", "");

            if (caseInsensitive)
            {
                input = input.ToLower();
                target = target.ToLower();
            }

            return target.Contains(input);
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
    }
}
