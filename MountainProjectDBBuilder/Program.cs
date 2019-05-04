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
            MountainProjectDataSearch.InitMountainProjectData(serializationPath);
            if (MountainProjectDataSearch.DestAreas.Count() == 0)
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
                MPObject result = MountainProjectDataSearch.SearchMountainProject(input);
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
