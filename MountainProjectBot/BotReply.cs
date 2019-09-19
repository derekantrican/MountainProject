using MountainProjectAPI;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using static MountainProjectAPI.Grade;

namespace MountainProjectBot
{
    public static class BotReply
    {
        const string BOTKEYWORDREGEX = @"(?i)!mountain\s*project(.*)";

        public static string GetReplyForRequest(Comment comment)
        {
            string response = GetReplyForRequest(WebUtility.HtmlDecode(comment.Body));
            response += Markdown.HRule;
            response += GetBotLinks(comment);

            return response;
        }

        public static string GetReplyForRequest(string commentBody)
        {
            //Get everything AFTER the keyword, but on the same line
            string queryText = Regex.Match(commentBody, BOTKEYWORDREGEX).Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(queryText))
                return "I didn't understand what you were looking for. Please use the Feedback button below if you think this is a bug";

            ResultParameters resultParameters = ResultParameters.ParseParameters(ref queryText);
            SearchParameters searchParameters = SearchParameters.ParseParameters(ref queryText);

            SearchResult searchResult = MountainProjectDataSearch.Search(queryText, searchParameters);

            return GetResponse(queryText, searchParameters.SpecificLocation, searchResult, resultParameters);
        }

        public static string GetReplyForMPLinks(Comment comment)
        {
            List<MPObject> foundMPObjects = new List<MPObject>();
            foreach (string url in ExtractMPLinks(WebUtility.HtmlDecode(comment.Body)))
            {
                MPObject mpObjectWithUrl = MountainProjectDataSearch.GetItemWithMatchingUrl(url);
                if (mpObjectWithUrl != null)
                    foundMPObjects.Add(mpObjectWithUrl);
            }

            string response = "";
            if (foundMPObjects.Count == 0)
                return null;

            foundMPObjects.ForEach(p => response += GetFormattedString(p, includeUrl: false) + Markdown.HRule);
            response += GetBotLinks(comment);

            return response;
        }

        private static List<string> ExtractMPLinks(string commentBody)
        {
            List<string> result = new List<string>();
            Regex regex = new Regex(@"(https:\/\/)?(www.)?mountainproject\.com.*?(?=\)|\s|]|$)");
            foreach (Match match in regex.Matches(commentBody))
            {
                string mpUrl = match.Value;
                if (!mpUrl.Contains("www."))
                    mpUrl = "www." + mpUrl;

                if (!mpUrl.Contains("https://"))
                    mpUrl = "https://" + mpUrl;

                try
                {
                    mpUrl = Utilities.GetRedirectURL(mpUrl);
                    if (!result.Contains(mpUrl))
                        result.Add(mpUrl);
                }
                catch //Something went wrong. We'll assume that it was because the url didn't match anything
                {
                    continue;
                }
            }

            return result;
        }

        private static string GetResponse(string queryText, string queryLocation, SearchResult searchResult, ResultParameters resultParameters)
        {
            if (searchResult.IsEmpty())
            {
                if (!string.IsNullOrEmpty(queryLocation))
                    return $"I could not find anything for \"{queryText}\" in \"{queryLocation}\". Please use the Feedback button below if you think this is a bug";
                else
                    return $"I could not find anything for \"{queryText}\". Please use the Feedback button below if you think this is a bug";
            }
            else if (searchResult.AllResults.Count > 1)
                return $"I found the following info (out of {searchResult.AllResults.Count} total results):" + Markdown.HRule + GetFormattedString(searchResult, resultParameters);
            else
                return $"I found the following info:" + Markdown.HRule + GetFormattedString(searchResult, resultParameters);
        }

        public static string GetFormattedString(MPObject finalResult, ResultParameters parameters = null, bool includeUrl = true)
        {
            return GetFormattedString(new SearchResult(finalResult), parameters, includeUrl);
        }

