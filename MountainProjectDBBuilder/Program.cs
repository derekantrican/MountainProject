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
using Mono.Options;
using MountainProjectModels;
using Common;

namespace MountainProjectDBBuilder
{
    class Program
    {
        static string serializationPath;
        static Stopwatch totalTimer = new Stopwatch();
        static Mode ProgramMode = Mode.None;

        static void Main(string[] args)
        {
            Utilities.LogPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss") + " Log.txt");
            serializationPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "MountainProjectAreas.xml");

            ParseStartupArguments(args);

            switch (ProgramMode)
            {
                case Mode.BuildDB:
                case Mode.None:
                    BuildDB();
                    break;
                case Mode.Parse:
                    ParseInputString();
                    break;
            }
        }

        private static void ParseStartupArguments(string[] args)
        {
            if (args.Length > 0)
            {
                bool help = false;

                var p = new OptionSet()
                {
                    {
                        "h|help|?",
                        "Show help",
                        v => help = v != null
                    },
                    {
                        "build",
                        "Build xml from MountainProject",
                        (arg) => { ProgramMode = Mode.BuildDB; }
                    },
                    {
                        "parse",
                        "Parse an input string",
                        (arg) => { ProgramMode = Mode.Parse; }
                    }
                };

                p.Parse(args);
                if (help)
                {
                    p.WriteOptionDescriptions(Console.Out);
                    Environment.Exit(0);
                }
            }
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
                MPObject result = DeepSearch(input, destAreas);
                stopwatch.Stop();

                if (string.IsNullOrEmpty(result.URL))
                    Console.WriteLine("Nothing found matching \"" + input + "\"");
                else
                {
                    string resultStr = "";
                    if (result is Area)
                        resultStr = $"{result.Name} [{(result as Area).Statistics.ToString()}]";
                    else if (result is Route)
                    {
                        Route resultRoute = result as Route;
                        resultStr = $"{resultRoute.Name} [{resultRoute.Type} {resultRoute.Grade}";

                        if (!string.IsNullOrEmpty(resultRoute.AdditionalInfo))
                            resultStr += " " + resultRoute.AdditionalInfo;

                        resultStr += "]";
                    }

                    Console.WriteLine("The following was found: " + resultStr + " (Found in " + stopwatch.ElapsedMilliseconds + " ms)");
                    Console.WriteLine("Open result? (y/n) ");
                    if (Console.ReadLine() == "y")
                        Process.Start(result.URL);
                }

                Console.WriteLine("Search something else? (y/n) ");
                keepSearching = Console.ReadLine();
            }
        }

        private static MPObject DeepSearch(string input, List<Area> destAreas)
        {
            Tuple<MPObject, int> currentResult = new Tuple<MPObject, int>(new MPObject(), int.MaxValue);

            foreach (Area destArea in destAreas)
            {
                if (input.ToLower().Contains(destArea.Name.ToLower()))
                {
                    //If we're matching the name of a destArea (eg a State), we'll assume that the route/area is within that state
                    currentResult = SearchSubAreasForMatch(input, destArea.SubAreas, new Tuple<MPObject, int>(new MPObject(), int.MaxValue));
                    return currentResult.Item1;
                }

                if (destArea.SubAreas != null &&
                    destArea.SubAreas.Count() > 0)
                    currentResult = SearchSubAreasForMatch(input, destArea.SubAreas, currentResult);
            }

            return currentResult.Item1;
        }

        private static Tuple<MPObject, int> SearchSubAreasForMatch(string input, List<Area> subAreas, Tuple<MPObject, int> currentResult)
        {
            foreach (Area subDestArea in subAreas)
            {
                if (input.ToLower().Contains(subDestArea.Name.ToLower()))
                {
                    ///!!!---THIS IS PROBABLY NOT THE BEST WAY TO DO THIS---!!!
                    ///(For instance: what if a title of a route is shorter than the area it's in
                    ///but both are in the search string? This will probably return a match for the area instead of the route)
                    int subDestSimilarilty = Utilities.StringMatch(input, subDestArea.Name);

                    if (subDestSimilarilty < currentResult.Item2)
                        currentResult = new Tuple<MPObject, int>(subDestArea, subDestSimilarilty);
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

        private static Tuple<MPObject, int> SearchRoutes(string input, List<Route> routes, Tuple<MPObject, int> currentResult)
        {
            List<Route> matches = routes.Where(p => input.ToLower().Contains(p.Name.ToLower())).ToList();

            foreach (Route route in matches)
            {
                ///!!!---THIS IS PROBABLY NOT THE BEST WAY TO DO THIS---!!!
                ///(For instance: what if a title of a route is shorter than the area it's in
                ///but both are in the search string? This will probably return a match for the area instead of the route)
                int similarity = Utilities.StringMatch(input, route.Name);

                if (similarity < currentResult.Item2)
                    currentResult = new Tuple<MPObject, int>(route, similarity);
            }

            return currentResult;
        }

        private static void BuildDB(bool showLogLines = true)
        {
            Utilities.ShowLogLines = showLogLines;

            try
            {
                totalTimer.Start();
                Utilities.Log("Starting DB Build...");

                List<Area> destAreas = Parsers.GetDestAreas();
                List<Task> areaTasks = new List<Task>();
                Parallel.ForEach(destAreas, destArea =>
                {
                    areaTasks.Add(Parsers.ParseAreaAsync(destArea));
                });

                Task.WaitAll(areaTasks.ToArray());

                totalTimer.Stop();
                Utilities.Log($"------PROGRAM FINISHED------ ({totalTimer.Elapsed})");
                SerializeResults(destAreas);
                SendReport($"MountainProjectDBBuilder completed SUCCESSFULLY in {totalTimer.Elapsed}", "");
            }
            catch (Exception ex)
            {

                Utilities.Log(Environment.NewLine + Environment.NewLine);
                Utilities.Log("!!!-------------EXCEPTION ENCOUNTERED-------------!!!");
                Utilities.Log($"EXCEPTION MESSAGE: {ex?.Message}\n");
                Utilities.Log($"INNER EXCEPTION: {ex?.InnerException}\n");
                Utilities.Log($"STACK TRACE: {ex?.StackTrace}\n");
                SendReport($"MountainProjectDBBuilder completed WITH ERRORS in {totalTimer.Elapsed}",
                    $"{ex?.Message}\n{ex.InnerException}\n{ex?.StackTrace}");
            }
            finally
            {
                Utilities.SaveLogToFile();
            }
        }

        private static void SerializeResults(List<Area> inputAreas)
        {
            Utilities.Log("[SerializeResults] Serializing areas to file");
            TextWriter writer = new StreamWriter(serializationPath);
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Area>));
            xmlSerializer.Serialize(writer, inputAreas);
            writer.Close();
        }

        private static List<Area> DeserializeAreas(string xmlFilePath)
        {
            if (File.Exists(xmlFilePath))
            {
                Utilities.Log("[DeserializeAreas] Deserializing areas from: " + xmlFilePath);
                FileStream fileStream = new FileStream(xmlFilePath, FileMode.Open);
                XmlSerializer xmlDeserializer = new XmlSerializer(typeof(List<Area>));
                return (List<Area>)xmlDeserializer.Deserialize(fileStream);
            }
            else
            {
                Utilities.Log("[DeserializeAreas] The file " + xmlFilePath + " does not exist");
                return new List<Area>();
            }
        }

        private static void SendReport(string subject, string message)
        {
            try
            {
                Utilities.Log("[SendReport] Sending report");
                string url = @"https://script.google.com/macros/s/AKfycbzSbnYebCUPam1CkMgkD65LzTF_EQIbxFAGBeSZpqS4Shg36m8/exec?";
                url += $"subjectonly={Uri.EscapeDataString(subject)}&messageonly={Uri.EscapeDataString(message)}";

                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(url);
                httpRequest.AutomaticDecompression = DecompressionMethods.GZip;

                using (HttpWebResponse webResponse = (HttpWebResponse)httpRequest.GetResponse())
                using (Stream stream = webResponse.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string response = reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                Utilities.Log("[SendReport] Could not send email: " + ex.Message);
            }
        }
    }
}
