using MountainProjectAPI;
using RedditSharp.Things;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace MountainProjectBot
{
    public static class BotReply
    {
        const string BOTKEYWORDREGEX = @"(?i)!mountain\s*project(.*)";

        public static string GetReplyForCommentBody(string commentBody)
        {
            //Get everything AFTER the keyword, but on the same line
            string queryText = Regex.Match(commentBody, BOTKEYWORDREGEX).Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(queryText))
                return "I didn't understand what you were looking for. Please use the Feedback button below if you think this is a bug";

            ResultParameters resultParameters = ResultParameters.ParseParameters(ref queryText);
            SearchParameters searchParameters = SearchParameters.ParseParameters(ref queryText);

            List<MPObject> searchResults = MountainProjectDataSearch.SearchMountainProject(queryText, searchParameters);
            MPObject filteredResult = MountainProjectDataSearch.FilterByPopularity(searchResults);
            string replyText = GetFormattedString(filteredResult, resultParameters);
            if (string.IsNullOrEmpty(replyText))
            {
                if (searchParameters != null && !string.IsNullOrEmpty(searchParameters.SpecificLocation))
                    replyText = $"I could not find anything for \"{queryText}\" in \"{searchParameters.SpecificLocation}\". Please use the Feedback button below if you think this is a bug";
                else
                    replyText = $"I could not find anything for \"{queryText}\". Please use the Feedback button below if you think this is a bug";
            }

            replyText = $"I found the following info (out of {searchResults.Count} total results):" + Markdown.NewLine + replyText;

            return replyText;
        }

        public static string GetFormattedString(MPObject finalResult, ResultParameters parameters = null)
        {
            if (finalResult == null)
                return null;

            string result = "";
            if (finalResult is Area)
            {
                Area inputArea = finalResult as Area;
                result += $"{Markdown.Bold(inputArea.Name)} [{inputArea.Statistics}]" + Markdown.NewLine;
                result += GetLocationString(inputArea);
                result += GetPopularRoutes(inputArea, parameters);

                result += inputArea.URL;
            }
            else if (finalResult is Route)
            {
                Route inputRoute = finalResult as Route;
                result += $"{Markdown.Bold(inputRoute.Name)}";
                if (!string.IsNullOrEmpty(inputRoute.AdditionalInfo))
                    result += $" [{inputRoute.AdditionalInfo}]";

                result += Markdown.NewLine;

                result += $"Type: {string.Join(", ", inputRoute.Types)}" + Markdown.NewLine;
                result += $"Grade: {inputRoute.GetRouteGrade(parameters)}" + Markdown.NewLine;
                result += $"Rating: {inputRoute.Rating}/4" + Markdown.NewLine;
                result += GetLocationString(inputRoute);

                result += inputRoute.URL;
            }

            return result;
        }

        public static string GetLocationString(MPObject child)
        {
            MPObject innerParent, outerParent;
            innerParent = null;
            if (child is Route)
                innerParent = MountainProjectDataSearch.GetParent(child, -2); //Get the "second to last" parent https://github.com/derekantrican/MountainProject/issues/12
            else if (child is Area)
                innerParent = MountainProjectDataSearch.GetParent(child, -1); //Get immediate parent

            if (innerParent == null ||  //If "child" is a dest area, the parent will be "All Locations" which won't be in our directory
                innerParent.URL == Utilities.INTERNATIONALURL) //If "child" is an area like "Europe"
                return "";

            outerParent = MountainProjectDataSearch.GetParent(child, 1); //Get state that route/area is in
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

            string locationString = $"Located in {Markdown.Link(innerParent.Name, innerParent.URL)}";
            if (outerParent != null && outerParent.URL != innerParent.URL)
                locationString += $", {Markdown.Link(outerParent.Name, outerParent.URL)}";

            locationString += Markdown.NewLine;

            return locationString;
        }

        private static string GetPopularRoutes(Area area, ResultParameters parameters)
        {
            string result = "Popular routes:\n";

            if (area.PopularRouteUrls.Count == 0)
                return "";

            foreach (string url in area.PopularRouteUrls)
            {
                List<MPObject> itemsToSearch = new List<MPObject>();
                itemsToSearch.AddRange(area.SubAreas);
                itemsToSearch.AddRange(area.Routes);

                Route popularRoute = MountainProjectDataSearch.GetItemWithMatchingUrl(url, itemsToSearch) as Route;
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
            string botLinks = "";

            if (relatedComment != null)
            {
                string commentLink = WebUtility.HtmlEncode("https://reddit.com" + relatedComment.Permalink);
                botLinks += Markdown.Link("Feedback", "https://docs.google.com/forms/d/e/1FAIpQLSchgbXwXMylhtbA8kXFycZenSKpCMZjmYWMZcqREl_OlCm4Ew/viewform?usp=pp_url&entry.266808192=" + commentLink) + " | ";
            }
            else
                botLinks += Markdown.Link("Feedback", "https://docs.google.com/forms/d/e/1FAIpQLSchgbXwXMylhtbA8kXFycZenSKpCMZjmYWMZcqREl_OlCm4Ew/viewform?usp=pp_url") + " | ";

            botLinks += Markdown.Link("FAQ", "https://github.com/derekantrican/MountainProject/wiki/Bot-FAQ") + " | ";
            botLinks += Markdown.Link("Operators", "https://github.com/derekantrican/MountainProject/wiki/Bot-%22Operators%22") + " | ";
            botLinks += Markdown.Link("GitHub", "https://github.com/derekantrican/MountainProject") + " | ";
            botLinks += Markdown.Link("Donate", "https://www.paypal.me/derekantrican");

            return botLinks;
        }
    }
}