        public static string GetFormattedString(SearchResult searchResult, ResultParameters parameters = null, bool includeUrl = true)
        {
            if (searchResult.IsEmpty())
                return null;

            string result = "";
            if (searchResult.FilteredResult is Area)
            {
                Area inputArea = searchResult.FilteredResult as Area;
                if (!string.IsNullOrEmpty(inputArea.Statistics.ToString()))
                    result += $"{Markdown.Bold(inputArea.Name)} [{inputArea.Statistics}]" + Markdown.NewLine;
                else
                    result += $"{Markdown.Bold(inputArea.Name)}" + Markdown.NewLine;

                result += $"{Markdown.Bold(inputArea.Name)} [{inputArea.Statistics}]" + Markdown.NewLine;
                result += GetLocationString(inputArea, searchResult.RelatedLocation);
                result += GetPopularRoutes(inputArea, parameters);

                if (includeUrl)
                    result += inputArea.URL;
            }
            else if (searchResult.FilteredResult is Route)
            {
                Route inputRoute = searchResult.FilteredResult as Route;
                result += $"{Markdown.Bold(inputRoute.Name)} {GetRouteAdditionalInfo(inputRoute, parameters, showGrade: false, showHeight: false)}";

                result += Markdown.NewLine;

                result += $"Type: {string.Join(", ", inputRoute.Types)}" + Markdown.NewLine;
                result += $"Grade: {inputRoute.GetRouteGrade(parameters).ToString()}" + Markdown.NewLine;

                if (inputRoute.Height != null && inputRoute.Height.Value != 0)
                {
                    result += $"Height: {Math.Round(inputRoute.Height.GetValue(Dimension.Units.Feet), 1)} ft/" +
                              $"{Math.Round(inputRoute.Height.GetValue(Dimension.Units.Meters), 1)} m" +
                              Markdown.NewLine;
                }

                result += $"Rating: {inputRoute.Rating}/4" + Markdown.NewLine;
                result += GetLocationString(inputRoute, searchResult.RelatedLocation);

                if (includeUrl)
                    result += inputRoute.URL;
            }

            return result;
        }

        public static string GetLocationString(MPObject child, Area referenceLocation = null)
        {
            MPObject innerParent, outerParent;
            innerParent = null;
            outerParent = MountainProjectDataSearch.GetParent(child, 1); //Get state that route/area is in
            if (child is Route)
            {
                innerParent = MountainProjectDataSearch.GetParent(child, -2); //Get the "second to last" parent https://github.com/derekantrican/MountainProject/issues/12

                if (innerParent.URL == outerParent.URL)
                    innerParent = MountainProjectDataSearch.GetParent(child, -1);
            }
            else if (child is Area)
                innerParent = MountainProjectDataSearch.GetParent(child, -1); //Get immediate parent

            if (innerParent == null ||  //If "child" is a dest area, the parent will be "All Locations" which won't be in our directory
                innerParent.URL == Utilities.INTERNATIONALURL) //If "child" is an area like "Europe"
                return "";

            if (outerParent.URL == Utilities.INTERNATIONALURL) //If this is international, get the country instead of the state (eg "China")
            {
                if (child.ParentUrls.Count > 3)
                {
                    if (child.ParentUrls.Contains(Utilities.AUSTRALIAURL)) //Australia is both a continent and a country so it is an exception
                        outerParent = MountainProjectDataSearch.GetParent(child, 2);
                    else
                        outerParent = MountainProjectDataSearch.GetParent(child, 3);
                }
                else
                    return ""; //Return a blank string if we are in an area like "China" (so we don't return a string like "China is located in Asia")
            }

            if (referenceLocation != null) //Override the "innerParent" in situations where we want the location string to include the "insisted" location
            {
                //Only override if the location is not already present
                if (innerParent.URL != referenceLocation.URL &&
                    outerParent.URL != referenceLocation.URL)
                {
                    innerParent = referenceLocation;
                }
            }

            string locationString = $"Located in {Markdown.Link(innerParent.Name, innerParent.URL)}";
            if (outerParent != null && outerParent.URL != innerParent.URL)
                locationString += $", {Markdown.Link(outerParent.Name, outerParent.URL)}";

            locationString += Markdown.NewLine;

            return locationString;
        }

        private static string GetPopularRoutes(Area area, ResultParameters parameters)
        {
            string result = "Popular routes:\n";

            List<Route> popularRoutes = new List<Route>();
            if (area.PopularRouteUrls.Count == 0) //MountainProject doesn't list any popular routes. Figure out some ourselves
                popularRoutes = area.GetPopularRoutes(3);
            else
            {
                List<MPObject> itemsToSearch = new List<MPObject>();
                itemsToSearch.AddRange(area.SubAreas);
                itemsToSearch.AddRange(area.Routes);

                area.PopularRouteUrls.ForEach(p => popularRoutes.Add(MountainProjectDataSearch.GetItemWithMatchingUrl(p, itemsToSearch) as Route));
            }

            foreach (Route popularRoute in popularRoutes)
                result += $"\n- {Markdown.Link(popularRoute.Name, popularRoute.URL)} {GetRouteAdditionalInfo(popularRoute, parameters)}";

            if (string.IsNullOrEmpty(result))
                return "";

            result += Markdown.NewLine;

            return result;
        }

