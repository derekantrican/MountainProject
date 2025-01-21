using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using static MountainProjectAPI.Grade;
using static MountainProjectAPI.Route;

namespace MountainProjectAPI
{
    public static class Parsers
    {
        public static int TotalAreas = 0;
        public static int TotalRoutes = 0;
        public static int TargetTotalRoutes = 0;
        public static Stopwatch TotalTimer;
        public static ConcurrentDictionary<DateTime, string> Info = new ConcurrentDictionary<DateTime, string>();

        public static double Progress
        {
            get { return (double)TotalRoutes / TargetTotalRoutes; }
        }

        public static int GetTargetTotalRoutes()
        {
            IHtmlDocument doc = Utilities.GetHtmlDoc(Utilities.ALLLOCATIONSURL);
            IElement element = doc.GetElementsByTagName("h2").FirstOrDefault(x => x.TextContent.Contains("Climbing Directory"));

            return int.Parse(Regex.Match(element.TextContent.Replace(",", ""), @"\d+").Value);
        }

        public static List<Area> GetDestAreas(bool populateNames = false)
        {
            List<Area> destAreas = new List<Area>();
            using (IHtmlDocument doc = Utilities.GetHtmlDoc(Utilities.ALLLOCATIONSURL))
            {
                List<IElement> destAreaNodes = doc.GetElementsByTagName("a").Where(x => x.Attributes["href"] != null &&
                                                                                            Utilities.MatchesStateUrlRegex(x.Attributes["href"].Value)).ToList();
                destAreaNodes = (from s in destAreaNodes
                                 orderby s.TextContent
                                 group s by s.Attributes["href"].Value into g
                                 select g.First()).ToList();

                //Move international to the end
                IElement internationalArea = destAreaNodes.Find(p => p.TextContent == "International");
                destAreaNodes.Remove(internationalArea);
                destAreaNodes.Add(internationalArea);

                //Convert to DestArea objects
                foreach (IElement destAreaElement in destAreaNodes)
                {
                    string areaUrl = destAreaElement.Attributes["href"].Value;
                    Area destArea = new Area()
                    {
                        ID = Utilities.GetID(areaUrl),
                        URL = areaUrl,
                        Name = populateNames ? Utilities.CleanExtraPartsFromName(destAreaElement.TextContent) : null,
                    };

                    destAreas.Add(destArea);
                    TotalAreas++;
                }
            }

            return destAreas;
        }

        #region Parse Area
        public static async Task ParseAreaAsync(Area inputArea, bool recursive = true, bool consoleMessages = true)
        {
            Stopwatch areaStopwatch = Stopwatch.StartNew();

            IHtmlDocument doc = null;
            try
            {
                using (doc = await Utilities.GetHtmlDocAsync(inputArea.URL, true))
                {
                    await ParseAreaAsync(doc, inputArea, recursive, consoleMessages, areaStopwatch);
                }
            }
            catch (Exception ex)
            {
                string html = null;
                try
                {
                    //Todo: for some reason, the below throws an exception. Supposedly .Text has a MemberNotNull attribute
                    //in AngleSharp, but I don't know if it works. More debugging (into AngleSharp) would be required.
                    html = doc?.Source?.Text;
                }
                catch { }

                throw new ParseException($"Failed to parse area with id {inputArea?.ID}", ex)
                {
                    RelatedObject = inputArea,
                    Html = html,
                };
            }
        }

