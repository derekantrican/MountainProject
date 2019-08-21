using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
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
    }
}
