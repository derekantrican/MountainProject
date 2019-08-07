using MountainProjectAPI;
using RedditSharp.Things;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace MountainProjectBot
{
    public static class BotReply
    {
        const string BOTKEYWORDREGEX = @"(?i)!mountain\s*project(.*)";

        public static string GetReplyForRequest(Comment comment)
        {
            string response = GetReplyForRequest(comment.Body);
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
            foreach (string url in ExtractMPLinks(comment.Body))
            {
                MPObject mpObjectWithUrl = MountainProjectDataSearch.GetItemWithMatchingUrl(url, MountainProjectDataSearch.DestAreas.Cast<MPObject>().ToList());
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
            return GetFormattedString(new SearchResult() { FilteredResult = finalResult }, parameters, includeUrl);
        }

        public static string GetFormattedString(SearchResult searchResult, ResultParameters parameters = null, bool includeUrl = true)
        {
            if (searchResult.IsEmpty())
                return null;

            string result = "";
            if (searchResult.FilteredResult is Area)
            {
                Area inputArea = searchResult.FilteredResult as Area;
                result += $"{Markdown.Bold(inputArea.Name)} [{inputArea.Statistics}]" + Markdown.NewLine;
                result += GetLocationString(inputArea, searchResult.RelatedLocation);
                result += GetPopularRoutes(inputArea, parameters);

                if (includeUrl)
                    result += inputArea.URL;
            }
            else if (searchResult.FilteredResult is Route)
            {
                Route inputRoute = searchResult.FilteredResult as Route;
                result += $"{Markdown.Bold(inputRoute.Name)}";
                if (!string.IsNullOrEmpty(inputRoute.AdditionalInfo))
                    result += $" [{inputRoute.AdditionalInfo}]";

                result += Markdown.NewLine;

                result += $"Type: {string.Join(", ", inputRoute.Types)}" + Markdown.NewLine;
                result += $"Grade: {inputRoute.GetRouteGrade(parameters)}" + Markdown.NewLine;
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
                innerParent = referenceLocation;

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
            {
                result += $"\n- {Markdown.Link(popularRoute.Name, popularRoute.URL)} [{popularRoute.GetRouteGrade(parameters)}";
                if (!string.IsNullOrEmpty(popularRoute.AdditionalInfo))
                    result += $", {popularRoute.AdditionalInfo}";

                result += "]";
            }

            result += Markdown.NewLine;

            return result;
        }

        public static string GetBotLinks(Comment relatedComment = null)
        {
            List<string> botLinks = new List<string>();

            if (relatedComment != null)
            {
                string commentLink = WebUtility.HtmlEncode(RedditHelper.GetFullLink(relatedComment.Permalink));
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
    }
}