        private static string GetRouteAdditionalInfo(Route route, ResultParameters parameters, bool showGrade = true, bool showHeight = true)
        {
            List<string> parts = new List<string>();

            if (showGrade)
                parts.Add(route.GetRouteGrade(parameters).ToString());

            if (showHeight && route.Height != null && route.Height.Value != 0)
            {
                parts.Add($"{Math.Round(route.Height.GetValue(Dimension.Units.Feet), 1)} ft/" +
                          $"{Math.Round(route.Height.GetValue(Dimension.Units.Meters), 1)} m");
            }

            if (!string.IsNullOrEmpty(route.AdditionalInfo))
                parts.Add(route.AdditionalInfo);

            if (parts.Count > 0)
                return $"[{string.Join(", ", parts)}]";
            else
                return "";
        }

        public static string GetBotLinks(VotableThing relatedThing = null)
        {
            return GetBotLinks(relatedThing.Permalink);
        }

        private static string GetBotLinks(Uri relatedPermalink = null)
        {
            List<string> botLinks = new List<string>();

            if (relatedPermalink != null)
            {
                string commentLink = WebUtility.HtmlEncode(RedditHelper.GetFullLink(relatedPermalink));
                botLinks.Add(Markdown.Link("Feedback", "https://docs.google.com/forms/d/e/1FAIpQLSchgbXwXMylhtbA8kXFycZenSKpCMZjmYWMZcqREl_OlCm4Ew/viewform?usp=pp_url&entry.266808192=" + commentLink));
            }
            else
                botLinks.Add(Markdown.Link("Feedback", "https://docs.google.com/forms/d/e/1FAIpQLSchgbXwXMylhtbA8kXFycZenSKpCMZjmYWMZcqREl_OlCm4Ew/viewform?usp=pp_url"));

            botLinks.Add(Markdown.Link("FAQ", "https://github.com/derekantrican/MountainProject/wiki/Bot-FAQ"));
            botLinks.Add(Markdown.Link("Syntax", "https://github.com/derekantrican/MountainProject/wiki/Bot-Syntax"));
            botLinks.Add(Markdown.Link("Grade Conversion", "https://www.mountainproject.com/international-climbing-grades"));
            botLinks.Add(Markdown.Link("GitHub", "https://github.com/derekantrican/MountainProject"));
            botLinks.Add(Markdown.Link("Donate", "https://www.paypal.me/derekantrican"));

            return string.Join(" | ", botLinks);
        }

        #region Post Title Parsing
        public static Route ParsePostTitleToRoute(string postTitle) //Todo: create a unit test for this method using the 1000 post titles
        {
            Route finalResult = null; //Todo: in the future support returning multiple routes (but only if there are multiple grades in the title? Maybe only if all of the routes' full names are in the title?)

            List<Grade> postGrades = Grade.ParseString(postTitle);
            if (postGrades.Count > 0)
            {
                Console.WriteLine($"    Recognized grade(s): {string.Join(" | ", postGrades)}");
                List<Route> possibleResults = new List<Route>();
                List<string> possibleRouteNames = GetPossibleRouteNames(postTitle);
                Console.WriteLine($"    Recognized name(s): {string.Join(" | ", possibleRouteNames)}");
                foreach (string possibleRouteName in possibleRouteNames)
                {
                    SearchResult searchResult = MountainProjectDataSearch.Search(possibleRouteName, new SearchParameters() { OnlyRoutes = true });
                    if (!searchResult.IsEmpty() && searchResult.AllResults.Count < 75) //If the number of matching results is greater than 75, it was probably a very generic word for a search (eg "There") //Todo: once I unit test this, experiment with dropping this value to 100
                    {
                        foreach (Route route in searchResult.AllResults.Cast<Route>())
                        {
                            if (route.Grades.Any(g => postGrades.Any(p => g.Equals(p, true, true))))
                                possibleResults.Add(route);
                        }
                    }
                }

                if (possibleResults.Count > 0)
                {
                    //Todo: maybe we should prioritize "route full name in the title" or "location in the title" before we discard generic matches?

                    //Prioritize routes where the full name is in the post title
                    //(Additionally, we could also prioritize how close - within the post title - the name is to the rating)
                    List<Route> filteredResults = possibleResults.Where(p => Utilities.StringsMatch(Utilities.FilterStringForMatch(p.Name), Utilities.FilterStringForMatch(postTitle))).ToList();

                    if (filteredResults.Count == 1)
                        finalResult = filteredResults.First();
                    else if (filteredResults.Count > 1)
                    {
                        //Prioritize routes where one of the parents (locations) is also in the post title
                        List<Route> routesWithMatchingLocations = filteredResults.Where(r => r.Parents.Any(p => postTitle.ToLower().Contains(p.Name.ToLower()))).ToList();
                        if (routesWithMatchingLocations.Count > 0)
                            filteredResults = routesWithMatchingLocations;
                    }
                    else
                    {
                        //Prioritize routes where one of the parents (locations) is also in the post title
                        List<Route> routesWithMatchingLocations = possibleResults.Where(r => r.Parents.Any(p => postTitle.ToLower().Contains(p.Name.ToLower()))).ToList();
                        if (routesWithMatchingLocations.Count > 0)
                            filteredResults = routesWithMatchingLocations;
                    }

                    if (filteredResults.Count == 1)
                        finalResult = filteredResults.First();
                    else if (filteredResults.Count > 1)
                        finalResult = filteredResults.OrderByDescending(p => p.Popularity).First();
                    else
                        finalResult = possibleResults.OrderByDescending(p => p.Popularity).First();
                }
            }

            return finalResult;
        }

