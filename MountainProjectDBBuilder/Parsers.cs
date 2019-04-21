using AngleSharp.Dom;
using AngleSharp.Html.Dom;
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
        public static List<DestArea> GetDestAreas()
        {
            List<DestArea> destAreas = new List<DestArea>();

            IHtmlDocument doc = Common.GetHtmlDoc(Common.BaseUrl);
            List<IElement> destAreaNodes = doc.GetElementsByTagName("a").Where(x => x.Attributes["href"] != null &&
                                                                                    Common.MatchesStateUrlRegex(x.Attributes["href"].Value)).ToList();
            destAreaNodes = (from s in destAreaNodes
                             orderby s.TextContent
                             group s by s.Attributes["href"].Value into g
                             select g.First()).ToList();

            //Move international to the end
            IElement internationalArea = destAreaNodes.Find(p => p.TextContent == "International");
            destAreaNodes.Remove(internationalArea);
            destAreaNodes.Add(internationalArea);

            //Convert to DestArea objects
            destAreaNodes.ForEach(p => 
            {
                DestArea currentArea = new DestArea(p.TextContent, p.Attributes["href"].Value);
                destAreas.Add(currentArea);
            });

            return destAreas;
        }

        public static void PopulateSubDestAreas(DestArea inputArea)
        {
            Common.Log("[PopulateSubDestAreas] Populating subAreas for " + inputArea.Name);
            List<SubDestArea> subAreas = new List<SubDestArea>();

            IHtmlDocument doc = Common.GetHtmlDoc(inputArea.URL);
            IElement leftColumnDiv = doc.GetElementsByTagName("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
            List<IElement> htmlSubAreas = doc.GetElementsByTagName("a").Where(p => p.ParentElement.ParentElement.ParentElement == leftColumnDiv).ToList();
            htmlSubAreas.RemoveAll(p => p.ParentElement.ParentElement.Attributes["id"] != null && p.ParentElement.ParentElement.Attributes["id"].Value == "nearbyMTBRides");

            foreach (IElement node in htmlSubAreas)
            {
                SubDestArea subArea = new SubDestArea(node.TextContent, node.Attributes["href"].Value);

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

            IHtmlDocument doc = Common.GetHtmlDoc(inputSubArea.URL);
            IElement leftColumnDiv = doc.GetElementsByTagName("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
            List<IElement> htmlSubAreas = doc.GetElementsByTagName("a").Where(p => p.ParentElement.ParentElement.ParentElement == leftColumnDiv).ToList();
            htmlSubAreas.RemoveAll(p => p.ParentElement.ParentElement.Attributes["id"] != null && p.ParentElement.ParentElement.Attributes["id"].Value == "nearbyMTBRides" ||
                                        (p.Attributes["href"] != null && p.Attributes["href"].Value == "#"));

            foreach (IElement node in htmlSubAreas)
            {
                Stopwatch subsubareaStopwatch = Stopwatch.StartNew();
                SubDestArea subArea = new SubDestArea(node.TextContent, node.Attributes["href"].Value);

                if (subAreas.Where(p => p.URL == subArea.URL).FirstOrDefault() != null)
                    throw new Exception("Item already exists in subAreas list: " + subArea.URL);

                subAreas.Add(subArea);
                subsubareaParseTimes.Add(subsubareaStopwatch.Elapsed);
            }

            Common.Log($"[PopulateSubSubDestAreas] Done with subAreas for {inputSubArea.Name} " +
                $"({popSubSubDestAreasStopwatch.Elapsed}. {subAreas.Count} subsubareas, avg {Common.Average(subsubareaParseTimes)})");

            inputSubArea.SubSubAreas = subAreas;
        }

        public static void PopulateRoutes(SubDestArea inputSubDestArea)
        {
            Stopwatch popRoutesStopwatch = Stopwatch.StartNew();
            Common.Log("[PopulateRoutes] Current subDestArea: " + inputSubDestArea.Name);
            List<TimeSpan> routeParseTimes = new List<TimeSpan>();
            List<Route> routes = new List<Route>();

            IHtmlDocument doc = Common.GetHtmlDoc(inputSubDestArea.URL);
            IElement routesTable = doc.GetElementsByTagName("table").Where(p => p.Attributes["id"] != null && p.Attributes["id"].Value == "left-nav-route-table").FirstOrDefault();

            if (routesTable == null)
            {
                IElement leftColumnDiv = doc.GetElementsByTagName("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
                List<IElement> htmlSubAreas = doc.GetElementsByTagName("a").Where(p => p.ParentElement.ParentElement.ParentElement == leftColumnDiv).ToList();
                htmlSubAreas.RemoveAll(p => p.ParentElement.ParentElement.Attributes["id"] != null && p.ParentElement.ParentElement.Attributes["id"].Value == "nearbyMTBRides");

                if (htmlSubAreas.Count == 0)
                    return;
            }

            List<IElement> htmlRoutes = routesTable == null ? new List<IElement>() : routesTable.GetElementsByTagName("a").ToList();

            if (htmlRoutes.Count == 0) //This is for "subsubareas"
            {
                PopulateSubSubDestAreas(inputSubDestArea);
                foreach (SubDestArea subArea in inputSubDestArea.SubSubAreas)
                {
                    subArea.Statistics = PopulateStatistics(subArea.URL);
                    PopulateRoutes(subArea);
                }
            }
            else
            {
                foreach (IElement node in htmlRoutes)
                {
                    Stopwatch routeStopwatch = Stopwatch.StartNew();

                    Route route = ParseRoute(node.Attributes["href"].Value);

                    if (routes.Where(p => p.URL == route.URL).FirstOrDefault() != null)
                        throw new Exception("Item already exists in routes list: " + route.URL);

                    routes.Add(route);
                    routeParseTimes.Add(routeStopwatch.Elapsed);
                }

                Common.Log($"[PopulateRoutes] Done with subDestArea: {inputSubDestArea.Name} " +
                    $"({popRoutesStopwatch.Elapsed}. {routes.Count} routes, avg {Common.Average(routeParseTimes)})");

                inputSubDestArea.Routes = routes;
            }
        }

        public static Route ParseRoute(string inputURL)
        {
            IHtmlDocument doc = Common.GetHtmlDoc(inputURL);
            string routeName = Regex.Replace(doc.GetElementsByTagName("h1").FirstOrDefault().TextContent, @"<[^>]*>", "").Replace("\n", "").Trim();

            //Get Route type
            string type = HttpUtility.HtmlDecode(doc.GetElementsByTagName("tr").Where(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Type:")).FirstOrDefault()
                                        .GetElementsByTagName("td").ToList()[1].TextContent).Trim();
            Route.RouteType routeType = ParseRouteType(type);

            //Get Route grade
            List<IElement> gradesOnPage = doc.GetElementsByTagName("span").Where(x => x.Attributes["class"] != null &&
                                                                                               (x.Attributes["class"].Value == "rateHueco" || x.Attributes["class"].Value == "rateYDS")).ToList();
            IElement sidebar = doc.GetElementsByTagName("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
            gradesOnPage.RemoveAll(p => sidebar.Descendents().Contains(p));
            string routeGrade = "";
            IElement gradeElement = gradesOnPage.FirstOrDefault();
            if (gradeElement != null)
                routeGrade = HttpUtility.HtmlDecode(gradeElement.TextContent.Replace(gradeElement.GetElementsByTagName("a").FirstOrDefault().TextContent, "")).Trim();

            Route route = new Route(routeName, routeGrade, routeType, inputURL);
            route.AdditionalInfo = ParseAdditionalRouteInfo(type); //Get Route additional info

            return route;
        }

        public static AreaStats PopulateStatistics(string inputURL)
        {
            IHtmlDocument doc = Common.GetHtmlDoc(inputURL);

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
