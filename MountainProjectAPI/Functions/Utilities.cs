using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        public const string MPROUTEURL = "https://www.mountainproject.com/route";
        public const string MPAREAURL = "https://www.mountainproject.com/area";
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
            { "105720495", @"\bJTree\b" }, //Joshua Tree
            { "105946429", @"\bBC\b" }, //British Columbia
            { "106007071", @"\bUK\b" }, //United Kingdom
            { "105843226", @"\bORG\b" }, //Owens River Gorge
            { "105903004", @"\bHCR\b" }, //Horseshoe Canyon Ranch
        };

        public static string GetHtml(string url)
        {
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
                        {
                            Console.WriteLine($"Retries failed when trying to get HTML from {url}");
                            throw;
                        }
                    }
                }
            }

            return html;
        }

        public static async Task<string> GetHtmlAsync(string url)
        {
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
                        {
                            Console.WriteLine($"Retries failed when trying to get HTML from {url}");
                            throw;
                        }
                    }
                }
            }

            return html;
        }

        static readonly HtmlParser parser = new HtmlParser();
        public static IHtmlDocument GetHtmlDoc(string url)
        {
            return parser.ParseDocument(GetHtml(url));
        }

        public static async Task<IHtmlDocument> GetHtmlDocAsync(string url)
        {
            string html = await GetHtmlAsync(url);
            return await parser.ParseDocumentAsync(html);
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
            return mpURL?.Replace($"{MPROUTEURL}/", "").Replace($"{MPAREAURL}/", "").Split('/')[0];
        }

        public static string GetSimpleURL(string mpUrl)
        {
            return Regex.Match(mpUrl, $@"{MPBASEURL}/(route|area)/\d+").Value;
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
