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
        public static List<Area> GetDestAreas(bool recursive = true)
        {
            List<Area> destAreas = new List<Area>();

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
            foreach (IElement destAreaElement in destAreaNodes)
            {
                Area destArea = new Area()
                {
                    Name = destAreaElement.TextContent,
                    URL = destAreaElement.Attributes["href"].Value
                };

                destAreas.Add(destArea);

                if (recursive)
                    ParseArea(destArea); //Parse dest area
            }

            doc.Dispose();

            return destAreas;
        }

        public static void ParseArea(Area inputArea, bool recursive = true)
        {
            Stopwatch areaStopwatch = Stopwatch.StartNew();
            IHtmlDocument doc = Common.GetHtmlDoc(inputArea.URL);

            if (string.IsNullOrEmpty(inputArea.Name))
                inputArea.Name = Regex.Replace(doc.GetElementsByTagName("h1").FirstOrDefault().TextContent, @"<[^>]*>", "").Replace("\n", "").Trim();

            inputArea.Statistics = PopulateStatistics(inputArea.URL);

            //Get Area's routes
            IElement routesTable = doc.GetElementsByTagName("table").Where(p => p.Attributes["id"] != null && p.Attributes["id"].Value == "left-nav-route-table").FirstOrDefault();
            List<IElement> htmlRoutes = routesTable == null ? new List<IElement>() : routesTable.GetElementsByTagName("a").ToList();

            //Get Area's areas
            IElement leftColumnDiv = doc.GetElementsByTagName("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
            List<IElement> htmlSubAreas = doc.GetElementsByTagName("a").Where(p => p.ParentElement.ParentElement.ParentElement == leftColumnDiv).ToList();
            htmlSubAreas.RemoveAll(p => p.ParentElement.ParentElement.Attributes["id"] != null && p.ParentElement.ParentElement.Attributes["id"].Value == "nearbyMTBRides");
            htmlSubAreas.RemoveAll(p => !p.Attributes["href"].Value.Contains(Common.BaseUrl));

            //Dispose doc
            doc.Dispose();

            //Populate route details
            foreach (IElement routeElement in htmlRoutes)
            {
                Route route = new Route() { Name = routeElement.TextContent, URL = routeElement.Attributes["href"].Value };
                inputArea.Routes.Add(route);
                ParseRoute(route); //Parse route
            }

            //Populate sub area details
            foreach (IElement areaElement in htmlSubAreas)
            {
                Area subArea = new Area() { Name = areaElement.TextContent, URL = areaElement.Attributes["href"].Value };
                inputArea.SubAreas.Add(subArea);

                if (recursive)
                    ParseArea(subArea); //Parse sub area
            }

            Common.Log($"Done with Area: {inputArea.Name} ({areaStopwatch.Elapsed}). {htmlRoutes.Count} routes, {htmlSubAreas.Count} subareas");
        }

        public static void ParseRoute(Route inputRoute)
        {
            Stopwatch routeStopwatch = Stopwatch.StartNew();
            IHtmlDocument doc = Common.GetHtmlDoc(inputRoute.URL);

            if (string.IsNullOrEmpty(inputRoute.Name))
                inputRoute.Name = Regex.Replace(doc.GetElementsByTagName("h1").FirstOrDefault().TextContent, @"<[^>]*>", "").Replace("\n", "").Trim();

            //Get Route type
            string type = HttpUtility.HtmlDecode(doc.GetElementsByTagName("tr").Where(p => p.GetElementsByTagName("td").FirstOrDefault().TextContent.Contains("Type:")).FirstOrDefault()
                                        .GetElementsByTagName("td").ToList()[1].TextContent).Trim();
            inputRoute.Type = ParseRouteType(type);

            //Get Route grade
            List<IElement> gradesOnPage = doc.GetElementsByTagName("span").Where(x => x.Attributes["class"] != null &&
                                                                                               (x.Attributes["class"].Value == "rateHueco" || x.Attributes["class"].Value == "rateYDS")).ToList();
            IElement sidebar = doc.GetElementsByTagName("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
            gradesOnPage.RemoveAll(p => sidebar.Descendents().Contains(p));
            string routeGrade = "";
            IElement gradeElement = gradesOnPage.FirstOrDefault();
            if (gradeElement != null)
                routeGrade = HttpUtility.HtmlDecode(gradeElement.TextContent.Replace(gradeElement.GetElementsByTagName("a").FirstOrDefault().TextContent, "")).Trim();

            inputRoute.Grade = routeGrade;

            inputRoute.AdditionalInfo = ParseAdditionalRouteInfo(type); //Get Route additional info

            doc.Dispose();

            Common.Log($"Done with Route: {inputRoute.Name} ({routeStopwatch.Elapsed})");
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