        public static List<string> GetPossibleRouteNames(string postTitle)
        {
            //Todo: this method should also filter out locations as possible route names (of the form ", POSSIBLENAME"). 
            //For instance, in the postTitle "Evening session on Dasani 6-. Morrison, Colorado" Colorado should not be 
            //a possible name. This may be difficult because there could also be "My first 12, Jade" (maybe that's ok, though).
            //Maybe in a series of "POSSIBLE_NAME, POSSIBLE_NAME, POSSIBLE_NAME" (possible names separated by commas and optional
            //spaces) we only take the first one in the list. For instance, in this:
            //"Sex Cave Sector of the Daliwood Boulders, in Dali, Yunnan Province, China" we should only consider
            //"Sex Cave Sector of the Daliwood Boulders" (and that even below will get broken into "Sex Cave Sector" and
            //"Daliwood Boulders" for alternate matches)

            List<string> result = new List<string>();

            Regex routeGradeRegex = new Regex(@"((5\.)\d+[+-]?[a-dA-D]?([\/\\\-][a-dA-D])?)|([vV]\d+([\/\\\-]\d+)?)");
            postTitle = routeGradeRegex.Replace(postTitle, "");

            string[] connectingWords = new string[] { "and", "the", "to", "or", "a", "an", "was", "but" };
            string[] locationWords = new string[] { "of", "on", "at", "in" };
            connectingWords = connectingWords.Concat(locationWords).ToArray();

            string possibleRouteName = "";
            foreach (string word in Regex.Split(postTitle, @"[^\p{L}0-9'’]"))
            {
                if ((!string.IsNullOrWhiteSpace(word) && char.IsUpper(word[0]) && word.Length > 1) ||
                    (connectingWords.Contains(word.ToLower()) && !string.IsNullOrWhiteSpace(possibleRouteName)) || Utilities.IsNumber(word))
                    possibleRouteName += word + " ";
                else if (!string.IsNullOrWhiteSpace(possibleRouteName))
                {
                    possibleRouteName = possibleRouteName.Trim();
                    result.Add(possibleRouteName);

                    //If there is a "location word" in the possibleRouteName, add the separate parts to the result as well
                    Regex locationWordsRegex = new Regex(@"(\s+" + string.Join(@"\s+)|(\s+", locationWords) + @"\s+)");
                    if (locationWordsRegex.IsMatch(possibleRouteName))
                        locationWordsRegex.Split(possibleRouteName).ToList().ForEach(p => result.Add(Utilities.TrimWords(p, connectingWords).Trim()));

                    possibleRouteName = "";
                }
            }

            //Add any "remaining" names
            if (!string.IsNullOrWhiteSpace(possibleRouteName) && possibleRouteName != "V")
                result.Add(possibleRouteName.Trim());

            result = result.Distinct().ToList();
            result.RemoveAll(p => p.Length < 3); //Remove any short "names" (eg "My")
            result.RemoveAll(p => connectingWords.Contains(p.ToLower())); //Remove any "possible names" that are only a connecting word (eg "the")
            result.RemoveAll(p => Utilities.IsNumber(p));

            //Todo: experiment with using all "word combinations". IE once we have parsed our list of "possibleRouteNames", expand the list to
            //be all combinations of those words (respecting order and not "cross-contaminating groups"). EG "Climbed Matthes Crest"
            //possibleRouteName should be expanded to "Climbed Matthes Crest", "Climbed", "Matthes", "Crest", "Climbed Matthes", "Matthes Crest".
            //See if this actually improves results or hurts it. (After this, we should probably run the "RemoveAll" lines again and remove the 
            //"locationWordsRegex.Split" section above)

            return result;
        }
        #endregion Post Title Parsing
    }
}
