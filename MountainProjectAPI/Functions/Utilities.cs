using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
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
            return Regex.Replace(input, @"[^\p{L}0-9]", "");
        }

        public static bool StringsEqual(string inputString, string targetString, bool caseInsensitive = true)
        {
            string input = inputString;
            string target = targetString;

            if (caseInsensitive)
            {
                input = input.ToLower();
                target = target.ToLower();
            }

            return target == input;
        }

        public static bool StringsMatch(string inputString, string targetString, bool caseInsensitive = true)
        {
            string input = inputString;
            string target = targetString;

            if (caseInsensitive)
            {
                input = input.ToLower();
                target = target.ToLower();
            }

            return target.Contains(input);
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

        public static bool IsNumber(string inputString)
        {
            return int.TryParse(inputString, out _);
        }

        public static string TrimWords(string input, string[] wordsToTrim)
        {
            var result = string.Join(" ", input.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries)
                .SkipWhile(x => wordsToTrim.Contains(x.ToLower()))
                .Reverse()
                .SkipWhile(x => wordsToTrim.Contains(x.ToLower()))
                .Reverse());

            return result;
        }

        public static List<string> GetWordGroups(string phrase)
        {
            return findWords(phrase.Split(' ')).ToList();
        }

        private static string[] findWords(params string[] args)
        {

            if (args.Length == 0)
            {
                return new string[] { "" };
            }
            else
            {
                string[] oldWords = findWords(args.Skip(1).ToArray());
                string[] newWords = oldWords.Where(word => word == "" || word.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)[0] == args[1])
                                            .Select(word => (args[0] + " " + word).Trim()).ToArray();

                return oldWords.Union(newWords).ToArray();
            }
        }
    }
}
