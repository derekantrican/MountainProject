using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MountainProjectAPI
{
    public static class Parsers
    {
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
                    Name = destAreaElement.TextContent,
                    URL = destAreaElement.Attributes["href"].Value
                };

                destAreas.Add(destArea);
            }

            doc.Dispose();

            return destAreas;
        }

        #region Parse Area
        public static async Task ParseAreaAsync(Area inputArea, bool recursive = true)
        {
            Console.WriteLine($"Current Area: {inputArea.Name}");

            Stopwatch areaStopwatch = Stopwatch.StartNew();
            IHtmlDocument doc = await Utilities.GetHtmlDocAsync(inputArea.URL);

            if (string.IsNullOrEmpty(inputArea.Name))
                inputArea.Name = ParseName(doc);

            inputArea.Statistics = PopulateStatistics(doc);
            inputArea.Popularity = ParsePopularity(doc);
            inputArea.ParentUrls = GetParentUrls(doc);
            inputArea.PopularRouteUrls = GetPopularRouteUrls(doc, 3);

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
                Route route = new Route() { Name = routeElement.TextContent, URL = routeElement.Attributes["href"].Value };
                inputArea.Routes.Add(route);
                await ParseRouteAsync(route); //Parse route
            }

            //Populate sub area details
            foreach (IElement areaElement in htmlSubAreas)
            {
                Area subArea = new Area() { Name = areaElement.TextContent, URL = areaElement.Attributes["href"].Value };
                inputArea.SubAreas.Add(subArea);

                if (recursive)
                    await ParseAreaAsync(subArea); //Parse sub area
            }

            Console.WriteLine($"Done with Area: {inputArea.Name} ({areaStopwatch.Elapsed}). {htmlRoutes.Count} routes, {htmlSubAreas.Count} subareas");
        }

        public static List<string> GetPopularRouteUrls(IHtmlDocument doc, int numberToReturn)
        {
            List<string> result = new List<string>();

            IElement outerTable = doc.GetElementsByTagName("table").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "table table-striped route-table hidden-sm-up");
            if (outerTable == null) //Some areas don't have a "classic climbs" table (later we can approximate this based on the Popularity property)
                return result;

            List<IElement> rows = outerTable.GetElementsByTagName("tr").ToList(); //Skip the header row
            foreach (IElement row in rows)
                result.Add(row.GetElementsByTagName("a").FirstOrDefault().Attributes["href"].Value);

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
                inputRoute.Name = ParseName(doc);

            inputRoute.Types = ParseRouteTypes(doc);
            inputRoute.Popularity = ParsePopularity(doc);
            inputRoute.Grade = ParseRouteGrade(doc);
            inputRoute.AdditionalInfo = ParseAdditionalRouteInfo(doc);
            inputRoute.ParentUrls = GetParentUrls(doc);

            doc.Dispose();

            Console.WriteLine($"Done with Route: {inputRoute.Name} ({routeStopwatch.Elapsed})");
        }

        public static string ParseRouteGrade(IHtmlDocument doc)
        {
            List<IElement> gradesOnPage = doc.GetElementsByTagName("span").Where(x => x.Attributes["class"] != null &&
                                                                                   (x.Attributes["class"].Value == "rateHueco" || x.Attributes["class"].Value == "rateYDS")).ToList();
            IElement sidebar = doc.GetElementsByTagName("div").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar");
            gradesOnPage.RemoveAll(p => sidebar.Descendents().Contains(p));
            string routeGrade = "";
            IElement gradeElement = gradesOnPage.FirstOrDefault();
            if (gradeElement != null)
                routeGrade = HttpUtility.HtmlDecode(gradeElement.TextContent.Replace(gradeElement.GetElementsByTagName("a").FirstOrDefault().TextContent, "")).Trim();
            else
            {
                IElement gradesSection = doc.GetElementsByTagName("h2").FirstOrDefault(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "inline-block mr-2");
                routeGrade = HttpUtility.HtmlDecode(gradesSection.TextContent);
            }

            return routeGrade;
        }

        public static List<Route.RouteType> ParseRouteTypes(IHtmlDocument doc)
        {
            string typeString = HttpUtility.HtmlDecode(doc.GetElementsByTagName("tr").FirstOrDefault(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Type:"))
                                        .GetElementsByTagName("td")[1].TextContent).Trim();

            List<Route.RouteType> result = new List<Route.RouteType>();

            if (Regex.IsMatch(typeString, "BOULDER", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "BOULDER", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Boulder);
            }

            if (Regex.IsMatch(typeString, "TRAD", RegexOptions.IgnoreCase)) //This has to go before an attempt to match "TR" so that we don't accidentally match "TR" instead of "TRAD"
            {
                typeString = Regex.Replace(typeString, "TRAD", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Trad);
            }

            if (Regex.IsMatch(typeString, "TR|TOP ROPE", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "TR|TOP ROPE", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.TopRope);
            }

            if (Regex.IsMatch(typeString, "AID", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "AID", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Aid);
            }

            if (Regex.IsMatch(typeString, "SPORT", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "SPORT", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Sport);
            }

            if (Regex.IsMatch(typeString, "MIXED", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "MIXED", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Mixed);
            }

            if (Regex.IsMatch(typeString, "ICE", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "ICE", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Ice);
            }

            if (Regex.IsMatch(typeString, "ALPINE", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "ALPINE", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Alpine);
            }

            if (Regex.IsMatch(typeString, "SNOW", RegexOptions.IgnoreCase))
            {
                typeString = Regex.Replace(typeString, "SNOW", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Snow);
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
                typeString = Regex.Replace(typeString, @"\s+", " "); //Replace multiple spaces (more than one in a row) with a single space
                typeString = Regex.Replace(typeString, @"\s+,", ","); //Remove any spaces before commas
                typeString = Regex.Replace(typeString, "^,+|,+$|,{2,}", ""); //Remove any commas at the beginning/end of string (or multiple commas in a row)
                typeString = typeString.Trim(); //Trim any extra whitespace from the beginning/end of string

                return typeString;
            }

            return "";
        }
        #endregion Parse Route

        #region Common Parse Methods
        public static List<string> GetParentUrls(IHtmlDocument doc)
        {
            List<string> result = new List<string>();
            IElement outerDiv = doc.GetElementsByTagName("div").FirstOrDefault(x => x.Children.FirstOrDefault(p => p.TagName == "A" && p.TextContent == "All Locations") != null);
            List<IElement> parentList = outerDiv.Children.Where(p => p.TagName == "A").ToList();
            foreach (IElement parentElement in parentList)
                result.Add(parentElement.Attributes["href"].Value);

            return result;
        }

        public static int ParsePopularity(IHtmlDocument doc)
        {
            IElement pageViewsElement = doc.GetElementsByTagName("tr").FirstOrDefault(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Page Views:"))
                                .GetElementsByTagName("td")[1];
            string pageViewsStr = Regex.Match(pageViewsElement.TextContent.Replace(",", ""), @"(\d+)\s*total").Groups[1].Value;
            return Convert.ToInt32(pageViewsStr);
        }

        public static string ParseName(IHtmlDocument doc)
        {
            return Regex.Replace(doc.GetElementsByTagName("h1").FirstOrDefault().TextContent, @"<[^>]*>", "").Replace("\n", "").Trim();
        }
        #endregion Common Parse Methods
    }
}
