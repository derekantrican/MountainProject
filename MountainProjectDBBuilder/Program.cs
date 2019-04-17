using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Web;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Net.Mail;
using static MountainProjectDBBuilder.Enums;

namespace MountainProjectDBBuilder
{
    class Program
    {
        static string baseUrl = "https://www.mountainproject.com";
        static string logPath;
        static string logString = "";
        static bool showLogLines = true;
        static string serializationPath;
        static Stopwatch totalTimer = new Stopwatch();
        static Stopwatch areaTimer = new Stopwatch();
        static Exception exception = null;

        static void Main(string[] args)
        {
            logPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss") + " Log.txt");
            serializationPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "MountainProjectAreas.xml");

            if (args.Contains("parse"))
                ParseInputString();
            else
                BuildDB();
        }

        private static void ParseInputString()
        {
            List<DestArea> destAreas = DeserializeAreas(serializationPath);
            if (destAreas.Count() == 0)
            {
                Console.WriteLine("The xml either doesn't exist or is empty");
                Environment.Exit(0);
            }

            Console.WriteLine("File read.");

            string keepSearching = "y";
            while (keepSearching == "y")
            {
                Console.WriteLine("Please input the string you would like to parse: ");
                string input = Console.ReadLine();

                Stopwatch stopwatch = Stopwatch.StartNew();
                Tuple<string, string> result = DeepSearch(input, destAreas);
                stopwatch.Stop();

                if (string.IsNullOrEmpty(result.Item1))
                    Console.WriteLine("Nothing found matching \"" + input + "\"");
                else
                {
                    Console.WriteLine("The following was found: " + result.Item1 + " (Found in " + stopwatch.ElapsedMilliseconds + " ms)");
                    Console.WriteLine("Open result? (y/n) ");
                    if (Console.ReadLine() == "y")
                        Process.Start(result.Item2);
                }

                Console.WriteLine("Search something else? (y/n) ");
                keepSearching = Console.ReadLine();
            }
        }

        private static Tuple<string, string> DeepSearch(string input, List<DestArea> destAreas)
        {
            Tuple<int, string, string> currentResult = new Tuple<int, string, string>(int.MaxValue, "", "");

            foreach (DestArea destArea in destAreas)
            {
                if (input.ToLower().Contains(destArea.Name.ToLower()))
                {
                    //int destAreaSimilarilty = StringMatch(input, destArea.Name);
                    //if (destAreaSimilarilty < currentResult.Item1)
                    //{
                    //    string resultStr = destArea.Name;
                    //    resultStr += " [" + destArea.Statistics.ToString() + "]";
                    //    currentResult = new Tuple<int, string, string>(destAreaSimilarilty, resultStr, destArea.URL);
                    //}

                    //If we're matching the name of a destArea (eg a State), we'll assume that the route/area is within that state
                    currentResult = SearchSubAreasForMatch(input, destArea.SubAreas, new Tuple<int, string, string>(int.MaxValue, "", ""));
                    return new Tuple<string, string>(currentResult.Item2, currentResult.Item3);
                }

                if (destArea.SubAreas != null &&
                    destArea.SubAreas.Count() > 0)
                    currentResult = SearchSubAreasForMatch(input, destArea.SubAreas, currentResult);
            }

            return new Tuple<string, string>(currentResult.Item2, currentResult.Item3);
        }

        private static Tuple<int, string, string> SearchSubAreasForMatch(string input, List<SubDestArea> subAreas, Tuple<int, string, string> currentResult)
        {
            foreach (SubDestArea subDestArea in subAreas)
            {
                if (input.ToLower().Contains(subDestArea.Name.ToLower()))
                {
                    int subDestSimilarilty = StringMatch(input, subDestArea.Name);
                    if (subDestSimilarilty < currentResult.Item1)
                    {
                        string resultStr = subDestArea.Name;
                        resultStr += " [" + subDestArea.Statistics.ToString() + "]";
                        currentResult = new Tuple<int, string, string>(subDestSimilarilty, resultStr, subDestArea.URL);
                    }
                }

                if (subDestArea.SubSubAreas != null &&
                    subDestArea.SubSubAreas.Count() > 0)
                    currentResult = SearchSubAreasForMatch(input, subDestArea.SubSubAreas, currentResult);

                if (subDestArea.Routes != null &&
                    subDestArea.Routes.Count() > 0)
                    currentResult = SearchRoutes(input, subDestArea.Routes, currentResult);
            }

            return currentResult;
        }

