using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Net.Mail;
using static MountainProjectDBBuilder.Enums;
using System.Threading.Tasks;

namespace MountainProjectDBBuilder
{
    class Program
    {
        static string serializationPath;
        static Stopwatch totalTimer = new Stopwatch();
        static Stopwatch areaTimer = new Stopwatch();
        static Exception exception = null;

        static void Main(string[] args)
        {
            Common.LogPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss") + " Log.txt");
            serializationPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "MountainProjectAreas.xml");

            if (args.Contains("parse"))
                ParseInputString();
            else
                BuildDB();
        }

        private static void ParseInputString()
        {
            List<Area> destAreas = DeserializeAreas(serializationPath);
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

        private static Tuple<string, string> DeepSearch(string input, List<Area> destAreas)
        {
            Tuple<int, string, string> currentResult = new Tuple<int, string, string>(int.MaxValue, "", "");

            foreach (Area destArea in destAreas)
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

        private static Tuple<int, string, string> SearchSubAreasForMatch(string input, List<Area> subAreas, Tuple<int, string, string> currentResult)
        {
            foreach (Area subDestArea in subAreas)
            {
                if (input.ToLower().Contains(subDestArea.Name.ToLower()))
                {
                    ///!!!---THIS IS PROBABLY NOT THE BEST WAY TO DO THIS---!!!
                    ///(For instance: what if a title of a route is shorter than the area it's in
                    ///but both are in the search string? This will probably return a match for the area instead of the route)
                    int subDestSimilarilty = Common.StringMatch(input, subDestArea.Name);

                    if (subDestSimilarilty < currentResult.Item1)
                    {
                        string resultStr = subDestArea.Name;
                        resultStr += " [" + subDestArea.Statistics.ToString() + "]";
                        currentResult = new Tuple<int, string, string>(subDestSimilarilty, resultStr, subDestArea.URL);
                    }
                }

                if (subDestArea.SubAreas != null &&
                    subDestArea.SubAreas.Count() > 0)
                    currentResult = SearchSubAreasForMatch(input, subDestArea.SubAreas, currentResult);

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
                ///!!!---THIS IS PROBABLY NOT THE BEST WAY TO DO THIS---!!!
                ///(For instance: what if a title of a route is shorter than the area it's in
                ///but both are in the search string? This will probably return a match for the area instead of the route)
                int similarity = Common.StringMatch(input, route.Name);

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

        private static void BuildDB(bool showLogLines = true)
        {
            Common.ShowLogLines = showLogLines;

            try
            {
                totalTimer.Start();

                List<Area> destAreas = Parsers.GetDestAreas();
                List<Task> areaTasks = new List<Task>();
                foreach (Area destArea in destAreas)
                    areaTasks.Add(Parsers.ParseAreaAsync(destArea, state: destArea.Name));

                Task.WaitAll(areaTasks.ToArray());

                Common.Log($"[MAIN] ---PROGRAM FINISHED--- ({totalTimer.Elapsed})");
                Console.Read();
                SerializeResults(destAreas);
                Console.Read();
            }
            catch (Exception ex)
            {
                exception = ex;
                Common.Log(Environment.NewLine + Environment.NewLine);
                Common.Log("!!!-------------EXCEPTION ENCOUNTERED-------------!!!");
                Common.Log("EXCEPTION MESSAGE: " + ex?.Message + Environment.NewLine);
                Common.Log("INNER EXCEPTION: " + ex?.InnerException + Environment.NewLine);
                Common.Log("STACK TRACE: " + ex?.StackTrace + Environment.NewLine);
            }
            finally
            {
                Common.SaveLogToFile();
                //SendReport(logPath);
            }
        }

        private static void SerializeResults(List<Area> inputAreas)
        {
            Common.Log("[SerializeResults] Serializing areas to file");
            TextWriter writer = new StreamWriter(serializationPath);
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Area>));
            xmlSerializer.Serialize(writer, inputAreas);
            writer.Close();
        }

        private static List<Area> DeserializeAreas(string xmlFilePath)
        {
            if (File.Exists(xmlFilePath))
            {
                Common.Log("[DeserializeAreas] Deserializing areas from: " + xmlFilePath);
                FileStream fileStream = new FileStream(xmlFilePath, FileMode.Open);
                XmlSerializer xmlDeserializer = new XmlSerializer(typeof(List<Area>));
                return (List<Area>)xmlDeserializer.Deserialize(fileStream);
            }
            else
            {
                Common.Log("[DeserializeAreas] The file " + xmlFilePath + " does not exist");
                return new List<Area>();
            }
        }

        private static void SendReport(string logPath)
        {
            try
            {
                Common.Log("[SendReport] Sending report");
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
                Common.Log("[SendReport] Could not send email: " + ex.Message);
            }
        }
    }
}
