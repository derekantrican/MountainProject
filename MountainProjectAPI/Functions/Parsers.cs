using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System;
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

        public static List<Area> GetDestAreas()
        {
            List<Area> destAreas = new List<Area>();

            IHtmlDocument doc = Utilities.GetHtmlDoc(Utilities.ALLLOCATIONSURL);
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
                Area destArea = new Area()
                {
                    ID = Utilities.GetID(destAreaElement.Attributes["href"].Value)
                };

                destAreas.Add(destArea);
                TotalAreas++;
            }

            doc.Dispose();

            return destAreas;
        }

        #region Parse Area
        public static async Task ParseAreaAsync(Area inputArea, bool recursive = true)
        {
            Stopwatch areaStopwatch = Stopwatch.StartNew();
            IHtmlDocument doc = await Utilities.GetHtmlDocAsync(inputArea.URL);

            if (string.IsNullOrEmpty(inputArea.Name))
                inputArea.Name = Utilities.CleanExtraPartsFromName(ParseAreaNameFromSidebar(doc));

            Console.WriteLine($"Current Area: {inputArea.Name}");

            inputArea.Statistics = PopulateStatistics(doc);
            inputArea.Popularity = ParsePopularity(doc);
            inputArea.ParentIDs = GetParentIDs(doc);
            inputArea.PopularRouteIDs = GetPopularRouteIDs(doc, 3);

            //Get Area's routes
            IElement routesTable = doc.GetElementsByTagName("table").Where(p => p.Attributes["id"] != null && p.Attributes["id"].Value == "left-nav-route-table").FirstOrDefault();
            List<IElement> htmlRoutes = routesTable == null ? new List<IElement>() : routesTable.GetElementsByTagName("a").ToList();

            //Get Area's areas
            IElement leftColumnDiv = doc.GetElementsByTagName("div").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar");
            List<IElement> htmlSubAreas = doc.GetElementsByTagName("a").Where(p => p.ParentElement.ParentElement.ParentElement == leftColumnDiv).ToList();
            htmlSubAreas.RemoveAll(p => p.ParentElement.ParentElement.Attributes["id"] != null && p.ParentElement.ParentElement.Attributes["id"].Value == "nearbyMTBRides");
            htmlSubAreas.RemoveAll(p => !p.Attributes["href"].Value.Contains(Utilities.MPBASEURL));

            //Dispose doc
            doc.Dispose();

            //Populate route details
            foreach (IElement routeElement in htmlRoutes)
            {
                Route route = new Route(routeElement.TextContent, Utilities.GetID(routeElement.Attributes["href"].Value));
                inputArea.Routes.Add(route);
                TotalRoutes++;

                await ParseRouteAsync(route); //Parse route
            }

            //Populate sub area details
            foreach (IElement areaElement in htmlSubAreas)
            {
                Area subArea = new Area() 
                { 
                    ID = Utilities.GetID(areaElement.Attributes["href"].Value) 
                };

                inputArea.SubAreas.Add(subArea);
                TotalAreas++;

                if (recursive)
                    await ParseAreaAsync(subArea); //Parse sub area
            }

            Console.WriteLine($"Done with Area: {inputArea.Name} ({areaStopwatch.Elapsed}). {htmlRoutes.Count} routes, {htmlSubAreas.Count} subareas");
        }

        public static List<string> GetPopularRouteIDs(IHtmlDocument doc, int numberToReturn)
        {
            List<string> result = new List<string>();

            IElement outerTable = doc.GetElementsByTagName("table").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "table table-striped route-table hidden-sm-up");
            if (outerTable == null) //Some areas don't have a "classic climbs" table (later we can approximate this based on the Popularity property)
                return result;

            List<IElement> rows = outerTable.GetElementsByTagName("tr").ToList(); //Skip the header row
            foreach (IElement row in rows)
                result.Add(Utilities.GetID(row.GetElementsByTagName("a").FirstOrDefault().Attributes["href"].Value));

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
        public static async Task ParseRouteAsync(Route inputRoute)
        {
            Console.WriteLine($"Current Route: {inputRoute.Name}");

            Stopwatch routeStopwatch = Stopwatch.StartNew();
            IHtmlDocument doc = await Utilities.GetHtmlDocAsync(inputRoute.URL);

            if (string.IsNullOrEmpty(inputRoute.Name))
                inputRoute.Name = ParseNameFromHeader(doc);

            inputRoute.Types = ParseRouteTypes(doc);
            inputRoute.Popularity = ParsePopularity(doc);
            inputRoute.Rating = ParseRouteRating(doc);
            inputRoute.Grades = ParseRouteGrades(doc);
            string additionalInfo = ParseAdditionalRouteInfo(doc);
            inputRoute.Height = ParseRouteHeight(ref additionalInfo);
            inputRoute.AdditionalInfo = additionalInfo;
            inputRoute.ParentIDs = GetParentIDs(doc);

            doc.Dispose();

            Console.WriteLine($"Done with Route: {inputRoute.Name} ({routeStopwatch.Elapsed})");

            if (TotalTimer != null)
            {
                long elapsedMS = TotalTimer.ElapsedMilliseconds;
                TimeSpan estTimeRemaining = TimeSpan.FromMilliseconds(elapsedMS / Progress - elapsedMS);
                WriteLineWithColor($"{Progress * 100:0.00}% complete. Estimated time remaining: {Math.Floor(estTimeRemaining.TotalHours)} hours, {estTimeRemaining.Minutes} min", ConsoleColor.Green);
            }
        }

        public static double ParseRouteRating(IHtmlDocument doc)
        {
            IElement ratingElement = doc.GetElementsByTagName("span").FirstOrDefault(x => x.Children.FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value.Contains("scoreStars")) != null);
            double rating = 0;
            string ratingStr = Regex.Match(ratingElement.TextContent, @"Avg.*?(\d+(\.\d*)?)").Groups[1].Value;
            double.TryParse(ratingStr, out rating);

            return rating;
        }

        public static List<Grade> ParseRouteGrades(IHtmlDocument doc)
        {
            List<Grade> grades = new List<Grade>();
            foreach (IElement spanElement in doc.GetElementsByTagName("span"))
            {
                if (spanElement.Attributes["class"] == null ||
                    string.IsNullOrEmpty(spanElement.GetElementsByTagName("a").FirstOrDefault()?.TextContent))
                    continue;

                string gradeValue = HttpUtility.HtmlDecode(spanElement.TextContent.Replace(spanElement.GetElementsByTagName("a").FirstOrDefault().TextContent, "")).Trim();
                switch (spanElement.Attributes["class"].Value)
                {
                    case "rateYDS":
                    case "rateHueco":
                        List<Grade> parsedGrades = Grade.ParseString(gradeValue);
                        if (parsedGrades.Count > 0)
                            grades.Add(Grade.ParseString(gradeValue)[0]);
                        else
                        {
                            //I think there's an issue with the MountainProject website where Hueco grades are listed as YDS (eg /route/111259770/three-pipe-problem).
                            //I've reported this to them (I think) but for now I'm "coding around it".
                            if (gradeValue.Contains("5."))
                                grades.Add(new Grade(GradeSystem.YDS, gradeValue, false));
                            else if (gradeValue.Contains("V"))
                                grades.Add(new Grade(GradeSystem.Hueco, gradeValue, false));
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

            if (grades.Count == 0)
            {
                IElement gradesSection = doc.GetElementsByTagName("h2").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "inline-block mr-2");
                grades.Add(new Grade(GradeSystem.Unlabled, HttpUtility.HtmlDecode(gradesSection.TextContent)));
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
            string regexMatch = Regex.Match(additionalInfo, @"\d+\s?ft", RegexOptions.IgnoreCase).Value;
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
        public static List<string> GetParentIDs(IHtmlDocument doc)
        {
            List<string> result = new List<string>();
            IElement outerDiv = doc.GetElementsByTagName("div").FirstOrDefault(x => x.Children.FirstOrDefault(p => p.TagName == "A" && p.TextContent == "All Locations") != null);
            List<IElement> parentList = outerDiv.Children.Where(p => p.TagName == "A").ToList();
            foreach (IElement parentElement in parentList)
            {
                string url = parentElement.Attributes["href"].Value;
                if (url != "https://www.mountainproject.com/route-guide")
                    result.Add(Utilities.GetID(url));
            }

            return result;
        }

        public static int ParsePopularity(IHtmlDocument doc)
        {
            IElement pageViewsElement = doc.GetElementsByTagName("tr").FirstOrDefault(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Page Views:"))
                                .GetElementsByTagName("td")[1];
            string pageViewsStr = Regex.Match(pageViewsElement.TextContent.Replace(",", ""), @"(\d+)\s*total").Groups[1].Value;
            return Convert.ToInt32(pageViewsStr);
        }

        public static string ParseAreaNameFromSidebar(IHtmlDocument doc)
        {
            IElement leftColumnDiv = doc.GetElementsByTagName("div").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar");
            IElement nameElementInSidebar = doc.GetElementsByTagName("h3").Where(p => p.ParentElement == leftColumnDiv).FirstOrDefault();
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

        private static readonly object sync = new object();
        private static void WriteLineWithColor(string text, ConsoleColor color)
        {
            lock (sync)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
    }
}