        private static Tuple<int, string, string> SearchRoutes(string input, List<Route> routes, Tuple<int, string, string> currentResult)
        {
            List<Route> matches = routes.Where(p => input.ToLower().Contains(p.Name.ToLower())).ToList();

            foreach (Route route in matches)
            {
                int similarity = StringMatch(input, route.Name);
                if (similarity < currentResult.Item1)
                {
                    string resultStr = route.Name;
                    resultStr += " [" + route.Type;
                    resultStr += " " + route.Grade;

                    if (!string.IsNullOrEmpty(route.AdditionalInfo))
                        resultStr += " " + route.AdditionalInfo;

                    resultStr += "]";

                    currentResult = new Tuple<int, string, string>(similarity, resultStr, route.URL);
                }
            }

            return currentResult;
        }

        ///!!!---THIS IS PROBABLY NOT THE BEST WAY TO DO THIS---!!!
        ///(For instance: what if a title of a route is shorter than the area it's in
        ///but both are in the search string? This will probably return a match for the area instead of the route)
        private static int StringMatch(string string1, string string2, bool caseInvariant = true, bool removeWhitespace = true, bool removeNonAlphaNumeric = true)
        {
            if (caseInvariant)
            {
                string1 = string1.ToLower();
                string2 = string2.ToLower();
            }

            if (removeWhitespace)
            {
                string1 = string1.Replace(" ", "");
                string2 = string2.Replace(" ", "");
            }

            if (removeNonAlphaNumeric)
            {
                string1 = Regex.Replace(string1, @"[^a-z\d]", "");
                string2 = Regex.Replace(string2, @"[^a-z\d]", "");
            }

            /// <summary>
            /// Compute the Levenshtein distance between two strings https://www.dotnetperls.com/levenshtein
            /// </summary>
            int n = string1.Length;
            int m = string2.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0)
            {
                return m;
            }

            if (m == 0)
            {
                return n;
            }

            for (int i = 0; i <= n; d[i, 0] = i++)
            {
            }

