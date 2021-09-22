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
using System.Xml;
using System.ServiceModel.Syndication;
using Base;
using System.Collections.Concurrent;
using System.Threading;
using Newtonsoft.Json;

namespace MountainProjectDBBuilder
{
    public enum Mode
    {
        None,
        Parse,
        BuildDB,
        DownloadFile,
        Benchmark
    }

    public enum FileType
    {
        XML,
        JSON
    }

    class Program
    {
        private static string serializationPath;
        private static string logPath;
        private static readonly StringWriter outputCapture = new StringWriter();
        private static readonly Stopwatch totalTimer = new Stopwatch();
        private static Mode programMode = Mode.None;
        private static FileType fileType = FileType.XML;
        private static bool buildAll = true;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            ConsoleHelper.WriteToAdditionalTarget(outputCapture);

            logPath = $"{DateTime.Now:yyyy.MM.dd.HH.mm.ss} Log.txt";
            serializationPath = "MountainProjectAreas.xml";

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
                case Mode.DownloadFile:
                    break;
            }
        }

        private static void ParseStartupArguments(string[] args)
        {
            if (args.Length > 0)
            {
                bool help = false;

                var p = new OptionSet() //Todo: should implement better cmd arg parsing like https://github.com/commandlineparser/commandline
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
                        "filetype=",
                        "File type to serialize as (xml or json - xml is default)",
                        (arg) => { fileType = (FileType)Enum.Parse(typeof(FileType), arg); }
                    },
                    {
                        "onlyNew",
                        "Only add new items since the last time the database was built",
                        (arg) => { buildAll = false; }
                    },
                    {
                        "download=",
                        "Download xml file from Google Drive",
                        (arg) =>
                        {
                            DownloadGoogleDriveFileFromUrl(arg);
                            Environment.Exit(0);
                        }
                    },
                    {
                        "benchmark",
                        "Run back-to-back benchmark test",
                        (arg) => { programMode = Mode.Benchmark; }
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

                if (!buildAll && File.Exists(serializationPath))
                    AddNewItems();
                else
                    BuildFullDB();
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
                File.AppendAllText(logPath, outputCapture.ToString());
                outputCapture.Dispose();
            }
        }

        private static void BuildFullDB()
        {
            Parsers.TotalTimer = totalTimer;
            Parsers.TargetTotalRoutes = Parsers.GetTargetTotalRoutes();
            List<Area> destAreas = Parsers.GetDestAreas();

            ConcurrentBag<Task> areaTasks = new ConcurrentBag<Task>();
            Parallel.ForEach(destAreas, destArea =>
            {
                areaTasks.Add(Parsers.ParseAreaAsync(destArea));
            });

            Task.WaitAll(areaTasks.ToArray());

            totalTimer.Stop();
            Console.WriteLine($"------PROGRAM FINISHED------ ({totalTimer.Elapsed})");
            Console.WriteLine();
            Console.WriteLine($"Total # of areas: {Parsers.TotalAreas}, total # of routes: {Parsers.TotalRoutes}");
            SerializeResults(destAreas);

            SendReport($"MountainProjectDBBuilder completed SUCCESSFULLY in {totalTimer.Elapsed}. Total areas: {Parsers.TotalAreas}, total routes: {Parsers.TotalRoutes}", "");
        }

        private static void AddNewItems()
        {
            Parsers.TotalTimer = totalTimer;
            List<Area> destAreas = Parsers.GetDestAreas();

            DateTime lastBuild = DateTime.Now.AddHours(-1);//File.GetLastWriteTime(serializationPath);
            List<string> newlyAddedItemUrls = new List<string>();
            foreach (Area destArea in destAreas)
            {
                //Even though MP supports a single RSS feed for multiple areas (eg selectedIds=105907743,105905173,105909311&routes=on&areas=on), the resulting feed is not collated by date
                //but rather "all new items for the first area, THEN all new items for the second area, etc". This means - without knowing how to paginate a SyndicationFeed - we are better
                //off just parsing an individual RSS feed for each destination area. This is slower (51 web requests rather than 1) but less complicated than paginating the single, huge
                //feed to make sure we get anything new for all areas.
                string rssUrl = $"https://www.mountainproject.com/rss/new?selectedIds={destArea.ID}&routes=on&areas=on";
                SyndicationFeed feed = null;
                using (XmlReader reader = XmlReader.Create(rssUrl))
                {
                    feed = SyndicationFeed.Load(reader);
                }

                newlyAddedItemUrls.AddRange(feed.Items.Where(p => p.PublishDate.DateTime.ToLocalTime() > lastBuild).OrderBy(p => p.PublishDate).Select(p => p.Links[0].Uri.ToString()));
            }

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

            totalTimer.Stop();
            Console.WriteLine($"------PROGRAM FINISHED------ ({totalTimer.Elapsed})");
            Console.WriteLine();
            Console.WriteLine($"Total # of areas: {Parsers.TotalAreas}, total # of routes: {Parsers.TotalRoutes}");
            SerializeResults(destAreas);

            SendReport($"MountainProjectDBBuilder database updated SUCCESSFULLY in {totalTimer.Elapsed}", $"{newlyAddedItemUrls.Count()} new items:\n\n{string.Join("\n", newlyAddedItemUrls)}");
        }

        private static void DownloadGoogleDriveFileFromUrl(string fileUrl)
        {
            using (GoogleDriveDownloader downloader = new GoogleDriveDownloader())
            {
                downloader.DownloadFile(fileUrl, serializationPath);
            }
        }

        private static void SerializeResults(List<Area> inputAreas)
        {
            Console.WriteLine("[SerializeResults] Serializing areas to file");
            if (fileType == FileType.XML)
            {
                using (TextWriter writer = new StreamWriter(serializationPath))
                {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Area>));
                    xmlSerializer.Serialize(writer, inputAreas);
                }
            }
            else
            {
                File.WriteAllText(serializationPath, JsonConvert.SerializeObject(inputAreas));
            }
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