        public static async Task ParseAreaAsync(IHtmlDocument doc, Area inputArea, bool recursive, bool consoleMessages, Stopwatch areaStopwatch)
        {
            List<IElement> htmlRoutes = new List<IElement>();
            List<IElement> htmlSubAreas = new List<IElement>();

            string redactedName = await TryGetRedactedName(doc, inputArea.ID, "Area");
            if (!string.IsNullOrEmpty(redactedName))
            {
                inputArea.Name = redactedName;
                inputArea.IsNameRedacted = true;
            }

            if (string.IsNullOrEmpty(inputArea.Name))
            {
                inputArea.Name = Utilities.CleanExtraPartsFromName(ParseAreaNameFromSidebar(doc));
            }

            if (consoleMessages)
            {
                Console.WriteLine($"Current Area: {inputArea.Name}");
            }

            inputArea.Name = FilterName(inputArea.Name);
            inputArea.NameForMatch = FilterNameForMatch(inputArea.Name, inputArea.ID);
            inputArea.Statistics = PopulateStatistics(doc);
            inputArea.Popularity = ParsePopularity(doc);
            inputArea.ParentIDs = GetParentIDs(doc, inputArea);
            inputArea.PopularRouteIDs = GetPopularRouteIDs(doc, 3);

            //Get Area's routes
            IElement routesTable = doc.GetElementsByTagName("table").FirstOrDefault(p => p.Attributes["id"] != null && p.Attributes["id"].Value == "left-nav-route-table");
            htmlRoutes = routesTable == null ? new List<IElement>() : routesTable.GetElementsByTagName("a").ToList();

            //Get Area's areas
            IElement leftColumnDiv = doc.GetElementsByTagName("div").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar");
            htmlSubAreas = doc.GetElementsByTagName("a").Where(p => p.ParentElement.ParentElement.ParentElement == leftColumnDiv).ToList();
            htmlSubAreas.RemoveAll(p => p.ParentElement.ParentElement.Attributes["id"] != null && p.ParentElement.ParentElement.Attributes["id"].Value == "nearbyMTBRides");
            htmlSubAreas.RemoveAll(p => !Url.Contains(p.Attributes["href"].Value, Utilities.MPBASEURL));

            //Populate route details
            foreach (IElement routeElement in htmlRoutes)
            {
                string routeUrl = routeElement.Attributes["href"].Value;
                Route route = new Route(routeElement.TextContent, Utilities.GetID(routeUrl));
                route.URL = routeUrl;

                //This will be overwritten later, but assigning upon construction here so we know some info about parents
                //  in case we fail to get the HTML for the route
                route.ParentIDs = inputArea.ParentIDs.Concat(new[] { inputArea.ID }).ToList();

                inputArea.Routes.Add(route);
                TotalRoutes++;

                try
                {
                    await ParseRouteAsync(route, consoleMessages); //Parse route
                }
                catch (ParseException ex)
                {
                    //Handle things like this https://www.mountainproject.com/forum/topic/126874784
                    if (ex.InnerException is SourceMissingException sourceMissingException)
                    {
                        Info.TryAdd(DateTime.Now, sourceMissingException.ToString());
                        inputArea.Routes.Remove(route);
                        TotalRoutes--;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            //Populate sub area details
            foreach (IElement areaElement in htmlSubAreas)
            {
                string areaUrl = areaElement.Attributes["href"].Value;
                Area subArea = new Area()
                {
                    ID = Utilities.GetID(areaUrl),
                    URL = areaUrl,

                    //This will be overwritten later, but assigning upon construction here so we know some info about parents
                    //  in case we fail to get the HTML for the area
                    ParentIDs = inputArea.ParentIDs.Concat(new[] {inputArea.ID}).ToList(),
                };

                inputArea.SubAreas.Add(subArea);
                TotalAreas++;

                if (recursive)
                {
                    try
                    {
                        await ParseAreaAsync(subArea, consoleMessages: consoleMessages); //Parse sub area
                    }
                    catch (ParseException ex)
                    {
                        //Handle things like this https://www.mountainproject.com/forum/topic/126874784
                        if (ex.InnerException is SourceMissingException sourceMissingException)
                        {
                            Info.TryAdd(DateTime.Now, sourceMissingException.ToString());
                            inputArea.SubAreas.Remove(subArea);
                            TotalAreas--;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }

            if (consoleMessages)
            {
                Console.WriteLine($"Done with Area: {inputArea.Name} ({areaStopwatch.Elapsed}). {htmlRoutes.Count} routes, {htmlSubAreas.Count} subareas");
            }
        }

        public static List<string> GetPopularRouteIDs(IHtmlDocument doc, int numberToReturn)
        {
            List<string> result = new List<string>();

            IElement outerTable = doc.GetElementsByTagName("table").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "table table-striped route-table hidden-sm-up");
            if (outerTable == null) //Some areas don't have a "classic climbs" table (later we can approximate this based on the Popularity property)
            {
                return result;
            }

            List<IElement> rows = outerTable.GetElementsByTagName("tr").ToList(); //Skip the header row
            foreach (IElement row in rows)
            {
                result.Add(Utilities.GetID(row.GetElementsByTagName("a").FirstOrDefault().Attributes["href"].Value));
            }

            return result.Take(numberToReturn).ToList();
        }

        public static AreaStats PopulateStatistics(IHtmlDocument doc)
        {
            int boulderCount = 0, TRCount = 0, sportCount = 0, tradCount = 0;

            string boulderString = Regex.Match(doc.DocumentElement.InnerHtml, "\\[\\\"Boulder\\\",\\s*\\d*\\]").Value;
            string TRString = Regex.Match(doc.DocumentElement.InnerHtml, "\\[\\\"Toprope\\\",\\s*\\d*\\]").Value;
            string sportString = Regex.Match(doc.DocumentElement.InnerHtml, "\\[\\\"Sport\\\",\\s*\\d*\\]").Value;
            string tradString = Regex.Match(doc.DocumentElement.InnerHtml, "\\[\\\"Trad\\\",\\s*\\d*\\]").Value;

            if (!string.IsNullOrEmpty(boulderString))
            {
                boulderString = boulderString.Replace(" ", "").Replace("\n", "");
                boulderCount = Convert.ToInt32(Regex.Match(boulderString, @"\d+").Value);
            }

            if (!string.IsNullOrEmpty(TRString))
            {
                TRString = TRString.Replace(" ", "").Replace("\n", "");
                TRCount = Convert.ToInt32(Regex.Match(TRString, @"\d+").Value);
            }

            if (!string.IsNullOrEmpty(sportString))
            {
                sportString = sportString.Replace(" ", "").Replace("\n", "");
                sportCount = Convert.ToInt32(Regex.Match(sportString, @"\d+").Value);
            }

            if (!string.IsNullOrEmpty(tradString))
            {
                tradString = tradString.Replace(" ", "").Replace("\n", "");
                tradCount = Convert.ToInt32(Regex.Match(tradString, @"\d+").Value);
            }

            return new AreaStats(boulderCount, TRCount, sportCount, tradCount);
        }
        #endregion Parse Area

        #region Parse Route
        public static async Task ParseRouteAsync(Route inputRoute, bool consoleMessages = true)
        {
            if (consoleMessages)
            {
                Console.WriteLine($"Current Route: {inputRoute.Name}");
            }

            Stopwatch routeStopwatch = Stopwatch.StartNew();

            IHtmlDocument doc = null;
            try
            {
                using (doc = await Utilities.GetHtmlDocAsync(inputRoute.URL, true))
                {
                    await ParseRouteAsync(doc, inputRoute, consoleMessages, routeStopwatch);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                string html = null;
                try
                {
                    //Sometimes this can fail at "AngleSharp.Text.TextSource.get_Text()" (still not sure why),
                    //but wrapping it here to not muddy the ParseException we're constructing with a different exception
                    html = doc?.Source?.Text;
                }
                catch { }

                throw new ParseException($"Failed to parse route with id {inputRoute?.ID}", ex)
                {
                    RelatedObject = inputRoute,
                    Html = html,
                };
            }
        }

        public static async Task ParseRouteAsync(IHtmlDocument doc, Route inputRoute, bool consoleMessages, Stopwatch routeStopwatch)
        {
            string redactedName = await TryGetRedactedName(doc, inputRoute.ID, "Route");
            if (!string.IsNullOrEmpty(redactedName))
            {
                inputRoute.Name = redactedName;
                inputRoute.IsNameRedacted = true;
            }

            if (string.IsNullOrEmpty(inputRoute.Name))
            {
                inputRoute.Name = ParseNameFromHeader(doc);
            }

            inputRoute.Name = FilterName(inputRoute.Name);
            inputRoute.NameForMatch = FilterNameForMatch(inputRoute.Name, inputRoute.ID);
            inputRoute.Types = ParseRouteTypes(doc);
            inputRoute.Popularity = ParsePopularity(doc);
            inputRoute.Rating = ParseRouteRating(doc);
            inputRoute.Grades = ParseRouteGrades(doc);
            string additionalInfo = ParseAdditionalRouteInfo(doc);
            inputRoute.Height = ParseRouteHeight(ref additionalInfo);
            inputRoute.AdditionalInfo = additionalInfo;
            inputRoute.ParentIDs = GetParentIDs(doc, inputRoute);

            if (consoleMessages)
            {
                Console.WriteLine($"Done with Route: {inputRoute.Name} ({routeStopwatch.Elapsed})");

                if (TotalTimer != null && !double.IsNaN(Progress))
                {
                    long elapsedMS = TotalTimer.ElapsedMilliseconds;
                    TimeSpan estTimeRemaining = TimeSpan.FromMilliseconds((elapsedMS / Progress) - elapsedMS);
                    ConsoleHelper.RecordProgress(Progress, estTimeRemaining);
                }
            }
        }

        public static double ParseRouteRating(IHtmlDocument doc)
        {
            IElement ratingElement = doc.GetElementsByTagName("span").FirstOrDefault(x => x.Children.FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value.Contains("scoreStars")) != null);
            string ratingStr = Regex.Match(ratingElement.TextContent, @"Avg.*?(\d+(\.\d*)?)").Groups[1].Value;
            double.TryParse(ratingStr, out double rating);

            return rating;
        }

        public static List<Grade> ParseRouteGrades(IHtmlDocument doc)
        {
            List<Grade> grades = new List<Grade>();
            IElement gradesSection = doc.GetElementsByTagName("h2").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "inline-block mr-2");
            
            if (gradesSection == null)
            {
                //This was added because of this issue: https://www.mountainproject.com/forum/topic/126874784/mountainproject-lists-more-subareas-if-you-are-not-logged-in#ForumMessage-127092247
                return new List<Grade>();
            }

            foreach (IElement spanElement in gradesSection.GetElementsByTagName("span"))
            {
                if (spanElement.Attributes["class"] == null || string.IsNullOrEmpty(spanElement.GetElementsByTagName("a").FirstOrDefault()?.TextContent))
                    continue;

                string gradeValue = HttpUtility.HtmlDecode(spanElement.TextContent.Replace(spanElement.GetElementsByTagName("a").FirstOrDefault().TextContent, "")).Trim();
                switch (spanElement.Attributes["class"].Value)
                {
                    case "rateYDS":
                    case "rateHueco":
                        List<Grade> parsedGrades = Grade.ParseString(gradeValue);
                        if (parsedGrades.Count > 0)
                            grades.AddRange(parsedGrades);
                        else
                        {
                            //I think there's an issue with the MountainProject website where Hueco grades are listed as YDS (eg /route/111259770/three-pipe-problem).
                            //I've reported this to them (I think) but for now I'm "coding around it".
                            if (gradeValue.Contains("V"))
                                grades.Add(new Grade(GradeSystem.Hueco, gradeValue, false));
                            else
                                grades.Add(new Grade(GradeSystem.YDS, gradeValue, false));
                        }
                        break;
                    case "rateFrench":
                        grades.Add(new Grade(GradeSystem.French, gradeValue, false));
                        break;
                    case "rateEwbanks":
                        grades.Add(new Grade(GradeSystem.Ewbanks, gradeValue, false));
                        break;
                    case "rateUIAA":
                        grades.Add(new Grade(GradeSystem.UIAA, gradeValue, false));
                        break;
                    case "rateZA":
                        grades.Add(new Grade(GradeSystem.SouthAfrica, gradeValue, false));
                        break;
                    case "rateBritish":
                        grades.Add(new Grade(GradeSystem.Britsh, gradeValue, false));
                        break;
                    case "rateFont":
                        grades.Add(new Grade(GradeSystem.Fontainebleau, gradeValue, false));
                        break;
                }
            }

            string gradeInnerText = Regex.Replace(gradesSection.InnerHtml, "<.*>", "", RegexOptions.Singleline).Trim();
            if (!string.IsNullOrWhiteSpace(gradeInnerText))
            {
                if (gradeInnerText.StartsWith("AI") || gradeInnerText.StartsWith("WI") || gradeInnerText.StartsWith("M"))
                    grades.Add(new Grade(GradeSystem.Ice, HttpUtility.HtmlDecode(gradeInnerText)));
                else if (gradeInnerText.StartsWith("A") || gradeInnerText.StartsWith("C"))
                    grades.Add(new Grade(GradeSystem.Aid, HttpUtility.HtmlDecode(gradeInnerText)));
                else
                    grades.Add(new Grade(GradeSystem.Unlabled, HttpUtility.HtmlDecode(gradeInnerText)));
            }

            return grades;
        }

        public static List<RouteType> ParseRouteTypes(IHtmlDocument doc)
        {
            string typeString = HttpUtility.HtmlDecode(doc.GetElementsByTagName("tr").FirstOrDefault(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Type:"))
                                        .GetElementsByTagName("td")[1].TextContent).Trim();

            List<RouteType> result = new List<RouteType>();

            if (Regex.IsMatch(typeString, "BOULDER", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "BOULDER", "", RegexOptions.IgnoreCase);
                result.Add(RouteType.Boulder);
            }

            if (Regex.IsMatch(typeString, "TRAD", RegexOptions.IgnoreCase)) //This has to go before an attempt to match "TR" so that we don't accidentally match "TR" instead of "TRAD"
            {
                typeString = Regex.Replace(typeString, "TRAD", "", RegexOptions.IgnoreCase);
                result.Add(RouteType.Trad);
            }

            if (Regex.IsMatch(typeString, "TR|TOP ROPE", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "TR|TOP ROPE", "", RegexOptions.IgnoreCase);
                result.Add(RouteType.TopRope);
            }

            if (Regex.IsMatch(typeString, "AID", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "AID", "", RegexOptions.IgnoreCase);
                result.Add(RouteType.Aid);
            }

            if (Regex.IsMatch(typeString, "SPORT", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "SPORT", "", RegexOptions.IgnoreCase);
                result.Add(RouteType.Sport);
            }

            if (Regex.IsMatch(typeString, "MIXED", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "MIXED", "", RegexOptions.IgnoreCase);
                result.Add(RouteType.Mixed);
            }

            if (Regex.IsMatch(typeString, "ICE", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "ICE", "", RegexOptions.IgnoreCase);
                result.Add(RouteType.Ice);
            }

            if (Regex.IsMatch(typeString, "ALPINE", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "ALPINE", "", RegexOptions.IgnoreCase);
                result.Add(RouteType.Alpine);
            }

            if (Regex.IsMatch(typeString, "SNOW", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "SNOW", "", RegexOptions.IgnoreCase);
                result.Add(RouteType.Snow);
            }

            return result;
        }

        public static string ParseAdditionalRouteInfo(IHtmlDocument doc)
        {
            string typeString = HttpUtility.HtmlDecode(doc.GetElementsByTagName("tr").FirstOrDefault(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Type:"))
                            .GetElementsByTagName("td")[1].TextContent).Trim();

            typeString = Regex.Replace(typeString, "TRAD|TR|SPORT|BOULDER|MIXED|ICE|ALPINE|AID|SNOW", "", RegexOptions.IgnoreCase);
            if (!string.IsNullOrEmpty(Regex.Replace(typeString, "[^a-zA-Z0-9]", "")))
            {
                typeString = Regex.Replace(typeString, @"(, *){2,}|^, *", "").Trim(); //Clean string

                return typeString;
            }

            return "";
        }

        public static Dimension ParseRouteHeight(ref string additionalInfo)
        {
            string regexMatch = Regex.Match(additionalInfo, @"\d+\s?ft(\s\(\d+\s?m\))?", RegexOptions.IgnoreCase).Value;
            if (!string.IsNullOrEmpty(regexMatch))
            {
                additionalInfo = additionalInfo.Replace(regexMatch, "").Trim();
                additionalInfo = Regex.Replace(additionalInfo, @"(, *){2,}|^, *", "").Trim(); //Clean string

                return new Dimension(Convert.ToDouble(Regex.Match(regexMatch, @"\d*").Value), Dimension.Units.Feet);
            }
            else
                return null;
        }
        #endregion Parse Route

        #region Common Parse Methods
        public static async Task<string> TryGetRedactedName(IHtmlDocument doc, string currentId, string objectType)
        {
            string redactedLink = $"{Utilities.MPBASEURL}/updates/Climb-Lib-Models-{objectType}/{currentId}/redacted";
            IElement redactedLinkElement = doc.GetElementsByTagName("a").FirstOrDefault(p => p.Attributes["href"] != null && Url.Contains(p.Attributes["href"].Value, redactedLink) &&
                p.GetElementsByTagName("img").FirstOrDefault(i => i.Attributes["data-original-title"] != null && i.Attributes["data-original-title"].Value == "The original name has been redacted. Click for more info.") != null);
            if (redactedLinkElement != null)
            {
                using (IHtmlDocument objectUpdatesDoc = await Utilities.GetHtmlDocAsync(Url.BuildFullUrl(redactedLink)))
                {
                    IElement update = objectUpdatesDoc.GetElementsByTagName("h1").FirstOrDefault(e => e.TextContent == "Original Name" || e.TextContent == "Name History").ParentElement;
                    Regex regex = new Regex("Mountain Project has chosen not to publish the original name of this route:\\s*\\\"(?<original_name1>.*)\\\"|\\\"(?<original_name2>.*)\\\" was renamed \\\"(?<new_name>.*)\\\"");

                    Match match = regex.Match(update.InnerHtml);
                    if (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups["original_name1"].Value))
                        {
                            return match.Groups["original_name1"].Value;
                        }
                        else if (!string.IsNullOrWhiteSpace(match.Groups["original_name2"].Value))
                        {
                            return match.Groups["original_name2"].Value;
                        }
                    }
                }
            }

            return null;
        }

        public static string FilterName(string name)
        {
            if (Regex.Match(name, ", The", RegexOptions.IgnoreCase).Success)
            {
                name = name.Replace(", The", "");
                name = "The " + name;
            }

            return name.Trim();
        }

        public static string FilterNameForMatch(string name, string id)
        {
            string nameForMatch;

            //Remove any special characters (spaces, apostrophes, etc) and leave only letter characters (of any language)
            //Ideally, we would do this during Utilties.StringContains but Regex.Replace takes a significant 
            //amount of time. So running it during the DBBuild saves time for the Reddit bot
            nameForMatch = Utilities.FilterStringForMatch(Utilities.EnforceWordConsistency(name));
            if (id != null && Utilities.AreaNicknames.ContainsKey(id))
                nameForMatch = $"\\b{nameForMatch}\\b|{Utilities.AreaNicknames[id]}";

            return nameForMatch;
        }

        public static List<string> GetParentIDs(IHtmlDocument doc, MPObject mpObject)
        {
            List<string> result = new List<string>();
            IElement outerDiv = doc.GetElementsByTagName("div").FirstOrDefault(x => x.Children.FirstOrDefault(p => p.TagName == "A" && p.TextContent == "All Locations") != null);
            List<IElement> parentList = outerDiv.Children.Where(p => p.TagName == "A").ToList();
            foreach (IElement parentElement in parentList)
            {
                string url = parentElement.Attributes["href"].Value;
                if (!Url.Contains(url, Utilities.ALLLOCATIONSSUFFIX))
                {
                    string id = Utilities.GetID(url);
                    if (string.IsNullOrEmpty(id))
                    {
                        string html = null;
                        try
                        {
                            //Sometimes this can fail at "AngleSharp.Text.TextSource.get_Text()" (still not sure why),
                            //but wrapping it here to not muddy the ParseException we're constructing with a different exception
                            html = doc?.Source?.Text;
                        }
                        catch { }

                        throw new ParseException($"Failed to parse id from url {url} when getting parents for child")
                        {
                            RelatedObject = mpObject,
                            Html = html,
                        };
                    }

                    result.Add(Utilities.GetID(url));
                }
            }

            return result;
        }

        public static int ParsePopularity(IHtmlDocument doc)
        {
            try
            {
                IElement pageViewsElement = doc.GetElementsByTagName("tr").FirstOrDefault(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Page Views:"))
                                    .GetElementsByTagName("td")[1];
                string pageViewsStr = Regex.Match(pageViewsElement.TextContent.Replace(",", ""), @"(\d+)\s*total").Groups[1].Value;
                return Convert.ToInt32(pageViewsStr);
            }
            catch //There was a weird page (/route/119443775/arcane-shift) where a large section of the page's HTML was commented out. So we'll try to catch that issue
            {
                return -1;
            }
        }

        public static string ParseAreaNameFromSidebar(IHtmlDocument doc)
        {
            //Todo: in the future, we could probably do this better by getting the "intersection" of the header name & the sidebar name
            //(and still defaulting to cleaning up the header name if the sidebar name does not exist).
            //For example: https://www.mountainproject.com/area/106263225/public-sanitation-wall
            //Sidebar: Routes in K. Public Sanitation Wall
            //Header: Public Sanitation Wall Rock Climbing
            //Intersection: Public Sanitation Wall
            IElement leftColumnDiv = doc.GetElementsByTagName("div").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar");
            IElement nameElementInSidebar = doc.GetElementsByTagName("h3").FirstOrDefault(p => p.ParentElement == leftColumnDiv);
            if (nameElementInSidebar != null)
                return nameElementInSidebar.TextContent.Replace("Routes in ", "").Replace("Areas in ", "").Trim();
            else
                return ParseNameFromHeader(doc);
        }

        public static string ParseNameFromHeader(IHtmlDocument doc)
        {
            return doc.GetElementsByTagName("h1").FirstOrDefault().InnerHtml.Replace("\n", "").Split('<')[0].Trim();
        }
        #endregion Common Parse Methods
    }
}