            for (int j = 0; j <= m; d[0, j] = j++)
            {
            }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (string2[j - 1] == string1[i - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            // Step 7
            return d[n, m];
        }

        private static void BuildDB(bool showLog = true)
        {
            showLogLines = showLog;

            try
            {
                totalTimer.Start();
                JObject resultJson = new JObject();
                HtmlWeb web = new HtmlWeb();
                HtmlDocument doc = web.Load(baseUrl);

                List<DestArea> areas = new List<DestArea>();
                List<HtmlNode> destAreas = doc.DocumentNode.Descendants("a").Where(x => x.Attributes["href"] != null &&
                                                                                        MatchesStateUrlRegex(x.Attributes["href"].Value)).ToList();
                //Filter out duplicates
                destAreas = (from s in destAreas
                             orderby s.InnerText
                             group s by s.Attributes["href"].Value into g
                             select g.First()).ToList();

                //Move international to the end
                HtmlNode internationalArea = destAreas.Find(p => p.InnerText == "International");
                destAreas.Remove(internationalArea);
                destAreas.Add(internationalArea);

                areas = PopulateAreas(destAreas);

                foreach (DestArea area in areas)
                {
                    areaTimer.Restart();
                    Log($"[MAIN] Current Area: {area.Name}");
                    area.Statistics = PopulateStatistics(area.URL);
                    PopulateSubDestAreas(area);
                    foreach (SubDestArea subArea in area.SubAreas)
                    {
                        Log("[MAIN] Current SubArea: " + subArea.Name);
                        Stopwatch subAreaStopwatch = Stopwatch.StartNew();
                        subArea.Statistics = PopulateStatistics(subArea.URL);
                        PopulateRoutes(subArea);

                        Log($"[MAIN] Done with subArea: {subArea.Name} ({subAreaStopwatch.Elapsed})");
                    }

                    Log($"[MAIN] Done with area: {area.Name} ({areaTimer.Elapsed})");
                }

                Log($"[MAIN] ---PROGRAM FINISHED--- ({totalTimer.Elapsed})");
                SerializeResults(areas);
            }
            catch (Exception ex)
            {
                exception = ex;
                Log(Environment.NewLine + Environment.NewLine);
                Log("!!!-------------EXCEPTION ENCOUNTERED-------------!!!");
                Log("EXCEPTION MESSAGE: " + ex?.Message + Environment.NewLine);
                Log("INNER EXCEPTION: " + ex?.InnerException + Environment.NewLine);
                Log("STACK TRACE: " + ex?.StackTrace + Environment.NewLine);
            }
            finally
            {
                SaveLogToFile();
                //SendReport(logPath);
            }
        }

        private static List<DestArea> PopulateAreas(List<HtmlNode> inputNodes)
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

        private static void PopulateSubDestAreas(DestArea inputArea)
        {
            Log("[PopulateSubDestAreas] Populating subAreas for " + inputArea.Name);
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

        private static void PopulateSubSubDestAreas(SubDestArea inputSubArea)
        {
            Stopwatch popSubSubDestAreasStopwatch = Stopwatch.StartNew();
            Log("[PopulateSubSubDestAreas] Populating subAreas for " + inputSubArea.Name);
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

            Log($"[PopulateSubSubDestAreas] Done with subAreas for {inputSubArea.Name} " +
                $"({popSubSubDestAreasStopwatch.Elapsed}. {subAreas.Count} subsubareas, avg {Average(subsubareaParseTimes)})");

            inputSubArea.SubSubAreas = subAreas;
            return;
        }

        private static void PopulateRoutes(SubDestArea inputSubDestArea)
        {
            Stopwatch popRoutesStopwatch = Stopwatch.StartNew();
            Log("[PopulateRoutes] Current subDestArea: " + inputSubDestArea.Name);
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

                    string routeName = node.InnerText;
                    string routeURL = node.Attributes["href"].Value;

                    HtmlWeb routeWeb = new HtmlWeb();
                    HtmlDocument routeDoc = routeWeb.Load(routeURL);

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

                    Route route = new Route(routeName, routeGrade, routeType, routeURL);
                    route.AdditionalInfo = ParseAdditionalRouteInfo(type);

                    if (routes.Where(p => p.URL == route.URL).FirstOrDefault() != null)
                        throw new Exception("Item already exists in routes list: " + route.URL);

                    routes.Add(route);
                    routeParseTimes.Add(routeStopwatch.Elapsed);
                }
            }

            Log($"[PopulateRoutes] Done with subDestArea: {inputSubDestArea.Name} " +
                $"({popRoutesStopwatch.Elapsed}. {routes.Count} routes, avg {Average(routeParseTimes)})");

            inputSubDestArea.Routes = routes;
            return;
        }

        private static AreaStats PopulateStatistics(string inputURL)
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

        private static Route.RouteType ParseRouteType(string inputString)
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

        private static string ParseAdditionalRouteInfo(string inputString)
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

