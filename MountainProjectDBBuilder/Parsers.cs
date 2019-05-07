using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Common;
using MountainProjectModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MountainProjectDBBuilder
{
    public static class Parsers
    {
        public static List<Area> GetDestAreas()
        {
            List<Area> destAreas = new List<Area>();

            IHtmlDocument doc = Utilities.GetHtmlDoc(Utilities.MPBASEURL);
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

        public static async Task ParseAreaAsync(Area inputArea, bool recursive = true)
        {
            Utilities.Log($"Current Area: {inputArea.Name}");

            Stopwatch areaStopwatch = Stopwatch.StartNew();
            IHtmlDocument doc = await Utilities.GetHtmlDocAsync(inputArea.URL);

            if (string.IsNullOrEmpty(inputArea.Name))
                inputArea.Name = Regex.Replace(doc.GetElementsByTagName("h1").FirstOrDefault().TextContent, @"<[^>]*>", "").Replace("\n", "").Trim();

            inputArea.Statistics = PopulateStatistics(doc);

            //Get Area "popularity" (page views)
            IElement pageViewsElement = doc.GetElementsByTagName("tr").FirstOrDefault(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Page Views:"))
                                            .GetElementsByTagName("td")[1];
            string pageViewsStr = Regex.Match(pageViewsElement.TextContent.Replace(",", ""), @"(\d+)\s*total").Groups[1].Value;
            inputArea.Popularity = Convert.ToInt32(pageViewsStr);

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

            Utilities.Log($"Done with Area: {inputArea.Name} ({areaStopwatch.Elapsed}). {htmlRoutes.Count} routes, {htmlSubAreas.Count} subareas");
        }

        public static async Task ParseRouteAsync(Route inputRoute)
        {
            Utilities.Log($"Current Route: {inputRoute.Name}");

            Stopwatch routeStopwatch = Stopwatch.StartNew();
            IHtmlDocument doc = await Utilities.GetHtmlDocAsync(inputRoute.URL);

            if (string.IsNullOrEmpty(inputRoute.Name))
                inputRoute.Name = Regex.Replace(doc.GetElementsByTagName("h1").FirstOrDefault().TextContent, @"<[^>]*>", "").Replace("\n", "").Trim();

            //Get Route type
            string typeString = HttpUtility.HtmlDecode(doc.GetElementsByTagName("tr").FirstOrDefault(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Type:"))
                                        .GetElementsByTagName("td")[1].TextContent).Trim();
            inputRoute.Types = ParseRouteTypes(typeString);

            //Get Route "popularity" (page views)
            IElement pageViewsElement = doc.GetElementsByTagName("tr").FirstOrDefault(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Page Views:"))
                                            .GetElementsByTagName("td")[1];
            string pageViewsStr = Regex.Match(pageViewsElement.TextContent.Replace(",", ""), @"(\d+)\s*total").Groups[1].Value;
            inputRoute.Popularity = Convert.ToInt32(pageViewsStr);

            //Get Route grade
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

            inputRoute.Grade = routeGrade;

            inputRoute.AdditionalInfo = ParseAdditionalRouteInfo(typeString); //Get Route additional info

            doc.Dispose();

            Utilities.Log($"Done with Route: {inputRoute.Name} ({routeStopwatch.Elapsed})");
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

        public static List<Route.RouteType> ParseRouteTypes(string inputString)
        {
            List<Route.RouteType> result = new List<Route.RouteType>();

            if (Regex.IsMatch(inputString, "BOULDER", RegexOptions.IgnoreCase))
            {
                inputString = Regex.Replace(inputString, "BOULDER", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Boulder);
            }

            if (Regex.IsMatch(inputString, "TRAD", RegexOptions.IgnoreCase)) //This has to go before an attempt to match "TR" so that we don't accidentally match "TR" instead of "TRAD"
            {
                inputString = Regex.Replace(inputString, "TRAD", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Trad);
            }

            if (Regex.IsMatch(inputString, "TR|TOP ROPE", RegexOptions.IgnoreCase))
            {
                inputString = Regex.Replace(inputString, "TR|TOP ROPE", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.TopRope);
            }

            if (Regex.IsMatch(inputString, "AID", RegexOptions.IgnoreCase))
            {
                inputString = Regex.Replace(inputString, "AID", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Aid);
            }

            if (Regex.IsMatch(inputString, "SPORT", RegexOptions.IgnoreCase))
            {
                inputString = Regex.Replace(inputString, "SPORT", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Sport);
            }

            if (Regex.IsMatch(inputString, "MIXED", RegexOptions.IgnoreCase))
            {
                inputString = Regex.Replace(inputString, "MIXED", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Mixed);
            }

            if (Regex.IsMatch(inputString, "ICE", RegexOptions.IgnoreCase))
            {
                inputString = Regex.Replace(inputString, "ICE", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Ice);
            }

            if (Regex.IsMatch(inputString, "ALPINE", RegexOptions.IgnoreCase))
            {
                inputString = Regex.Replace(inputString, "ALPINE", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Alpine);
            }

            if (Regex.IsMatch(inputString, "SNOW", RegexOptions.IgnoreCase))
            {
                inputString = Regex.Replace(inputString, "SNOW", "", RegexOptions.IgnoreCase);
                result.Add(Route.RouteType.Snow);
            }

            return result;
        }

        public static string ParseAdditionalRouteInfo(string inputString)
        {
            inputString = Regex.Replace(inputString, "TRAD|TR|SPORT|BOULDER|MIXED|ICE|ALPINE|AID|SNOW", "", RegexOptions.IgnoreCase);
            if (!string.IsNullOrEmpty(Regex.Replace(inputString, "[^a-zA-Z0-9]", "")))
            {
                inputString = Regex.Replace(inputString, @"\s+", " "); //Replace multiple spaces (more than one in a row) with a single space
                inputString = Regex.Replace(inputString, @"\s+,", ","); //Remove any spaces before commas
                inputString = Regex.Replace(inputString, "^,+|,+$|,{2,}", ""); //Remove any commas at the beginning/end of string (or multiple commas in a row)
                inputString = inputString.Trim(); //Trim any extra whitespace from the beginning/end of string

                return inputString;
            }

            return "";
        }
    }
}
