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
            List<string> states = StatesWithAbbr.Keys.ToList();
            states.Add("International");

            foreach (string state in states)
            {
                string sanitizedString = MPBASEURL.Replace("/", "\\/").Replace(".", "\\.");
                Regex stateRegex = new Regex(sanitizedString + "\\/area\\/\\d*\\/" + state.Replace(" ", "-").ToLower() + "$");
                if (stateRegex.IsMatch(urlToMatch))
                    return true;
            }

            return false;
        }

        public static Dictionary<string, string> StatesWithAbbr = new Dictionary<string, string>
        {
            {"Alabama", "AL"},
            {"Alaska", "AK"},
            {"Arizona", "AZ"},
            {"Arkansas", "AR"},
            {"California", "CA"},
            {"Colorado", "CO"},
            {"Connecticut", "CT"},
            {"Delaware", "DE"},
            {"Florida", "FL"},
            {"Georgia", "GA"},
            {"Hawaii", "HI"},
            {"Idaho", "ID"},
            {"Illinois", "IL"},
            {"Indiana", "IN"},
            {"Iowa", "IA"},
            {"Kansas", "KS"},
            {"Kentucky", "KY"},
            {"Louisiana", "LA"},
            {"Maine", "ME"},
            {"Maryland", "MD"},
            {"Massachusetts", "MA"},
            {"Michigan", "MI"},
            {"Minnesota", "MN"},
            {"Mississippi", "MS"},
            {"Missouri", "MO"},
            {"Montana", "MT"},
            {"Nebraska", "NE"},
            {"Nevada", "NV"},
            {"New Hampshire", "NH"},
            {"New Jersey", "NJ"},
            {"New Mexico", "NM"},
            {"New York", "NY"},
            {"North Carolina", "NC"},
            {"North Dakota", "ND"},
            {"Ohio", "OH"},
            {"Oklahoma", "OK"},
            {"Oregon", "OR"},
            {"Pennsylvania", "PA"},
            {"Rhode Island", "RI"},
            {"South Carolina", "SC"},
            {"South Dakota", "SD"},
            {"Tennessee", "TN"},
            {"Texas", "TX"},
            {"Utah", "UT"},
            {"Vermont", "VT"},
            {"Virginia", "VA"},
            {"Washington", "WA"},
            {"West Virginia", "WV"},
            {"Wisconsin", "WI"},
            {"Wyoming", "WY"}
        };

        public static IHtmlDocument GetHtmlDoc(string url)
        {
            HtmlParser parser = new HtmlParser();
            string html = "";
            using (WebClient client = new WebClient() { Encoding = Encoding.UTF8 })
            {
                int retries = 0;
                while (true)
                {
                    try
                    {
                        html = client.DownloadString(url);
                        break;
                    }
                    catch
                    {
                        if (retries <= 5)
                        {
                            Console.WriteLine($"Download string failed. Trying again ({retries})");
                            retries++;
                        }
                        else
                            throw;
                    }
                }
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
                int retries = 0;
                while (true)
                {
                    try
                    {
                        html = await client.DownloadStringTaskAsync(url);
                        break;
                    }
                    catch
                    {
                        if (retries <= 5)
                        {
                            Console.WriteLine($"Download string failed. Trying again ({retries})");
                            retries++;
                        }
                        else
                            throw;
                    }
                }
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

        public static string GetRedirectURL(string url) //Credit: https://stackoverflow.com/a/28424940/2246411
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            int maxRedirCount = 8;  // prevent infinite loops
            string newUrl = url;
            do
            {
                HttpWebResponse resp = null;
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "HEAD";
                    req.AllowAutoRedirect = false;
                    resp = (HttpWebResponse)req.GetResponse();
                    switch (resp.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            return newUrl;
                        case HttpStatusCode.Redirect:
                        case HttpStatusCode.MovedPermanently:
                        case HttpStatusCode.RedirectKeepVerb:
                        case HttpStatusCode.RedirectMethod:
                            newUrl = resp.Headers["Location"];
                            if (newUrl == null)
                                return url;

                            if (newUrl.IndexOf("://", System.StringComparison.Ordinal) == -1)
                            {
                                // Doesn't have a URL Schema, meaning it's a relative or absolute URL
                                Uri u = new Uri(new Uri(url), newUrl);
                                newUrl = u.ToString();
                            }
                            break;
                        default:
                            return newUrl;
                    }
                    url = newUrl;
                }
                catch (WebException)
                {
                    // Return the last known good URL
                    return newUrl;
                }
                catch (Exception)
                {
                    return null;
                }
                finally
                {
                    if (resp != null)
                        resp.Close();
                }
            } while (maxRedirCount-- > 0);

            return newUrl;
        }

        public static string GetID(string mpURL)
        {
            return mpURL.Replace($"{MPBASEURL}/route/", "").Replace($"{MPBASEURL}/area/", "").Split('/')[0];
        }

        public static string GetSimpleURL(string mpUrl)
        {
            return Regex.Match(mpUrl, $@"{MPBASEURL}/(route|area)/\d+").Value;
        }

        public static bool IsNumber(string inputString)
        {
            return int.TryParse(inputString, out _);
        }

        public static List<string> GetWordGroups(string phrase)
        {
            return FindWords(phrase.Split(' ')).ToList();
        }

        private static string[] FindWords(params string[] args)
        {

            if (args.Length == 0)
            {
                return new string[] { "" };
            }
            else
            {
                string[] oldWords = FindWords(args.Skip(1).ToArray());
                string[] newWords = oldWords.Where(word => word == "" || word.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)[0] == args[1])
                                            .Select(word => (args[0] + " " + word).Trim()).ToArray();

                return oldWords.Union(newWords).ToArray();
            }
        }

        public static string TrimWords(string input, string[] wordsToTrim)
        {
            return TrimWordsStart(TrimWordsEnd(input, wordsToTrim), wordsToTrim);
        }

        public static string TrimWordsStart(string input, string[] wordsToTrim)
        {
            var result = string.Join(" ", input.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries)
                    .SkipWhile(x => wordsToTrim.Contains(x.ToLower())));
            return result;
        }

        public static string TrimWordsEnd(string input, string[] wordsToTrim)
        {
            var result = string.Join(" ", input.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries)
                    .Reverse()
                    .SkipWhile(x => wordsToTrim.Contains(x.ToLower()))
                    .Reverse());
            return result;
        }
    }
}