        private static void SerializeResults(List<DestArea> inputAreas)
        {
            Log("[SerializeResults] Serializing areas to file");
            TextWriter writer = new StreamWriter(serializationPath);
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<DestArea>));
            xmlSerializer.Serialize(writer, inputAreas);
            writer.Close();
        }

        private static List<DestArea> DeserializeAreas(string xmlFilePath)
        {
            if (File.Exists(xmlFilePath))
            {
                Log("[DeserializeAreas] Deserializing areas from: " + xmlFilePath);
                FileStream fileStream = new FileStream(xmlFilePath, FileMode.Open);
                XmlSerializer xmlDeserializer = new XmlSerializer(typeof(List<DestArea>));
                return (List<DestArea>)xmlDeserializer.Deserialize(fileStream);
            }
            else
            {
                Log("[DeserializeAreas] The file " + xmlFilePath + " does not exist");
                return new List<DestArea>();
            }
        }

        private static void SendReport(string logPath)
        {
            try
            {
                Log("[SendReport] Sending report");
                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
                mail.From = new MailAddress("derekantrican@gmail.com");
                mail.To.Add("derekantrican@gmail.com");

                if (exception != null)
                {
                    mail.Subject = "MountainProjectDBBuilder FAILED to finish";
                    mail.Body = "MountainProjectDBBuilder failed to finish successfully. Here is the exception information:";
                    mail.Body += Environment.NewLine + Environment.NewLine;
                    mail.Body += "EXCEPTION MESSAGE: " + exception.Message + Environment.NewLine;
                    mail.Body += "INNER EXCEPTION: " + exception.InnerException + Environment.NewLine;
                    mail.Body += "STACK TRACE: " + exception.StackTrace + Environment.NewLine;
                }
                else
                {
                    mail.Subject = "MountainProjectDBBuilder successfully finished";
                    mail.Body = "See attached files for report";
                }

                if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                {
                    Attachment logAttachment = new Attachment(logPath);
                    mail.Attachments.Add(logAttachment);
                }

                ////XML is too big to send so will just send the log for now
                //if (!string.IsNullOrEmpty(serializedAreasPath) && File.Exists(serializedAreasPath))
                //{
                //    Attachment areasAttachment = new Attachment(serializedAreasPath);
                //    mail.Attachments.Add(areasAttachment);
                //}

                SmtpServer.Port = 587;
                SmtpServer.Credentials = new NetworkCredential("derekantrican@gmail.com", ""/*This is where you would put the email password*/);
                SmtpServer.EnableSsl = true;

                SmtpServer.Send(mail);
            }
            catch (Exception ex)
            {
                Log("[SendReport] Could not send email: " + ex.Message);
            }
        }

        private static void Log(string itemToLog)
        {
            logString += itemToLog + "\n";

            if (showLogLines)
                Console.WriteLine(itemToLog);
        }

        private static void SaveLogToFile()
        {
            if (!File.Exists(logPath))
                File.Create(logPath).Close();

            File.AppendAllText(logPath, logString);
        }

        private static bool MatchesStateUrlRegex(string urlToMatch)
        {
            List<string> states = new List<string>()
            {
                "Alabama",
                "Alaska",
                "Arizona",
                "Arkansas",
                "California",
                "Colorado",
                "Connecticut",
                "Delaware",
                "Florida",
                "Georgia",
                "Hawaii",
                "Idaho",
                "Illinois",
                "Indiana",
                "Iowa",
                "Kansas",
                "Kentucky",
                "Louisiana",
                "Maine",
                "Maryland",
                "Massachusetts",
                "Michigan",
                "Minnesota",
                "Mississippi",
                "Missouri",
                "Montana",
                "Nebraska",
                "Nevada",
                "New-Hampshire",
                "New-Jersey",
                "New-Mexico",
                "New-York",
                "North-Carolina",
                "North-Dakota",
                "Ohio",
                "Oklahoma",
                "Oregon",
                "Pennsylvania",
                "Rhode-Island",
                "South-Carolina",
                "South-Dakota",
                "Tennessee",
                "Texas",
                "Utah",
                "Vermont",
                "Virginia",
                "Washington",
                "West-Virginia",
                "Wisconsin",
                "Wyoming",
                "International"
            };

            foreach (string state in states)
            {
                Regex stateRegex = new Regex(RegexSanitize(baseUrl) + "\\/area\\/\\d*\\/" + state.ToLower() + "$");
                if (stateRegex.IsMatch(urlToMatch))
                    return true;
            }

            return false;
        }

        private static string RegexSanitize(string input)
        {
            return input.Replace("/", "\\/").Replace(".", "\\.");
        }

        private static TimeSpan Average(List<TimeSpan> timeSpanList)
        {
            if (timeSpanList.Count == 0)
                return new TimeSpan();

            double doubleAverageTicks = timeSpanList.Average(timeSpan => timeSpan.Ticks);
            long longAverageTicks = Convert.ToInt64(doubleAverageTicks);

            return new TimeSpan(longAverageTicks);
        }
    }
}
