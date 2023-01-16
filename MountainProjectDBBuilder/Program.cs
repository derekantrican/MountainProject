using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Threading.Tasks;
using MountainProjectAPI;
using System.Xml;
using System.ServiceModel.Syndication;
using Base;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System.Text.RegularExpressions;
using CommandLine;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

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
        private static string serializationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MountainProjectAreas.xml");
        private static string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt");
        private static string logPath = $"{DateTime.Now:yyyy.MM.dd.HH.mm.ss} Log.txt";
        private static readonly StringWriter outputCapture = new StringWriter();
        private static readonly Stopwatch totalTimer = new Stopwatch();
        private static Mode programMode = Mode.None;
        private static FileType fileType = FileType.XML;
        private static bool buildAll = true;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            ConsoleHelper.WriteToAdditionalTarget(outputCapture);

            new Parser(p => p.CaseInsensitiveEnumValues = true).ParseArguments<Options>(args).WithParsed(o =>
            {
                fileType = o.FileType;

                if (o.OnlyNew)
                {
                    buildAll = false;
                }

                if (o.Build)
                {
                    programMode = Mode.BuildDB;
                }
                else if (o.Parse)
                {
                    programMode = Mode.Parse;
                }
                else if (o.Benchmark)
                {
                    programMode = Mode.Benchmark;
                }
                else if (!string.IsNullOrEmpty(o.DownloadUrl))
                {
                    DownloadGoogleDriveFileFromUrl(o.DownloadUrl);
                    Environment.Exit(0);
                }
            });

            switch (programMode)
            {
                case Mode.BuildDB:
                case Mode.None:
                    BuildDB();
                    break;
                case Mode.Parse:
                    ParseInputString();
                    break;
                case Mode.Benchmark:
                    RunBenchmark();
                    break;
            }
        }

        private static void ParseInputString()
        {
            if (File.Exists(serializationPath))
            {
                MountainProjectDataSearch.InitMountainProjectData(serializationPath);
            }

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
                        string url = Url.Replace(result.URL, Utilities.MPBASEURL, "");
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
            RunAndCatchBuildIssues("DB Build", () =>
            {
                if (!buildAll && File.Exists(serializationPath))
                    AddNewItems();
                else
                    BuildFullDB();
            });
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
            LogParseTime($"Full Build", totalTimer.Elapsed);
        }

        private static void AddNewItems()
        {
            Parsers.TotalTimer = totalTimer;
            List<Area> destAreas = Parsers.GetDestAreas(true);

            DateTime lastBuild = File.GetLastWriteTime(serializationPath);
            List<string> newlyAddedItemUrls = new List<string>();
            List<Exception> errors = new List<Exception>();
            foreach (Area destArea in destAreas)
            {
                //Even though MP supports a single RSS feed for multiple areas (eg selectedIds=105907743,105905173,105909311&routes=on&areas=on), the resulting feed is not collated by date
                //but rather "all new items for the first area, THEN all new items for the second area, etc". This means - without knowing how to paginate a SyndicationFeed - we are better
                //off just parsing an individual RSS feed for each destination area. This is slower (51 web requests rather than 1) but less complicated than paginating the single, huge
                //feed to make sure we get anything new for all areas.
                Console.WriteLine($"Getting new routes & areas for {destArea.Name}...");
                string rssUrl = $"https://www.mountainproject.com/rss/new?selectedIds={destArea.ID}&routes=on&areas=on";
                try
                {
                    SyndicationFeed feed = null;
                    using (XmlReader reader = XmlReader.Create(rssUrl))
                    {
                        feed = SyndicationFeed.Load(reader);
                    }

                    newlyAddedItemUrls.AddRange(feed.Items.Where(p => p.PublishDate.DateTime.ToLocalTime() > lastBuild).OrderBy(p => p.PublishDate).Select(p => p.Links[0].Uri.ToString()));
                }
                catch (Exception ex)
                {
                    string errorMsg = $"An error occurred when trying to get the new items for {destArea.Name}: {ex.Message}";
                    Console.WriteLine(errorMsg);
                    errors.Add(new Exception(errorMsg, ex));
                }
            }

            MountainProjectDataSearch.InitMountainProjectData(serializationPath);

            foreach (string newItemUrl in newlyAddedItemUrls)
            {
                string newId = Utilities.GetID(newItemUrl);

                if (MountainProjectDataSearch.GetItemWithMatchingID(newId) != null) //Item has already been added (probably via a recursive area add)
                    continue;

                MPObject newItem;
                if (Url.Contains(newItemUrl, Utilities.MPAREAURL))
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

            if (errors.Count == 1)
            {
                throw errors[0];
            }
            else if (errors.Count > 1)
            {
                throw new AggregateException("Some areas failed to return new items", errors);
            }

            SendReport($"MountainProjectDBBuilder database updated SUCCESSFULLY in {totalTimer.Elapsed}", $"{newlyAddedItemUrls.Count()} new items:\n\n{string.Join("\n", newlyAddedItemUrls)}");
        }

        private static void DownloadGoogleDriveFileFromUrl(string fileUrl)
        {
            using (GoogleDriveDownloader downloader = new GoogleDriveDownloader())
            {
                downloader.DownloadFile(fileUrl, serializationPath);
            }
        }

        private static void RunBenchmark()
        {
            RunAndCatchBuildIssues("benchmark", () =>
            {
                //Get total number of routes in Alabama (copied from Parsers.GetTargetTotalRoutes() & tweaked)
                IHtmlDocument doc = Utilities.GetHtmlDoc(Utilities.ALLLOCATIONSURL);
                IElement element = doc.GetElementById("route-guide").GetElementsByTagName("a").FirstOrDefault(x => x.TextContent.Contains("Alabama"));
                element = element.ParentElement.FirstElementChild;

                Parsers.TargetTotalRoutes = int.Parse(Regex.Match(element.TextContent.Replace(",", ""), @"\d+").Value);

                //No particular reason we're using Alabama. It's a good size - not a tiny area (like Delaware) and not huge (like California). Also happens to be first in the list.
                Area alabama = Parsers.GetDestAreas().FirstOrDefault(area => area.ID == "105905173");

                Parsers.ParseAreaAsync(alabama).Wait();

                totalTimer.Stop();
                Console.WriteLine($"------PROGRAM FINISHED------ ({totalTimer.Elapsed})");
                Console.WriteLine();
                Console.WriteLine($"Total # of areas: {Parsers.TotalAreas}, total # of routes: {Parsers.TotalRoutes}");

                SendReport($"[NET{Environment.Version.Major}] MountainProjectDBBuilder benchmark completed SUCCESSFULLY in {totalTimer.Elapsed}. Total areas: {Parsers.TotalAreas}, total routes: {Parsers.TotalRoutes}", "");
                LogParseTime($"NET{Environment.Version.Major} benchmark", totalTimer.Elapsed);
            });
        }

        private static void RunAndCatchBuildIssues(string name, Action buildAction)
        {
            try
            {
                totalTimer.Start();
                Console.WriteLine($"Starting {name}...");

                Parsers.TotalTimer = totalTimer;

                buildAction();
            }
            catch (Exception ex)
            {
                string exceptionString = "";
                if (ex is AggregateException aggregateException)
                {
                    foreach (Exception innerException in aggregateException.InnerExceptions)
                    {
                        if (innerException is ParseException parseException)
                        {
                            ParseException innerMostParseException = parseException.GetInnermostParseException();
                            exceptionString += $"FAILING MPOBJECT: {innerMostParseException.RelatedObject.URL}\n";
                            exceptionString += $"EXCEPTION MESSAGE: {innerMostParseException.InnerException?.Message}\n";
                            exceptionString += $"STACK TRACE: {innerMostParseException.InnerException?.StackTrace}\n\n";

                            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Failing Object ({innerMostParseException.RelatedObject.ID}).html"), innerMostParseException.Html);
                        }
                        else
                        {
                            exceptionString += $"EXCEPTION MESSAGE: {innerException?.Message}\n";
                            exceptionString += $"STACK TRACE: {innerException?.StackTrace}\n\n";
                        }
                    }
                }
                else
                {
                    if (ex is ParseException parseException)
                    {
                        ParseException innerMostParseException = parseException.GetInnermostParseException();
                        exceptionString += $"FAILING MPOBJECT: {innerMostParseException.RelatedObject.URL}\n";
                        exceptionString += $"EXCEPTION MESSAGE: {innerMostParseException.InnerException?.Message}\n";
                        exceptionString += $"STACK TRACE: {innerMostParseException.InnerException?.StackTrace}\n";

                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Failing Object ({innerMostParseException.RelatedObject.ID}).html"), innerMostParseException.Html);
                    }
                    else
                    {
                        exceptionString += $"EXCEPTION MESSAGE: {ex?.Message}\n";
                        exceptionString += $"INNER EXCEPTION: {ex?.InnerException?.Message}\n";
                        exceptionString += $"STACK TRACE: {ex?.StackTrace}\n";
                    }
                }

                Console.WriteLine(Environment.NewLine + Environment.NewLine);
                Console.WriteLine("!!!-------------EXCEPTION ENCOUNTERED-------------!!!");
                Console.WriteLine(exceptionString);
                SendReport($"MountainProjectDBBuilder {name} completed WITH ERRORS in {totalTimer.Elapsed}", exceptionString);
            }
            finally
            {
                File.AppendAllText(logPath, outputCapture.ToString());
                outputCapture.Dispose();
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
            if (File.Exists(settingsPath))
            {
                try
                {
                    Console.WriteLine("[SendReport] Sending report");
                    string url = Settings.ReadSettingValue(settingsPath, "reportUrl");
                    url += $"subjectonly={Uri.EscapeDataString(subject)}&messageonly={Uri.EscapeDataString(message)}";

                    using (HttpClient client = new HttpClient())
                    {
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                        client.Send(request);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[SendReport] Could not send email: " + ex.Message);
                }
            }
        }

        private static void LogParseTime(string sheetName, TimeSpan timeSpan)
        {
            if (File.Exists(settingsPath))
            {
                try
                {
                    Console.WriteLine("[LogParseTime] Logging parse time");
                    string url = Settings.ReadSettingValue(settingsPath, "dataLogUrl");
                    url += $"/{sheetName}";

                    using (HttpClient client = new HttpClient())
                    {
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Settings.ReadSettingValue(settingsPath, "dataLogAuth"));
                        request.Content = JsonContent.Create(new
                        {
                            values = new[]
                            {
                                DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss"),
                                timeSpan.TotalHours < 1 ? timeSpan.ToString("mm:ss") : timeSpan.ToString(),
                            },
                        });

                        client.Send(request);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[LogParseTime] Could not log time: " + ex.Message);
                }
            }
        }
    }
}
