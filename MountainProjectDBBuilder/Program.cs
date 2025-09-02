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
using System.Runtime.ExceptionServices;
using System.Threading;

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
        private static string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{DateTime.Now:yyyy.MM.dd.HH.mm.ss} Log.txt");
        private static readonly StringWriter outputCapture = new StringWriter();
        private static readonly Stopwatch totalTimer = new Stopwatch();
        private static Mode programMode = Mode.None;
        private static FileType fileType = FileType.XML;
        private static bool buildAll = true;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            ConsoleHelper.WriteToAdditionalTarget(outputCapture);
            WebRequest.DefaultWebProxy = null; //https://stackoverflow.com/a/4420429/2246411

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
                else if (!string.IsNullOrEmpty(o.SingleAreaId))
                {
					BuildDB(o.SingleAreaId);
                    Environment.Exit(0);
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
                {
                    input = input.Replace("-all", "").Trim();
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                SearchResult searchResult = MountainProjectDataSearch.Search(input, searchParameters);
                stopwatch.Stop();

                if (searchResult.IsEmpty())
                {
                    Console.WriteLine("Nothing found matching \"" + input + "\"");
                }
                else if (allResults)
                {
                    List<MPObject> matchedObjectsByPopularity = searchResult.AllResults.OrderByDescending(p => p.Popularity).ToList();
                    Console.WriteLine($"Found {matchedObjectsByPopularity.Count} items match that search query (found in {stopwatch.ElapsedMilliseconds} ms):");
                    foreach (MPObject result in matchedObjectsByPopularity)
                    {
                        string url = Url.Replace(result.URL, Utilities.MPBASEURL, "");
                        if (result is Route)
                        {
                            Console.WriteLine($"    Route: {result.Name} (Pop: {result.Popularity}) | Location: {GetLocationString(result)} | {url}");
                        }
                        else if (result is Area)
                        {
                            Console.WriteLine($"    Area: {result.Name} (Pop: {result.Popularity}) | Location: {GetLocationString(result)} | {url}");
                        }
                    }
                }
                else
                {
                    string resultStr = "";
                    MPObject result = searchResult.FilteredResult;
                    if (result is Area)
                    {
                        resultStr = (result as Area).ToString();
                    }
                    else if (result is Route)
                    {
                        resultStr = (result as Route).ToString(resultParameters);
                    }

                    Console.WriteLine($"The following was found (found in {stopwatch.ElapsedMilliseconds} ms):");
                    Console.WriteLine("    " + resultStr);
                    Console.WriteLine($"    Location: {GetLocationString(result, searchResult.RelatedLocation)}");
                    Console.WriteLine("\nOpen result? (y/n) ");
                    if (Console.ReadLine().ToLower() == "y")
                    {
                        Process.Start(result.URL);
                    }
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
            {
                return "";
            }

            string locationString = $"Located in {innerParent.Name}";
            if (outerParent != null && outerParent.URL != innerParent.URL)
            {
                locationString += $", {outerParent.Name}";
            }

            return locationString;
        }

        private static void BuildDB(string specificAreaId = null)
        {
            RunAndCatchBuildIssues("DB Build", () =>
            {
                if (!buildAll && File.Exists(serializationPath))
                {
                    AddNewItems();
                }
                else if (!string.IsNullOrEmpty(specificAreaId))
                {
                    BuildDBSingleArea(specificAreaId);
                }
                else
                {
                    BuildFullDB();
                }
            });
        }

        private static void BuildDBSingleArea(string specificAreaId)
        {
            Parsers.TotalTimer = totalTimer;
            Parsers.TargetTotalRoutes = Parsers.GetTargetTotalRoutes(specificAreaId);
            List<Area> destAreas = Parsers.GetDestAreas();

            Area singleArea = destAreas.FirstOrDefault(a => a.ID == specificAreaId);
            if (singleArea == null)
            {
                SendReport($"BuildDBSingleArea failed (could not find an area with an id matching {specificAreaId}", "");
                return;
            }

            Parsers.ParseAreaAsync(singleArea).Wait();

            totalTimer.Stop();
            Console.WriteLine($"------PROGRAM FINISHED------ ({totalTimer.Elapsed})");
            Console.WriteLine();
            Console.WriteLine($"Total # of areas: {Parsers.TotalAreas}, total # of routes: {Parsers.TotalRoutes}");
            FileInfo file = SerializeResults(destAreas);

            SendReport($"MountainProjectDBBuilder completed SUCCESSFULLY in {totalTimer.Elapsed} ({Math.Round(file.Length / 1024f / 1024f, 2)} MB) for {singleArea.Name}. Total areas: {Parsers.TotalAreas}, total routes: {Parsers.TotalRoutes}",
                string.Join("\n", Parsers.Info.OrderBy(s => s.Key).Select(s => $"[{s.Key}] {s.Value}")));
            LogParseTime($"Full Build", totalTimer.Elapsed);
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
            FileInfo file = SerializeResults(destAreas);

            SendReport($"MountainProjectDBBuilder completed SUCCESSFULLY in {totalTimer.Elapsed} ({Math.Round(file.Length / 1024f / 1024f, 2)} MB). Total areas: {Parsers.TotalAreas}, total routes: {Parsers.TotalRoutes}",
                string.Join("\n", Parsers.Info.OrderBy(s => s.Key).Select(s => $"[{s.Key}] {s.Value}")));
            LogParseTime($"Full Build", totalTimer.Elapsed);
        }

        //This is here to hopefully quit immediately if a ParseException is thrown (rather than waiting multiple hours on other tasks to finish)
        //Source: https://stackoverflow.com/a/22851991/2246411 (from the linked gist)
        private static void WaitAllWithImmediateThrow(Task[] tasks, CancellationToken token)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            Exception firstException = null;

            foreach (var task in tasks)
            {
                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        firstException = t.Exception;
                        cts.Cancel();
                    }
                },
                cts.Token,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Current);
            }

            try
            {
                Task.WaitAll(tasks, cts.Token);
            }
            catch (OperationCanceledException) { } //if cts.Cancel() is invoked, then that will be the ultimate exception (and mask the actual exception)

            if (firstException != null)
            {
                throw firstException;
            }
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
                {
                    continue;
                }

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

                // NOTE: the previous approach just parsed the new area/route and added it to the parent.
                // But that caused issues in one case when a new area was created and existing
                // routes/subareas were moved to the newly-created subarea. Instead, we will get the
                // most-immediate, already-existing parent of the newly-created item and re-parse the
                // whole parent (and, recursively, children) again. THIS WILL DEFINITELY TAKE LONGER since
                // there will be a number of children that didn't change and don't need to be re-parsed,
                // but it will be more accurate as any one of that children could have lost an area/route
                // (see above scenario) and the RSS feeds only provide data about "adds" (and not any other
                // change). See https://github.com/derekantrican/MountainProject/issues/77

                Area parentArea = null;
                //Go up the parent list (from most immediate to highest) and get the most immediate parent
                //that already existed (wasn't just added)
                foreach (string parentId in newItem.ParentIDs.AsEnumerable().Reverse())
                {
                    MPObject matchingParent = MountainProjectDataSearch.GetItemWithMatchingID(parentId);
                    if (matchingParent != null)
                    {
                        parentArea = matchingParent as Area;
                        break;
                    }
                }

                //Empty out subareas & routes and recursively re-parse that parent area to get the proper list of children
                parentArea.SubAreas.Clear();
                parentArea.Routes.Clear();
                Parsers.ParseAreaAsync(parentArea).Wait();
            }

            //Check for duplicates
            Dictionary<string, List<List<string>>> allPathsLists = new Dictionary<string, List<List<string>>>();
            foreach (Area area in MountainProjectDataSearch.DestAreas)
            {
                ListAllIdPaths(area, allPathsLists, new List<string> { area.ID });
            }

            List<string> duplicateIds = allPathsLists.Where(kvp => kvp.Value.Count > 1).Select(kvp => kvp.Key).ToList();

            //Reparse duplicate ids
            HashSet<string> idsToReparse = new HashSet<string>();
            foreach (string duplicateId in duplicateIds)
            {
                List<List<string>> reversedPaths = allPathsLists[duplicateId].Select(p => p.AsEnumerable().Reverse().ToList() /*this allows us to reverse the list as a new list*/).ToList();
                for (int i = 1; i < reversedPaths.MaxBy(l => l.Count).Count; i++)
                {
                    List<string> uniqueParentsForEachPath = reversedPaths.Select(p => p[i]).Distinct().ToList();
                    if (uniqueParentsForEachPath.Count > 1)
                    {
                        uniqueParentsForEachPath.ForEach(id => idsToReparse.Add(id));
                        break;
                    }
                }
            }

            if (idsToReparse.Any())
            {
                foreach (string id in idsToReparse)
                {
                    Area areaToReparse = MountainProjectDataSearch.GetItemWithMatchingID(id) as Area;
                    areaToReparse.SubAreas.Clear();
                    areaToReparse.Routes.Clear();

                    try
                    {
                        Parsers.ParseAreaAsync(areaToReparse).Wait();
                    }
                    catch (Exception ex)
                    {
                        ParseException parseException;
                        if (ex is AggregateException aggregate && ex.InnerException is ParseException)
                        {
                            parseException = ex.InnerException as ParseException;
                        }
                        else if (ex is ParseException)
                        {
                            parseException = ex as ParseException;
                        }
                        else
                        {
                            throw;
                        }

                        //This is for a case where the areaToReparse has been deleted
                        //Todo: It could be that the parents have also been deleted so we will need to do this recursively
                        if (parseException.GetInnermostParseException().InnerException is HttpRequestException httpException && httpException.StatusCode == HttpStatusCode.NotFound)
                        {
                            areaToReparse.PopulateParents();
                            areaToReparse = areaToReparse.Parents.Last() as Area;
                            areaToReparse.SubAreas.Clear();
                            areaToReparse.Routes.Clear();
                            Parsers.ParseAreaAsync(areaToReparse).Wait();
                        }
                    }
                }
            }

            //Check for duplicates (after reparse)
            allPathsLists = new Dictionary<string, List<List<string>>>();
            foreach (Area area in MountainProjectDataSearch.DestAreas)
            {
                ListAllIdPaths(area, allPathsLists, new List<string> { area.ID });
            }

            duplicateIds = allPathsLists.Where(kvp => kvp.Value.Count > 1).Select(kvp => kvp.Key).ToList();

            //Send report about # of duplicates AFTER reparsing duplicates
            if (duplicateIds.Any())
            {
                SendReport($"{duplicateIds.Count} duplicates COULD NOT BE RESOLVED after a reparse",
                    $"{string.Join("\n\n", duplicateIds.Select(id => $"{id}:\n{string.Join("\n", allPathsLists[id].Select(p => string.Join(" > ", p)))}"))}");
            }

            totalTimer.Stop();
            Console.WriteLine($"------PROGRAM FINISHED------ ({totalTimer.Elapsed})");
            Console.WriteLine();
            Console.WriteLine($"Total # of areas: {Parsers.TotalAreas}, total # of routes: {Parsers.TotalRoutes}");
            FileInfo file = SerializeResults(MountainProjectDataSearch.DestAreas);

            if (errors.Count == 1)
            {
                throw errors[0];
            }
            else if (errors.Count > 1)
            {
                throw new AggregateException("Some areas failed to return new items", errors);
            }

            SendReport($"MountainProjectDBBuilder database updated SUCCESSFULLY in {totalTimer.Elapsed} ({Math.Round(file.Length / 1024f / 1024f, 2)} MB)", $"{newlyAddedItemUrls.Count()} new items:\n\n{string.Join("\n", newlyAddedItemUrls)}");
        }

        private static void ListAllIdPaths(Area area, Dictionary<string, List<List<string>>> dict, List<string> currentPathToParent)
        {
            foreach (Area subArea in area.SubAreas)
            {
                List<string> pathFromParentToSubArea = currentPathToParent.Concat(new[] { subArea.ID }).ToList();
                dict.ExtendDictionaryList(subArea.ID, pathFromParentToSubArea);
                ListAllIdPaths(subArea, dict, pathFromParentToSubArea);
            }

            foreach (Route route in area.Routes)
            {
                List<string> pathFromParentToRoute = currentPathToParent.Concat(new[] { route.ID }).ToList();
                dict.ExtendDictionaryList(route.ID, pathFromParentToRoute);
            }
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

                SendReport($"[NET{Environment.Version.Major} - StaticHttpClient] MountainProjectDBBuilder benchmark completed SUCCESSFULLY in {totalTimer.Elapsed}. Total areas: {Parsers.TotalAreas}, total routes: {Parsers.TotalRoutes}", "");
                LogParseTime($"NET{Environment.Version.Major} (StaticHttpClient) benchmark", totalTimer.Elapsed);
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
                            exceptionString = parseException.DumpToString();
                        }
                        else
                        {
                            exceptionString += $"EXCEPTION MESSAGE: {ExceptionDispatchInfo.Capture(innerException).SourceException.Message}\n";
                            exceptionString += $"STACK TRACE: {ExceptionDispatchInfo.Capture(innerException).SourceException.StackTrace}\n\n";
                        }
                    }
                }
                else
                {
                    if (ex is ParseException parseException)
                    {
                        exceptionString = parseException.DumpToString();
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

        private static FileInfo SerializeResults(List<Area> inputAreas)
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

            return new FileInfo(serializationPath);
        }

        private static void SendReport(string subject, string message)
        {
            if (File.Exists(settingsPath))
            {
                try
                {
                    Console.WriteLine("[SendReport] Sending report");
                    string url = Settings.ReadSettingValue(settingsPath, "reportUrl");

                    using (HttpClient client = new HttpClient())
                    {
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                        request.Content = JsonContent.Create(new
                        {
                            subjectonly = subject,
                            messageonly = message,
                        });

                        var response = client.Send(request);
                        Console.WriteLine(response.Content.ReadAsStringAsync().Result);
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
                                DateTime.Now.ToString("M/d/yyyy"),
                                timeSpan.ToString(@"hh\:mm\:ss"),
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
