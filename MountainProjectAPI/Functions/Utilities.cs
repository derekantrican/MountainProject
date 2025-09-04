using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MountainProjectAPI
{
    public static class Utilities
    {
        public const string MPBASEURL = "mountainproject.com";
        public const string MPROUTEURL = "mountainproject.com/route";
        public const string MPAREAURL = "mountainproject.com/area";
        public const string ALLLOCATIONSSUFFIX = "route-guide";
        public const string ALLLOCATIONSURL = $"mountainproject.com/{ALLLOCATIONSSUFFIX}";
        public const string INTERNATIONALURL = "mountainproject.com/area/105907743/international";

        private const string RegexAgnosticBaseUrl = @"(https?:\/\/)?(www\.)?mountainproject\.com\/";
        public static Regex AllLocationsRegex = new Regex($@"{RegexAgnosticBaseUrl}route-guide");

        public static bool MatchesStateUrlRegex(string urlToMatch)
        {
            List<string> states = StatesWithAbbr.Keys.ToList();
            states.Add("International");

            Regex stateUrlRegex = new Regex($@"{RegexAgnosticBaseUrl}area\/\d{{9}}\/({string.Join('|', states.Select(s => s.Replace(' ', '-').ToLower()))})$");
            return stateUrlRegex.IsMatch(urlToMatch);
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

        public static Dictionary<string, string> AreaNicknames = new Dictionary<string, string>
        {
            { "105841134", @"\bRRG\b" }, //Red River Gorge
            { "112439039", @"\bBRRP\b" }, //Bald Rock Recreational Preserve
            { "113747976", @"\bCDCA\b" }, //Cathedral Domain Climbing Area
            { "110230324", @"\bMFRP\b" }, //Miller Fork Recreational Preserve
            { "106585368", @"\bPMRP\b" }, //Pendergrass-Murray Recreational Preserve
            { "105855991", @"\bNRG\b" }, //New River Gorge
            { "106031921", @"\bLRC\b" }, //Little Rock City
            { "106094862", @"\bHP ?40\b" }, //Horse Pens 40
            { "105739277", @"\bLCC\b" }, //Little Cottonwood Canyon
            { "106008886", @"\bFont\b" }, //Fontainebleau
            { "105720495", @"\bJoshua Tree\b|\bJ ?Tree\b" }, //Joshua Tree
            { "105946429", @"\bBC\b" }, //British Columbia
            { "106007071", @"\bUK\b" }, //United Kingdom
            { "105843226", @"\bORG\b" }, //Owens River Gorge
            { "105903004", @"\bHCR\b" }, //Horseshoe Canyon Ranch
            { "105907756", @"\bAUS\b" }, //Australia
        };

        private static readonly SemaphoreSlim requestLimiter = new SemaphoreSlim(25); // limit number of simultaneous requests to try to avoid 429's (and therefore: exponential backoff time)
        private static readonly HttpClient httpClient = new HttpClient();

        public static string GetHtml(string url)
        {
            string html;
            int retries = 0;
            while (true)
            {
                try
                {
                    using (Stream stream = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, Url.BuildFullUrl(url))).Content.ReadAsStream())
                    {
                        using (StreamReader streamReader = new StreamReader(stream))
                        {
                            html = streamReader.ReadToEnd();

                            //For some reason, sometimes MountainProject returns a page with all the urls containing an extra "index.php".
                            //We can simply replace all instances of this in the html to fix our parsers
                            if (html.Contains("/index.php"))
                            {
                                html = html.Replace("/index.php", "");
                            }

                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Follow redirects
                    if (ex is WebException webEx)
                    {
                        url = GetRedirectUrlFromResponse(url, webEx.Response as HttpWebResponse);
                    }

                    if (retries <= 5)
                    {
                        Console.WriteLine($"Download string failed. Trying again ({retries})");
                        retries++;
                    }
                    else
                    {
                        Console.WriteLine($"Retries failed when trying to get HTML from {url}");
                        throw new SourceMissingException($"Retries failed when trying to get HTML from {url}", ex);
                    }
                }
            }

            return html;
        }

        public static async Task<string> GetHtmlAsync(string url)
        {
            await requestLimiter.WaitAsync(); // wait for an available slot in the limited number of requests we send

            try
            {
                string html;
                int retries = 0;
                int backoffMs = 5000;
                while (true)
                {
                    try
                    {
                        html = await httpClient.GetStringAsync(Url.BuildFullUrl(url));

                        //For some reason, sometimes MountainProject returns a page with all the urls containing an extra "index.php".
                        //We can simply replace all instances of this in the html to fix our parsers
                        if (html.Contains("/index.php"))
                        {
                            html = html.Replace("/index.php", "");
                        }

                        // HttpResponseMessage response = await httpClient.GetAsync(Url.BuildFullUrl(url));
                        // html = await response.Content.ReadAsStringAsync();

                        break;
                    }
                    catch (Exception ex)
                    {
                        //Follow redirects
                        if (ex is WebException webEx && webEx.Response != null)
                        {
                            url = GetRedirectUrlFromResponse(url, webEx.Response as HttpWebResponse);
                        }
                        else if (ex is HttpRequestException requestException && requestException.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            //Backoff with TooManyRequest errors (rate limiting). 1 second delay intervals may seem like a high starting point,
                            //but in testing I've seen the backoff get as high as 3.5s before requests start going through again. This
                            //will limit how many requests we send before we start getting successful responses again.
                            Console.WriteLine($"Too Many Requests (waiting {backoffMs}ms)");
                            Thread.Sleep(backoffMs);
                            backoffMs += 5000;
                            continue;
                        }

                        if (retries <= 5)
                        {
                            Console.WriteLine($"Download string failed. Trying again ({retries})");
                            retries++;
                        }
                        else
                        {
                            Console.WriteLine($"Retries failed when trying to get HTML from {url}");
                            throw new SourceMissingException($"Retries failed when trying to get HTML from {url}", ex);
                        }
                    }
                }

                return html;
            }
            finally
            {
                requestLimiter.Release();
            }
        }

        static HtmlParser parser;
        public static IHtmlDocument GetHtmlDoc(string url) // This non-async version is used only for "all locations" (route-guide) and ParserTests
        {
            if (parser == null)
            {
                parser = new HtmlParser();
            }

            return parser.ParseDocument(GetHtml(url));
        }

        public static async Task<IHtmlDocument> GetHtmlDocAsync(string url, bool ensurePageHeaderExists = false)
        {
            if (parser == null)
            {
                parser = new HtmlParser();
            }

            string html = await GetHtmlAsync(url);
            IHtmlDocument doc = await parser.ParseDocumentAsync(html);

            //On occassion, I have seen a route HTML "header" (the part of the page that contains the name, "Improve this page", grade, etc) not be populated when grabbing
            //with the parsers. Usually this is fine on a retry so I'm building in some retries here.

            if (ensurePageHeaderExists)
            {
                int retries = 0;
                while (retries < 3)
                {

                    IElement routeHeaderSection = null;
                    try
                    {
                        routeHeaderSection = doc.GetElementsByTagName("div").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "row pt-main-content")?.Children[0];
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(doc?.Source?.Text);
                        throw new SourceMissingException($"Failed to get HTML source for {url}", ex)
                        {
                            Html = doc?.Source?.Text,
                        };
                    }

                    if (routeHeaderSection == null || routeHeaderSection.ChildElementCount == 0)
                    {
                        ConsoleHelper.Write($"NO HEADER FOUND FOR {url}. RETRYING ({retries + 1})...", ConsoleColor.Yellow);

                        if (retries == 2)
                        {
                            Console.WriteLine(doc?.Source?.Text);
                            throw new SourceMissingException($"Failed to get HTML source for {url}")
                            {
                                Html = doc?.Source?.Text,
                            };
                        }

                        doc.Dispose();

                        html = await GetHtmlAsync(url);
                        doc = await parser.ParseDocumentAsync(html);

                        retries++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return doc;
        }

        public static string CleanExtraPartsFromName(string input)
        {
            input = Regex.Replace(input, @"\sarea(s)?(?=($|\s*[\\\/]|\s*\(|\s*\)))", "", RegexOptions.IgnoreCase);
            return input.Trim(' ', '*', '-');
        }

        public static string EnforceWordConsistency(string input)
        {
            //Convert abbreviations (eg "Mt." or "&") to full words (eg "Mount" or "and")
            input = input.Replace("Mount ", "Mt. ");
            input = input.Replace("Mister ", "Mr. ");
            input = input.Replace("&", "and");

            //Remove diacritics (eg "Landjäger" should become "Landjager") (Credit: https://stackoverflow.com/a/249126/2246411)
            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                    stringBuilder.Append(c);
            }

            input = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

            //Convert number words (eg "Twenty Three") to numbers (eg "23")
            input = NumberWordsToNumbers.ConvertString(input);

            return input;
        }

        public static string FilterStringForMatch(string input, bool removeSpaces = true)
        {
            if (removeSpaces)
                return Regex.Replace(input, @"[^\p{L}0-9]", "");
            else
                return Regex.Replace(input, @"[^\p{L}0-9 ]", "");
        }

        public static bool StringStartsWith(string containingString, string innerString, bool caseInsensitive = true)
        {
            if (caseInsensitive)
            {
                innerString = innerString.ToLower();
                containingString = containingString.ToLower();
            }

            return containingString.StartsWith(innerString);
        }

        public static bool StringStartsWithFiltered(string containingString, string innerString, bool caseInsensitive = true, bool enforceConsistentWords = true)
        {
            if (enforceConsistentWords)
            {
                return StringStartsWith(EnforceWordConsistency(containingString), EnforceWordConsistency(innerString), caseInsensitive);
            }
            else
                return StringStartsWith(containingString, innerString, caseInsensitive);
        }

        public static bool StringsEqual(string firstString, string secondString, bool caseInsensitive = true)
        {
            string first = firstString;
            string second = secondString;

            if (caseInsensitive)
            {
                first = first.ToLower();
                second = second.ToLower();
            }

            return first == second;
        }

        public static bool StringsEqualWithFilters(string firstString, string secondString, bool caseInsensitive = true, bool enforceConsistentWords = true)
        {
            if (enforceConsistentWords)
            {
                return StringsEqual(FilterStringForMatch(EnforceWordConsistency(firstString)),
                                    FilterStringForMatch(EnforceWordConsistency(secondString)), caseInsensitive);
            }
            else
                return StringsEqual(FilterStringForMatch(firstString), FilterStringForMatch(secondString), caseInsensitive);
        }

        public static bool StringContains(string containingString, string innerString, bool caseInsensitive = true, bool useRegex = false)
        {
            if (caseInsensitive)
            {
                innerString = innerString.ToLower();
                containingString = containingString.ToLower();
            }

            if (useRegex)
                return Regex.IsMatch(innerString, containingString);
            else
                return containingString.Contains(innerString);
        }

        public static bool StringContainsWithFilters(string containingString, string innerString, bool caseInsensitive = false, bool enforceConsistentWords = true)
        {
            if (enforceConsistentWords)
            {
                return StringContains(FilterStringForMatch(EnforceWordConsistency(containingString)), 
                                      FilterStringForMatch(EnforceWordConsistency(innerString)), caseInsensitive);
            }
            else
                return StringContains(FilterStringForMatch(containingString), FilterStringForMatch(innerString), caseInsensitive);
        }

        public static string GetRedirectURL(string url) //Credit: https://stackoverflow.com/a/28424940/2246411
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            int maxRedirCount = 8;  // prevent infinite loops
            string newUrl = url;

            using (HttpClient client = new HttpClient())
            {
                do
                {
                    try
                    {
                        HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Head, url);
                        HttpResponseMessage resp = client.Send(req);
                        url = GetRedirectUrlFromResponse(url, resp);
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
                } while (maxRedirCount-- > 0);
            }
            return newUrl;
        }

        private static string GetRedirectUrlFromResponse(string originalUrl, HttpResponseMessage response)
        {
            string newUrl = originalUrl;
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    return newUrl;
                case HttpStatusCode.Redirect:
                case HttpStatusCode.MovedPermanently:
                case HttpStatusCode.RedirectKeepVerb:
                case HttpStatusCode.RedirectMethod:
                    newUrl = response.Headers.Location.AbsoluteUri;

                    if (newUrl == null)
                        return originalUrl;

                    if (!newUrl.Contains("://"))
                    {
                        // Doesn't have a URL Schema, meaning it's a relative or absolute URL
                        Uri u = new Uri(new Uri(originalUrl), newUrl);
                        newUrl = u.ToString();
                    }
                    break;
                default:
                    return newUrl;
            }

            return newUrl;
        }

        private static string GetRedirectUrlFromResponse(string originalUrl, HttpWebResponse response)
        {
            string newUrl = originalUrl;
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    return newUrl;
                case HttpStatusCode.Redirect:
                case HttpStatusCode.MovedPermanently:
                case HttpStatusCode.RedirectKeepVerb:
                case HttpStatusCode.RedirectMethod:
                    newUrl = response.Headers["Location"];

                    if (newUrl == null)
                        return originalUrl;

                    if (!newUrl.Contains("://"))
                    {
                        // Doesn't have a URL Schema, meaning it's a relative or absolute URL
                        Uri u = new Uri(new Uri(originalUrl), newUrl);
                        newUrl = u.ToString();
                    }
                    break;
                default:
                    return newUrl;
            }

            return newUrl;
        }

        /// <summary>
        /// Gets a MP ID from a url
        /// </summary>
        public static string GetID(string mpUrl)
        {
            return mpUrl == null ? null : Regex.Match(mpUrl, @"\d{9}").Value;
        }

        /// <summary>
        /// Returns the &quot;ID URL&quot; (eg https://www.mountainproject.com/area/105907743) from a url. NOTE: this method
        /// IS protocol/www agnostic, but it does NOT add missing protocol/www
        /// </summary>
        public static string GetSimpleURL(string mpUrl)
        {
            return new Regex($@"{RegexAgnosticBaseUrl}(area|route)\/\d{{9}}").Match(mpUrl).Value;
        }

        public static bool IsNumber(string inputString)
        {
            return int.TryParse(inputString, out _);
        }

        public static List<string> GetWords(string input, bool removeEmpty = true)
        {
            input = Regex.Replace(input, "['’]", "");
            List<string> words = Regex.Split(input, @"[^\p{L}0-9]").ToList();

            if (removeEmpty)
                words.RemoveAll(s => string.IsNullOrEmpty(s));

            return words;
        }

        public static List<string> GetWordGroups(string phrase, bool allowSingleWords = true)
        {
            if (allowSingleWords)
                return FindWords(phrase.Split(' ')).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            else
                return FindWords(phrase.Split(' ')).Where(s => !string.IsNullOrWhiteSpace(s) && s.Contains(" ")).Distinct().ToList();
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

        public static int StringDifference(string s1, string s2, bool allowTransposedLetters = false)
        {
            if (!allowTransposedLetters)
                return Levenshtein(s1, s2);
            else
                return DamerauLevenshtein(s1, s2);
        }

        private static int Levenshtein(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0)
                return m;

            if (m == 0)
                return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }

            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, 
                                                d[i, j - 1] + 1),
                                                d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        private static int DamerauLevenshtein(string s, string t)
        {
            var bounds = new { Height = s.Length + 1, Width = t.Length + 1 };

            int[,] matrix = new int[bounds.Height, bounds.Width];

            for (int height = 0; height < bounds.Height; height++)
                matrix[height, 0] = height;

            for (int width = 0; width < bounds.Width; width++)
                matrix[0, width] = width;

            for (int height = 1; height < bounds.Height; height++)
            {
                for (int width = 1; width < bounds.Width; width++)
                {
                    int cost = (s[height - 1] == t[width - 1]) ? 0 : 1;
                    int insertion = matrix[height, width - 1] + 1;
                    int deletion = matrix[height - 1, width] + 1;
                    int substitution = matrix[height - 1, width - 1] + cost;

                    int distance = Math.Min(insertion, Math.Min(deletion, substitution));

                    if (height > 1 && width > 1 && s[height - 1] == t[width - 2] && s[height - 2] == t[width - 1])
                    {
                        distance = Math.Min(distance, matrix[height - 2, width - 2] + cost);
                    }

                    matrix[height, width] = distance;
                }
            }

            return matrix[bounds.Height - 1, bounds.Width - 1];
        }
    }

    public static class NumberWordsToNumbers
    {
        #region Number Dictionaries
        private static Dictionary<string, int> singleNumbers = new Dictionary<string, int>
        {
            {"zero", 0 },
            {"one", 1 },
            {"two", 2 },
            {"three", 3 },
            {"four", 4 },
            {"five", 5 },
            {"six", 6 },
            {"seven", 7 },
            {"eight", 8 },
            {"nine", 9 }
        };

        private static Dictionary<string, int> teenNumbers = new Dictionary<string, int>
        {
            {"eleven", 11 },
            {"twelve", 12 },
            {"thirteen", 13 },
            {"fourteen", 14 },
            {"fifteen", 15 },
            {"sixteen", 16 },
            {"seventeen", 17 },
            {"eighteen", 18 },
            {"nineteen", 19 }
        };

        private static Dictionary<string, int> doubleNumbers = new Dictionary<string, int>
        {
            {"ten", 10 },
            {"twenty", 20 },
            {"thirty", 30 },
            {"fourty", 40 },
            {"fifty", 50 },
            {"sixty", 60 },
            {"seventy", 70 },
            {"eighty", 80 },
            {"ninety", 90 }
        };

        private static Dictionary<string, long> multiplierWords = new Dictionary<string, long>
        {
            {"hundred", 100 },
            {"thousand", 1000 },
            {"million", 1000000 },
            {"billion", 1000000000 },
            {"trillion", 1000000000000 }
        };
        #endregion Number Dictionaries

        public static string ConvertString(string input)
        {
            List<Match> numberWords = GetNumberWords(input);

            List<Tuple<string, int, int>> numberWordGroups = CombineNumberWordGroups(numberWords, input);

            for (int i = 0; i < numberWordGroups.Count; i++)
            {
                string filteredNumberWords = numberWordGroups[i].Item1.Replace("-", " ").Replace(",", " ").Replace(" and ", " ");
                numberWordGroups[i] = new Tuple<string, int, int>(ConvertNumberWordsToNumber(filteredNumberWords).ToString(),
                                                                  numberWordGroups[i].Item2,
                                                                  numberWordGroups[i].Item3);
            }

            input = ReplaceNumbersInString(input, numberWordGroups);

            return input;
        }

        private static List<Match> GetNumberWords(string input)
        {
            List<Match> result = new List<Match>();

            string lookBehindChars = @"^|\s|-";
            string lookAheadChars = @",|-|\.|\?|!|\s|$";

            string allNumbersRegex = $"{string.Join("|", multiplierWords.Keys.Select(p => $"(?<={lookBehindChars}){p}(?={lookAheadChars})"))}|" +
                                     $"{string.Join("|", doubleNumbers.Keys.Select(p => $"(?<={lookBehindChars}){p}(?={lookAheadChars})"))}|" +
                                     $"{string.Join("|", teenNumbers.Keys.Select(p => $"(?<={lookBehindChars}){p}(?={lookAheadChars})"))}|" +
                                     $"{string.Join("|", singleNumbers.Keys.Select(p => $"(?<={lookBehindChars}){p}(?={lookAheadChars})"))}|" +
                                     $@"(?<={lookBehindChars})\d+(?={lookAheadChars})";

            foreach (Match match in Regex.Matches(input, allNumbersRegex, RegexOptions.IgnoreCase))
                result.Add(match);

            return result;
        }

        private static List<Tuple<string, int, int>> CombineNumberWordGroups(List<Match> numberWords, string input)
        {
            List<Tuple<string, int, int>> result = new List<Tuple<string, int, int>>();

            for (int i = 0; i < numberWords.Count; i++)
            {
                Match word = numberWords[i];
                if (i > 0)
                {
                    Match lastWord = numberWords[i - 1];
                    string separatingString = input.Substring(lastWord.Index + lastWord.Length, word.Index - lastWord.Index - lastWord.Length);

                    //If words are only separated by "and", " ", or "-" then combine them together into one group
                    if (Regex.Replace(separatingString, " |-|and", "", RegexOptions.IgnoreCase) == "")
                    {
                        result[result.Count - 1] = new Tuple<string, int, int>(result[result.Count - 1].Item1 + separatingString + word.Value,
                                                                                result[result.Count - 1].Item2,
                                                                                result[result.Count - 1].Item3 + separatingString.Length + word.Value.Length);

                        continue;
                    }
                }

                result.Add(new Tuple<string, int, int>(word.Value, word.Index, word.Length));
            }

            return result;
        }

        private static string ConvertNumberWordsToNumber(string numberWords)
        {
            //Need to do number matching in reverse order so we match things like "sixty" before "six"
            foreach (string key in multiplierWords.Keys)
                numberWords = Regex.Replace(numberWords, key, multiplierWords[key].ToString(), RegexOptions.IgnoreCase);

            foreach (string key in doubleNumbers.Keys)
                numberWords = Regex.Replace(numberWords, key, doubleNumbers[key].ToString(), RegexOptions.IgnoreCase);

            foreach (string key in teenNumbers.Keys)
                numberWords = Regex.Replace(numberWords, key, teenNumbers[key].ToString(), RegexOptions.IgnoreCase);

            foreach (string key in singleNumbers.Keys)
                numberWords = Regex.Replace(numberWords, key, singleNumbers[key].ToString(), RegexOptions.IgnoreCase);

            numberWords = CombineMultipliers(numberWords);

            numberWords = CombineNearbyNumbers(numberWords);

            return numberWords;
        }

        private static string CombineMultipliers(string input)
        {
            string result = "";
            int currentNum = int.MinValue;
            foreach (string word in input.Split(' '))
            {
                if (word == "")
                    continue;

                int number;
                if (int.TryParse(word, out number))
                {
                    string numStr = currentNum.ToString();
                    if (currentNum == int.MinValue)
                        currentNum = number;
                    else if (multiplierWords.Values.Contains(number) && currentNum != int.MinValue)
                        currentNum *= number;
                    else
                    {
                        if (currentNum != int.MinValue)
                        {
                            result += currentNum + " ";
                            currentNum = int.MinValue;
                        }

                        currentNum = number;
                    }
                }
                else
                {
                    if (currentNum != double.MinValue)
                    {
                        result += currentNum + " ";
                        currentNum = int.MinValue;
                    }

                    result += word + " ";
                }
            }

            if (currentNum != int.MinValue)
                result += currentNum;

            return result.Trim();
        }

        private static string CombineNearbyNumbers(string input)
        {
            string result = "";
            int currentNum = int.MinValue;
            foreach (string word in input.Split(' '))
            {
                if (word == "")
                    continue;

                int number;
                if (int.TryParse(word, out number))
                {
                    string numStr = currentNum.ToString();
                    if (currentNum == int.MinValue)
                        currentNum = number;
                    else if (numStr.Length > word.Length &&
                             numStr.Substring(numStr.Length - word.Length).Distinct().All(c => c == '0')) //Check if space for new number in currentNum is only zeros
                    {
                        currentNum = currentNum + number;
                    }
                    else
                    {
                        if (currentNum != int.MinValue)
                        {
                            result += currentNum + " ";
                            currentNum = int.MinValue;
                        }

                        currentNum = number;
                    }
                }
                else
                {
                    if (currentNum != int.MinValue)
                    {
                        result += currentNum + " ";
                        currentNum = int.MinValue;
                    }

                    result += word + " ";
                }
            }

            if (currentNum != int.MinValue)
                result += currentNum;

            return result.Trim();
        }

        private static string ReplaceNumbersInString(string originalString, List<Tuple<string, int, int>> replacementNumbersAndPositions)
        {
            int indexDiff = 0; //Account for difference in length values as we replace substrings
            foreach (Tuple<string, int, int> replacementNumber in replacementNumbersAndPositions)
            {
                string temp = originalString.Replace(replacementNumber.Item2 - indexDiff, replacementNumber.Item3, replacementNumber.Item1);
                indexDiff += originalString.Length - temp.Length;
                originalString = temp;
            }

            return originalString;
        }

        private static string Replace(this string inputString, int startIndex, int length, string newSubString)
        {
            StringBuilder aStringBuilder = new StringBuilder(inputString);
            aStringBuilder.Remove(startIndex, length);
            aStringBuilder.Insert(startIndex, newSubString);
            return aStringBuilder.ToString();
        }
    }
}
