using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Threading.Tasks;
using Mono.Options;
using MountainProjectAPI;
using System.Collections.Concurrent;
using System.Xml;
using System.ServiceModel.Syndication;

namespace MountainProjectDBBuilder
{
    public enum Mode
    {
        None,
        Parse,
        BuildDB
    }
    
    class Program
    {
        static string serializationPath;
        static string logPath;
        static OutputCapture outputCapture;
        static Stopwatch totalTimer = new Stopwatch();
        static Mode programMode = Mode.None;
        static bool buildAll = true;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            outputCapture = new OutputCapture();

            logPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss") + " Log.txt");
            serializationPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "MountainProjectAreas.xml");

            ParseStartupArguments(args);

            switch (programMode)
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
                        (arg) => { programMode = Mode.BuildDB; }
                    },
                    {
                        "parse",
                        "Parse an input string",
                        (arg) => { programMode = Mode.Parse; }
                    },
                    { 
                        "onlyNew",
                        "Only add new items since the last time the database was built",
                        (arg) => { buildAll = false; }
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
            MountainProjectDataSearch.InitMountainProjectData(serializationPath);
            if (MountainProjectDataSearch.DestAreas.Count() == 0)
            {
                Console.WriteLine("The xml either doesn't exist or is empty");
                Environment.Exit(0);
            }

            Console.WriteLine("File read.");

            bool keepSearching = true;
            while (keepSearching)
            {
                Console.WriteLine("\n\nPlease input the string you would like to parse: ");
                string input = Console.ReadLine();

                SearchParameters searchParameters = SearchParameters.ParseParameters(ref input);
                ResultParameters resultParameters = ResultParameters.ParseParameters(ref input);

                bool allResults = input.Contains("-all");
                if (allResults)
                    input = input.Replace("-all", "").Trim();

                Stopwatch stopwatch = Stopwatch.StartNew();
                SearchResult searchResult = MountainProjectDataSearch.Search(input, searchParameters);
                stopwatch.Stop();

                if (searchResult.IsEmpty())
                    Console.WriteLine("Nothing found matching \"" + input + "\"");
                else if (allResults)
                {
                    List<MPObject> matchedObjectsByPopularity = searchResult.AllResults.OrderByDescending(p => p.Popularity).ToList();
                    Console.WriteLine($"Found {matchedObjectsByPopularity.Count} items match that search query (found in {stopwatch.ElapsedMilliseconds} ms):");
                    foreach (MPObject result in matchedObjectsByPopularity)
                    {
                        string url = result.URL.Replace(Utilities.MPBASEURL, "");
                        if (result is Route)
                            Console.WriteLine($"    Route: {result.Name} (Pop: {result.Popularity}) | Location: {GetLocationString(result)} | {url}");
                        else if (result is Area)
                            Console.WriteLine($"    Area: {result.Name} (Pop: {result.Popularity}) | Location: {GetLocationString(result)} | {url}");
                    }
                }
                else
                {
                    string resultStr = "";
                    MPObject result = searchResult.FilteredResult;
                    if (result is Area)
                        resultStr = (result as Area).ToString();
                    else if (result is Route)
                        resultStr = (result as Route).ToString(resultParameters);

                    Console.WriteLine($"The following was found (found in {stopwatch.ElapsedMilliseconds} ms):");
                    Console.WriteLine("    " + resultStr);
                    Console.WriteLine($"    Location: {GetLocationString(result, searchResult.RelatedLocation)}");
                    Console.WriteLine("\nOpen result? (y/n) ");
                    if (Console.ReadLine().ToLower() == "y")
                        Process.Start(result.URL);
                }

                Console.WriteLine("\nSearch something else? (y/n) ");
                keepSearching = Console.ReadLine().ToLower() == "y";
            }
        }

        public static string GetLocationString(MPObject child, Area referenceLocation = null)
        {
            MPObject innerParent = MountainProjectDataSearch.GetInnerParent(child);
            MPObject outerParent = MountainProjectDataSearch.GetOuterParent(child);

            if (referenceLocation != null) //Override the "innerParent" in situations where we want the location string to include the "insisted" location
            {
                //Only override if the location is not already present
                if (innerParent?.URL != referenceLocation.URL &&
                    outerParent?.URL != referenceLocation.URL)
                {
                    innerParent = referenceLocation;
                }
            }

            if (innerParent == null)
                return "";

            string locationString = $"Located in {innerParent.Name}";
            if (outerParent != null && outerParent.URL != innerParent.URL)
                locationString += $", {outerParent.Name}";

            return locationString;
        }

        private static void BuildDB()
        {
            try
            {
                totalTimer.Start();
                Console.WriteLine("Starting DB Build...");

                Parsers.TotalTimer = totalTimer;
                List<Area> destAreas = Parsers.GetDestAreas();

                if (!buildAll && File.Exists(serializationPath))
                {
                    DateTime lastBuild = File.GetLastWriteTime(serializationPath);
                    string rssUrl = $"https://www.mountainproject.com/rss/new?selectedIds={string.Join(",", destAreas.Select(p => p.ID))}&routes=on&areas=on";
                    SyndicationFeed feed = null;
                    using (XmlReader reader = XmlReader.Create(rssUrl))
                    {
                        feed = SyndicationFeed.Load(reader);
                    }

                    IEnumerable<string> newlyAddedItemUrls = feed.Items.Where(p => p.PublishDate > lastBuild).OrderBy(p => p.PublishDate).Select(p => p.Links[0].Uri.ToString());
                    MountainProjectDataSearch.InitMountainProjectData(serializationPath);

                    foreach (string newItemUrl in newlyAddedItemUrls)
                    {
                        string newId = Utilities.GetID(newItemUrl);

                        if (MountainProjectDataSearch.GetItemWithMatchingID(newId) != null) //Item has already been added (probably via a recursive area add)
                            continue;

                        MPObject newItem;
                        if (newItemUrl.Contains(Utilities.MPAREAURL))
                        {
                            newItem = new Area { ID = newId };
                            Parsers.ParseAreaAsync(newItem as Area).Wait();
                        }
                        else
                        {
                            newItem = new Route { ID = newId };
                            Parsers.ParseRouteAsync(newItem as Route).Wait();
                        }

                        Area currentParent = null;
                        bool itemAddedViaRecursiveParse = false;
                        foreach (string parentId in newItem.ParentIDs) //Make sure all parents are populated
                        {
                            MPObject matchingItem = MountainProjectDataSearch.GetItemWithMatchingID(parentId);
                            if (matchingItem == null)
                            {
                                Area newArea = new Area { ID = parentId };
                                Parsers.ParseAreaAsync(newArea).Wait();
                                currentParent.SubAreas.Add(newArea);
                                itemAddedViaRecursiveParse = true;
                                break;
                            }
                            else
                                currentParent = matchingItem as Area;
                        }

                        if (!itemAddedViaRecursiveParse)
                        {
                            if (newItem is Area)
                                (MountainProjectDataSearch.GetItemWithMatchingID(newItem.ParentIDs.Last()) as Area).SubAreas.Add(newItem as Area);
                            else
                                (MountainProjectDataSearch.GetItemWithMatchingID(newItem.ParentIDs.Last()) as Area).Routes.Add(newItem as Route);
                        }
                    }

                    destAreas = MountainProjectDataSearch.DestAreas;
                }
                else
                {
                    Parsers.TargetTotalRoutes = Parsers.GetTargetTotalRoutes();
                    ConcurrentBag<Task> areaTasks = new ConcurrentBag<Task>();
                    Parallel.ForEach(destAreas, destArea =>
                    {
                        areaTasks.Add(Parsers.ParseAreaAsync(destArea));
                    });

                    Task.WaitAll(areaTasks.ToArray());
                }

                totalTimer.Stop();
                Console.WriteLine(outputCapture.Captured.ToString());
                Console.WriteLine($"------PROGRAM FINISHED------ ({totalTimer.Elapsed})");
                Console.WriteLine();
                Console.WriteLine($"Total # of areas: {Parsers.TotalAreas}, total # of routes: {Parsers.TotalRoutes}");
                SerializeResults(destAreas);

                if (buildAll)
                    SendReport($"MountainProjectDBBuilder completed SUCCESSFULLY in {totalTimer.Elapsed}. Total areas: {Parsers.TotalAreas}, total routes: {Parsers.TotalRoutes}", "");
                else
                    SendReport($"MountainProjectDBBuilder database updated SUCCESSFULLY in {totalTimer.Elapsed}", "");
            }
            catch (Exception ex)
            {
                Console.WriteLine(Environment.NewLine + Environment.NewLine);
                Console.WriteLine("!!!-------------EXCEPTION ENCOUNTERED-------------!!!");
                Console.WriteLine($"EXCEPTION MESSAGE: {ex?.Message}\n");
                Console.WriteLine($"INNER EXCEPTION: {ex?.InnerException}\n");
                Console.WriteLine($"STACK TRACE: {ex?.StackTrace}\n");
                SendReport($"MountainProjectDBBuilder completed WITH ERRORS in {totalTimer.Elapsed}",
                    $"{ex?.Message}\n{ex.InnerException}\n{ex?.StackTrace}");
            }
            finally
            {
                File.AppendAllText(logPath, outputCapture.Captured.ToString());
                outputCapture.Dispose();
            }
        }

        private static void SerializeResults(List<Area> inputAreas)
        {
            Console.WriteLine("[SerializeResults] Serializing areas to file");
            TextWriter writer = new StreamWriter(serializationPath);
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Area>));
            xmlSerializer.Serialize(writer, inputAreas);
            writer.Close();
        }

        private static void SendReport(string subject, string message)
        {
            try
            {
                Console.WriteLine("[SendReport] Sending report");
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
                Console.WriteLine("[SendReport] Could not send email: " + ex.Message);
            }
        }
    }
}
