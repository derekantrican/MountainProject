using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MountainProjectDBBuilder
{
    public static class Parsers
    {
        public static List<DestArea> PopulateAreas(List<HtmlNode> inputNodes)
        {
            List<DestArea> result = new List<DestArea>();
            foreach (HtmlNode destArea in inputNodes)
            {
                DestArea currentArea = new DestArea(destArea.InnerText,
                                                    destArea.Attributes["href"].Value);
                result.Add(currentArea);
            }

            return result;
        }

        public static void PopulateSubDestAreas(DestArea inputArea)
        {
            Common.Log("[PopulateSubDestAreas] Populating subAreas for " + inputArea.Name);
            List<SubDestArea> subAreas = new List<SubDestArea>();

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(inputArea.URL);

            HtmlNode leftColumnDiv = doc.DocumentNode.Descendants("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
            List<HtmlNode> htmlSubAreas = doc.DocumentNode.Descendants("a").Where(p => p.ParentNode.ParentNode.ParentNode == leftColumnDiv).ToList();
            htmlSubAreas.RemoveAll(p => p.ParentNode.ParentNode.Attributes["id"] != null && p.ParentNode.ParentNode.Attributes["id"].Value == "nearbyMTBRides");

            foreach (HtmlNode node in htmlSubAreas)
            {
                SubDestArea subArea = new SubDestArea(node.InnerText, node.Attributes["href"].Value);

                if (subAreas.Where(p => p.URL == subArea.URL).FirstOrDefault() != null)
                    throw new Exception("Item already exists in subAreas list: " + subArea.URL);

                subAreas.Add(subArea);
            }

            inputArea.SubAreas = subAreas;
        }

        public static void PopulateSubSubDestAreas(SubDestArea inputSubArea)
        {
            Stopwatch popSubSubDestAreasStopwatch = Stopwatch.StartNew();
            Common.Log("[PopulateSubSubDestAreas] Populating subAreas for " + inputSubArea.Name);
            List<TimeSpan> subsubareaParseTimes = new List<TimeSpan>();
            List<SubDestArea> subAreas = new List<SubDestArea>();

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(inputSubArea.URL);

            HtmlNode leftColumnDiv = doc.DocumentNode.Descendants("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
            List<HtmlNode> htmlSubAreas = doc.DocumentNode.Descendants("a").Where(p => p.ParentNode.ParentNode.ParentNode == leftColumnDiv).ToList();
            htmlSubAreas.RemoveAll(p => (p.ParentNode.ParentNode.Attributes["id"] != null && p.ParentNode.ParentNode.Attributes["id"].Value == "nearbyMTBRides") ||
                                        (p.Attributes["href"] != null && p.Attributes["href"].Value == "#"));

            foreach (HtmlNode node in htmlSubAreas)
            {
                Stopwatch subsubareaStopwatch = Stopwatch.StartNew();
                SubDestArea subArea = new SubDestArea(node.InnerText, node.Attributes["href"].Value);

                if (subAreas.Where(p => p.URL == subArea.URL).FirstOrDefault() != null)
                    throw new Exception("Item already exists in subAreas list: " + subArea.URL);

                subAreas.Add(subArea);
                subsubareaParseTimes.Add(subsubareaStopwatch.Elapsed);
            }

            Common.Log($"[PopulateSubSubDestAreas] Done with subAreas for {inputSubArea.Name} " +
                $"({popSubSubDestAreasStopwatch.Elapsed}. {subAreas.Count} subsubareas, avg {Common.Average(subsubareaParseTimes)})");

            inputSubArea.SubSubAreas = subAreas;
            return;
        }

        public static void PopulateRoutes(SubDestArea inputSubDestArea)
        {
            Stopwatch popRoutesStopwatch = Stopwatch.StartNew();
            Common.Log("[PopulateRoutes] Current subDestArea: " + inputSubDestArea.Name);
            List<TimeSpan> routeParseTimes = new List<TimeSpan>();
            List<Route> routes = new List<Route>();

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(inputSubDestArea.URL);

            HtmlNode routesTable = doc.DocumentNode.Descendants("table").Where(p => p.Attributes["id"] != null && p.Attributes["id"].Value == "left-nav-route-table").FirstOrDefault();

            if (routesTable == null)
            {
                HtmlNode leftColumnDiv = doc.DocumentNode.Descendants("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
                List<HtmlNode> htmlWalls = doc.DocumentNode.Descendants("a").Where(p => p.ParentNode.ParentNode.ParentNode == leftColumnDiv).ToList();
                htmlWalls.RemoveAll(p => p.ParentNode.ParentNode.Attributes["id"] != null && p.ParentNode.ParentNode.Attributes["id"].Value == "nearbyMTBRides");

                if (htmlWalls.Count == 0)
                    return;
            }

            List<HtmlNode> htmlRoutes = routesTable == null ? new List<HtmlNode>() : routesTable.Descendants("a").ToList();

            if (htmlRoutes.Count == 0) //This is for "subsubareas"
            {
                PopulateSubSubDestAreas(inputSubDestArea);
                foreach (SubDestArea subArea in inputSubDestArea.SubSubAreas)
                {
                    subArea.Statistics = PopulateStatistics(subArea.URL);
                    PopulateRoutes(subArea);
                }

                return;
            }
            else
            {
                foreach (HtmlNode node in htmlRoutes)
                {
                    Stopwatch routeStopwatch = Stopwatch.StartNew();

                    Route route = ParseRoute(node.Attributes["href"].Value);

                    if (routes.Where(p => p.URL == route.URL).FirstOrDefault() != null)
                        throw new Exception("Item already exists in routes list: " + route.URL);

                    routes.Add(route);
                    routeParseTimes.Add(routeStopwatch.Elapsed);
                }
            }

            Common.Log($"[PopulateRoutes] Done with subDestArea: {inputSubDestArea.Name} " +
                $"({popRoutesStopwatch.Elapsed}. {routes.Count} routes, avg {Common.Average(routeParseTimes)})");

            inputSubDestArea.Routes = routes;
            return;
        }

        public static Route ParseRoute(string inputURL)
        {
            HtmlWeb routeWeb = new HtmlWeb();
            HtmlDocument routeDoc = routeWeb.Load(inputURL);

            string routeName = Regex.Replace(routeDoc.DocumentNode.Descendants("h1").FirstOrDefault().InnerText, @"<[^>]*>", "").Replace("\n", "").Trim();

            string type = HttpUtility.HtmlDecode(routeDoc.DocumentNode.Descendants("tr").Where(p => p.Descendants("td").FirstOrDefault().InnerText.Contains("Type:")).FirstOrDefault()
                                        .Descendants("td").ToList()[1].InnerText).Trim();
            Route.RouteType routeType = ParseRouteType(type);

            List<HtmlNode> gradesOnPage = routeDoc.DocumentNode.Descendants("span").Where(x => x.Attributes["class"] != null &&
                                                                                               (x.Attributes["class"].Value == "rateHueco" || x.Attributes["class"].Value == "rateYDS")).ToList();
            HtmlNode sidebar = routeDoc.DocumentNode.Descendants("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
            gradesOnPage.RemoveAll(p => sidebar.Descendants().Contains(p));

            string routeGrade = "";
            HtmlNode gradeElement = gradesOnPage.FirstOrDefault();
            if (gradeElement != null)
                routeGrade = HttpUtility.HtmlDecode(gradeElement.InnerText.Replace(gradeElement.Descendants("a").FirstOrDefault().InnerText, "")).Trim();

            Route route = new Route(routeName, routeGrade, routeType, inputURL);
            route.AdditionalInfo = ParseAdditionalRouteInfo(type);

            return route;
        }

        public static AreaStats PopulateStatistics(string inputURL)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(inputURL);

            int boulderCount = 0, TRCount = 0, sportCount = 0, tradCount = 0;

            string boulderString = Regex.Match(doc.DocumentNode.InnerHtml, "\\[\\\"Boulder\\\",\\s*\\d*\\]").Value;
            string TRString = Regex.Match(doc.DocumentNode.InnerHtml, "\\[\\\"Toprope\\\",\\s*\\d*\\]").Value;
            string sportString = Regex.Match(doc.DocumentNode.InnerHtml, "\\[\\\"Sport\\\",\\s*\\d*\\]").Value;
            string tradString = Regex.Match(doc.DocumentNode.InnerHtml, "\\[\\\"Trad\\\",\\s*\\d*\\]").Value;

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

        public static Route.RouteType ParseRouteType(string inputString)
        {
            if (Regex.Match(inputString, "BOULDER", RegexOptions.IgnoreCase).Success)
                return Route.RouteType.Boulder;
            else if (Regex.Match(inputString, "TRAD", RegexOptions.IgnoreCase).Success) //This has to go before an attempt to match "TR" so that we don't accidentally match "TR" instead of "TRAD"
                return Route.RouteType.Trad;
            else if (Regex.Match(inputString, "TR|TOP ROPE", RegexOptions.IgnoreCase).Success)
                return Route.RouteType.TopRope;
            else if (Regex.Match(inputString, "SPORT", RegexOptions.IgnoreCase).Success)
                return Route.RouteType.Sport;

            return Route.RouteType.TopRope;
        }

        public static string ParseAdditionalRouteInfo(string inputString)
        {
            inputString = Regex.Replace(inputString, "TRAD|TR|SPORT|BOULDER", "", RegexOptions.IgnoreCase);
            if (!string.IsNullOrEmpty(Regex.Replace(inputString, "[^a-zA-Z0-9]", "")))
            {
                inputString = Regex.Replace(inputString, @"\s+", " "); //Replace multiple spaces (more than one in a row) with a single space
                inputString = Regex.Replace(inputString, @"\s+,", ","); //Remove any spaces before commas
                inputString = Regex.Replace(inputString, "^,|,$|,{2,}", ""); //Remove any commas at the beginning/end of string (or multiple commas in a row)
                inputString = inputString.Trim(); //Trim any extra whitespace from the beginning/end of string

                return inputString;
            }

            return "";
        }
    }
}
