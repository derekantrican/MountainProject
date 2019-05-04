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
        public static string BaseUrl = "https://www.mountainproject.com";
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
                Regex stateRegex = new Regex(RegexSanitize(BaseUrl) + "\\/area\\/\\d*\\/" + state.ToLower() + "$");
                if (stateRegex.IsMatch(urlToMatch))
                    return true;
            }

            return false;
        }

        public static string RegexSanitize(string input)
        {
            return input.Replace("/", "\\/").Replace(".", "\\.");
        }

        public static bool StringMatch(string inputString, string targetString)
        {
            //Match regardless of # of spaces
            string string1 = inputString.Replace(" ", "");
            string string2 = targetString.Replace(" ", "");

            return string1.Equals(string2, StringComparison.InvariantCultureIgnoreCase);
        }

        public static TimeSpan Average(List<TimeSpan> timeSpanList)
        {
            if (timeSpanList.Count == 0)
                return new TimeSpan();

            double doubleAverageTicks = timeSpanList.Average(timeSpan => timeSpan.Ticks);
            long longAverageTicks = Convert.ToInt64(doubleAverageTicks);

            return new TimeSpan(longAverageTicks);
        }

        public static IHtmlDocument GetHtmlDoc(string url)
        {
            HtmlParser parser = new HtmlParser();
            string html = "";
            using (WebClient client = new WebClient())
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
            using (WebClient client = new WebClient())
            {
                html = await client.DownloadStringTaskAsync(url);
            }

            IHtmlDocument doc = parser.ParseDocument(html);

            return doc;
        }
    }
}
