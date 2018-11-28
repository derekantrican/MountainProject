﻿using HtmlAgilityPack;
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

namespace MountainProjectDBBuilder
{
    class Program
    {
        static string baseUrl = "https://www.mountainproject.com";
        static string logPath;
        static string serializationPath;
        static Stopwatch totalTimer = new Stopwatch();
        static Stopwatch areaTimer = new Stopwatch();
        static Exception exception = null;

        static void Main(string[] args)
        {
            try
            {
                logPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss") + " Log.txt");
                serializationPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "MountainProjectAreas.xml");
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
                    Log("[MAIN] Current Area: " + area.Name);
                    area.Statistics = PopulateStatistics(area.URL);
                    PopulateSubDestAreas(area);
                    foreach (SubDestArea subArea in area.SubAreas)
                    {
                        Log("[MAIN] Current SubArea: " + subArea.Name);
                        subArea.Statistics = PopulateStatistics(subArea.URL);
                        PopulateRoutes(subArea);
                        Log("[MAIN] Done with subArea: " + subArea.Name);
                    }

                    Log("[MAIN] Done with area: " + area.Name + " (" + areaTimer.ElapsedMilliseconds + " ms)");
                }

                Log("[MAIN] ---PROGRAM FINISHED--- (" + totalTimer.ElapsedMilliseconds + " ms)");
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
            Log("[PopulateSubSubDestAreas] Populating subAreas for " + inputSubArea.Name);
            List<SubDestArea> subAreas = new List<SubDestArea>();

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(inputSubArea.URL);

            HtmlNode leftColumnDiv = doc.DocumentNode.Descendants("div").Where(p => p.Attributes["class"] != null && p.Attributes["class"].Value == "mp-sidebar").FirstOrDefault();
            List<HtmlNode> htmlSubAreas = doc.DocumentNode.Descendants("a").Where(p => p.ParentNode.ParentNode.ParentNode == leftColumnDiv).ToList();
            htmlSubAreas.RemoveAll(p => (p.ParentNode.ParentNode.Attributes["id"] != null && p.ParentNode.ParentNode.Attributes["id"].Value == "nearbyMTBRides") ||
                                        (p.Attributes["href"] != null && p.Attributes["href"].Value == "#"));

            foreach (HtmlNode node in htmlSubAreas)
            {
                SubDestArea subArea = new SubDestArea(node.InnerText, node.Attributes["href"].Value);

                if (subAreas.Where(p => p.URL == subArea.URL).FirstOrDefault() != null)
                    throw new Exception("Item already exists in subAreas list: " + subArea.URL);

                subAreas.Add(subArea);
            }

            inputSubArea.SubSubAreas = subAreas;
        }

        private static void PopulateRoutes(SubDestArea inputSubDestArea)
        {
            Log("[PopulateRoutes] Current subDestArea: " + inputSubDestArea.Name);
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
                }
            }

            inputSubDestArea.Routes = routes;
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
                SmtpServer.Credentials = new NetworkCredential("derekantrican@gmail.com", "ylbykoqjmbqtismk");
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
            Console.WriteLine(itemToLog);

            if (!File.Exists(logPath))
                File.Create(logPath).Close();

            File.AppendAllText(logPath, itemToLog + Environment.NewLine);
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
    }
}
