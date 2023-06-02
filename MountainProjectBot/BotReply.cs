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

        //TEMP - will be removed later, but added now to make a statement
        public static string PrivatizeReply(string response)
        {
            string[] responseLines = response.Split(new[] { Markdown.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<string> hiddenLines = responseLines.Select(l => Markdown.Spoiler(l));
            response = string.Join(Markdown.NewLine, hiddenLines);

            return Markdown.Bold("The bot's comment has been made private in protest of the upcoming Reddit API changes") + "  " +
                   Markdown.Link("Learn more", "https://reddit.com/r/apolloapp/comments/13ws4w3/had_a_call_with_reddit_to_discuss_pricing_bad") +  Markdown.NewLine + 
                   response + Markdown.NewLine + Markdown.Italic("(if the bot survives through the changes, comments will return to normal after June 19)");
        }

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
            {
                return "I didn't understand what you were looking for. Please use the Feedback button below if you think this is a bug";
            }

            ResultParameters resultParameters = ResultParameters.ParseParameters(ref queryText);
            SearchParameters searchParameters = SearchParameters.ParseParameters(ref queryText);

            SearchResult searchResult = MountainProjectDataSearch.Search(queryText, searchParameters);

            return GetResponse(queryText, searchParameters?.SpecificLocation, searchResult, resultParameters);
        }

        public static string GetReplyForMPLinks(Comment comment)
        {
            List<MPObject> foundMPObjects = new List<MPObject>();
            foreach (string url in ExtractMPLinks(WebUtility.HtmlDecode(comment.Body)))
            {
                MPObject mpObjectWithID = MountainProjectDataSearch.GetItemWithMatchingID(Utilities.GetID(url));
                if (mpObjectWithID != null)
                {
                    foundMPObjects.Add(mpObjectWithID);
                }
            }

            string response = "";
            if (foundMPObjects.Count == 0)
            {
                return null;
            }

            foundMPObjects.ForEach(p => response += GetFormattedString(p, includeUrl: false) + Markdown.HRule);
            response += GetBotLinks(comment);

            return response;
        }

        private static List<string> ExtractMPLinks(string commentBody)
        {
            List<string> result = new List<string>();
            Regex regex = new Regex(@"mountainproject\.com\/(area|route|v)\/\d+");
            foreach (Match match in regex.Matches(commentBody))
            {
                try
                {
                    string mpUrl = $"https://www.{match.Value}";
                    mpUrl = Utilities.GetRedirectURL(mpUrl);
                    if (!result.Contains(mpUrl))
                    {
                        result.Add(mpUrl);
                    }
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
                return $"I could not find anything for \"{queryText}\"{(!string.IsNullOrEmpty(queryLocation) ? $" in {queryLocation}" : "")}. Please use the Feedback button below if you think this is a bug";
            }
            else if (searchResult.AllResults.Count > 1)
            {
                return $"I found the following info (out of {searchResult.AllResults.Count} total results):{Markdown.HRule}{GetFormattedString(searchResult, resultParameters)}";
            }
            else
            {
                return $"I found the following info:{Markdown.HRule}{GetFormattedString(searchResult, resultParameters)}";
            }
        }

        public static string GetFormattedString(MPObject finalResult, ResultParameters parameters = null, bool includeUrl = true)
        {
            return GetFormattedString(new SearchResult(finalResult), parameters, includeUrl);
        }

        public static string GetFormattedString(SearchResult searchResult, ResultParameters parameters = null, bool includeUrl = true)
        {
            if (searchResult.IsEmpty())
            {
                return null;
            }

            string result = "";
            if (searchResult.FilteredResult is Area inputArea)
            {
                Area latestAreaData = new Area { ID = inputArea.ID };
                Parsers.ParseAreaAsync(latestAreaData, false, false).Wait(); //Get most updated data (straight from MountainProject page)
                latestAreaData.PopulateParents();

                if (!string.IsNullOrEmpty(latestAreaData.Statistics.ToString()))
                {
                    result += $"{Markdown.Bold(latestAreaData.Name)} [{latestAreaData.Statistics}]" + Markdown.NewLine;
                }
                else
                {
                    result += $"{Markdown.Bold(latestAreaData.Name)}" + Markdown.NewLine;
                }

                result += GetLocationString(latestAreaData, searchResult.RelatedLocation);
                result += GetPopularRoutes(latestAreaData, parameters);

                if (includeUrl)
                {
                    result += latestAreaData.URL;
                }
            }
            else if (searchResult.FilteredResult is Route inputRoute)
            {
                Route latestRouteData = new Route { ID = inputRoute.ID };
                Parsers.ParseRouteAsync(latestRouteData, false).Wait(); //Get most updated data (straight from MountainProject page)
                latestRouteData.PopulateParents();

                if (latestRouteData.IsNameRedacted)
                {
                    result += $"{Markdown.Bold("[REDACTED]")} {Markdown.Spoiler($"({latestRouteData.Name})")}";
                }
                else
                {
                    result += Markdown.Bold(latestRouteData.Name);
                }
                
                result += $" {GetRouteAdditionalInfo(latestRouteData, parameters, showGrade: false, showHeight: false)}";

                result += Markdown.NewLine;

                result += $"Type: {string.Join(", ", latestRouteData.Types)}" + Markdown.NewLine;
                result += $"Grade: {GetRouteGrades(latestRouteData, parameters, true)}" + Markdown.NewLine;

                if (latestRouteData.Height != null && latestRouteData.Height.Value != 0)
                {
                    result += $"Height: {Math.Round(latestRouteData.Height.GetValue(Dimension.Units.Feet), 1)} ft/" +
                              $"{Math.Round(latestRouteData.Height.GetValue(Dimension.Units.Meters), 1)} m" +
                              Markdown.NewLine;
                }

                result += $"Rating: {latestRouteData.Rating}/4" + Markdown.NewLine;
                result += GetLocationString(latestRouteData, searchResult.RelatedLocation);

                if (includeUrl)
                {
                    result += latestRouteData.URL;
                }

                //TEMP
                result += Markdown.NewLine + Markdown.Italic("[Trying out a new grade display - let me know your thoughts]");
            }

            return result;
        }

        public static string GetRouteGrades(Route route, ResultParameters parameters, bool withAbbreviations = false)
        {
            if (parameters != null)
            {
                return route.GetRouteGrade(parameters.GradeSystem).ToString();
            }
            else
            {
                List<Grade> grades = new List<Grade>();

                //Add "American" grades
                grades.AddRange(route.Grades.Where(g => g.System == GradeSystem.YDS || 
                                                        g.System == GradeSystem.Hueco));

                //Add ice grade (if necessary)
                Grade iceGrade = route.Grades.Find(g => g.System == GradeSystem.Ice);
                if ((route.Types.Contains(Route.RouteType.Ice) || route.Types.Contains(Route.RouteType.Snow) || route.Types.Contains(Route.RouteType.Alpine)) &&
                    iceGrade != null)
                {
                    grades.Add(iceGrade);
                }

                //Add international grades
                grades.AddRange(route.Grades.Where(g => g.System == GradeSystem.French || 
                                                        g.System == GradeSystem.UIAA ||
                                                        g.System == GradeSystem.Fontainebleau ||
                                                        g.System == GradeSystem.Ewbanks));

                if (grades.Count > 0)
                {
                    //Convert grades to strings, but "inject" Aid grade if it exists
                    Grade aidGrade = route.Grades.Find(g => g.System == GradeSystem.Aid);
                    List<string> gradeStrings = grades.Select(g =>
                    {
                        string abbreviationText = g.Abbreviation != null && withAbbreviations ? Markdown.DoubleSuperscript(g.Abbreviation) : "";

                        if (g.System == GradeSystem.YDS && aidGrade != null)
                        {
                            return $"{g.ToString(false)}{abbreviationText} ({aidGrade.ToString(false)})";
                        }
                        else
                        {
                            return g.ToString(false) + abbreviationText;
                        }
                    }).ToList();

                    return string.Join(" | ", gradeStrings);
                }
                else if (route.Grades.Count == 1)
                {
                    Grade grade = route.Grades.First();
                    string abbreviationText = grade.Abbreviation != null && withAbbreviations ? Markdown.DoubleSuperscript(grade.Abbreviation) : "";

                    return grade.ToString(false) + abbreviationText;
                }
                else
                {
                    return "";
                }
            }
        }

        public static string GetLocationString(MPObject child, Area referenceLocation = null)
        {
            MPObject innerParent = MountainProjectDataSearch.GetInnerParent(child);
            MPObject outerParent = MountainProjectDataSearch.GetOuterParent(child);

            if (referenceLocation != null) //Override the "innerParent" in situations where we want the location string to include the "insisted" location
            {
                //Only override if the location is not already present
                if (innerParent?.URL != referenceLocation.URL &&
                    outerParent?.URL != referenceLocation.URL)
                {
                    innerParent = referenceLocation;
                }
            }

            if (innerParent == null)
            {
                return "";
            }

            string locationString = $"Located in {Markdown.Link(innerParent.Name, innerParent.URL)}";
            if (outerParent != null && outerParent.URL != innerParent.URL)
            {
                locationString += $", {Markdown.Link(outerParent.Name, outerParent.URL)}";
            }

            locationString += Markdown.NewLine;

            return locationString;
        }

        private static string GetPopularRoutes(Area area, ResultParameters parameters)
        {
            string result = "";

            List<Route> popularRoutes = new List<Route>();
            if (area.PopularRouteIDs.Count == 0) //MountainProject doesn't list any popular routes. Figure out some ourselves
            {
                popularRoutes = area.GetPopularRoutes(3);
            }
            else
            {
                area.PopularRouteIDs.ForEach(id => popularRoutes.Add(MountainProjectDataSearch.GetItemWithMatchingID(id, MountainProjectDataSearch.DestAreas) as Route));
            }

            popularRoutes.RemoveAll(r => r == null);

            foreach (Route popularRoute in popularRoutes)
            {
                result += $"\n- {Markdown.Link(popularRoute.Name, popularRoute.URL)} {GetRouteAdditionalInfo(popularRoute, parameters)}";
            }

            if (string.IsNullOrEmpty(result))
            {
                return "";
            }

            return "Popular routes:" + Markdown.NewLine + result + Markdown.NewLine;
        }

        private static string GetRouteAdditionalInfo(Route route, ResultParameters parameters, bool showGrade = true, bool showHeight = true)
        {
            List<string> parts = new List<string>();

            if (showGrade)
            {
                parts.Add(GetRouteGrades(route, parameters).ToString());
            }

            if (showHeight && route.Height != null && route.Height.Value != 0)
            {
                parts.Add($"{Math.Round(route.Height.GetValue(Dimension.Units.Feet), 1)} ft/" +
                          $"{Math.Round(route.Height.GetValue(Dimension.Units.Meters), 1)} m");
            }

            if (!string.IsNullOrEmpty(route.AdditionalInfo))
            {
                parts.Add(route.AdditionalInfo);
            }

            if (parts.Count > 0)
            {
                return $"[{string.Join(", ", parts)}]";
            }
            else
            {
                return "";
            }
        }

        public static string GetBotLinks(VotableThing relatedThing = null)
        {
            List<string> botLinks = new List<string>();

            if (relatedThing != null)
            {
                string encodedLink = WebUtility.HtmlEncode(RedditHelper.GetFullLink(relatedThing.Permalink));
                botLinks.Add(Markdown.Link("Feedback", "https://docs.google.com/forms/d/e/1FAIpQLSchgbXwXMylhtbA8kXFycZenSKpCMZjmYWMZcqREl_OlCm4Ew/viewform?usp=pp_url&entry.266808192=" + encodedLink));
            }
            else
            {
                botLinks.Add(Markdown.Link("Feedback", "https://docs.google.com/forms/d/e/1FAIpQLSchgbXwXMylhtbA8kXFycZenSKpCMZjmYWMZcqREl_OlCm4Ew/viewform?usp=pp_url"));
            }

            botLinks.Add(Markdown.Link("FAQ", "https://github.com/derekantrican/MountainProject/wiki/Bot-FAQ"));
            botLinks.Add(Markdown.Link("Syntax", "https://github.com/derekantrican/MountainProject/wiki/Bot-Syntax"));
            botLinks.Add(Markdown.Link("GitHub", "https://github.com/derekantrican/MountainProject"));
            botLinks.Add(Markdown.Link("Donate", "https://www.paypal.me/derekantrican"));

            return string.Join(" | ", botLinks);
        }
    }
}
