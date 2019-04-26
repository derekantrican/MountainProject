using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MountainProjectDBBuilder
{
    public static class Common
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
            if (!File.Exists(LogPath))
                File.Create(LogPath).Close();

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

        public static TimeSpan Average(List<TimeSpan> timeSpanList)
        {
            if (timeSpanList.Count == 0)
                return new TimeSpan();

            double doubleAverageTicks = timeSpanList.Average(timeSpan => timeSpan.Ticks);
            long longAverageTicks = Convert.ToInt64(doubleAverageTicks);

            return new TimeSpan(longAverageTicks);
        }

        public static int StringMatch(string string1, string string2, bool caseInvariant = true, bool removeWhitespace = true, bool removeNonAlphaNumeric = true)
        {
            if (caseInvariant)
            {
                string1 = string1.ToLower();
                string2 = string2.ToLower();
            }

            if (removeWhitespace)
            {
                string1 = string1.Replace(" ", "");
                string2 = string2.Replace(" ", "");
            }

            if (removeNonAlphaNumeric)
            {
                string1 = Regex.Replace(string1, @"[^a-z\d]", "");
                string2 = Regex.Replace(string2, @"[^a-z\d]", "");
            }

            /// <summary>
            /// Compute the Levenshtein distance between two strings https://www.dotnetperls.com/levenshtein
            /// </summary>
            int n = string1.Length;
            int m = string2.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0)
            {
                return m;
            }

            if (m == 0)
            {
                return n;
            }

            for (int i = 0; i <= n; d[i, 0] = i++)
            {
            }

            for (int j = 0; j <= m; d[0, j] = j++)
            {
            }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (string2[j - 1] == string1[i - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            // Step 7
            return d[n, m];
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
