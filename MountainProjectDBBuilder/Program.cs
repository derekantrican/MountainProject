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
        static string logString = "";
        static Stopwatch totalTimer = new Stopwatch();
        static Mode ProgramMode = Mode.None;

        static void Main(string[] args)
        {
            logPath = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss") + " Log.txt");
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

            bool keepSearching = true;
            while (keepSearching)
            {
                Console.WriteLine("\n\nPlease input the string you would like to parse: ");
                string input = Console.ReadLine();

                Stopwatch stopwatch = Stopwatch.StartNew();
                MPObject result = MountainProjectDataSearch.SearchMountainProject(input);
                stopwatch.Stop();

                if (result == null)
                    Console.WriteLine("Nothing found matching \"" + input + "\"");
                else
                {
                    string resultStr = "";
                    if (result is Area)
                        resultStr = (result as Area).ToString();
                    else if (result is Route)
                        resultStr = (result as Route).ToString();

                    Console.WriteLine("The following was found: " + resultStr + " (Found in " + stopwatch.ElapsedMilliseconds + " ms)");
                    Console.WriteLine($"Parent: {MountainProjectDataSearch.GetParent(result, -1).Name}");
                    Console.WriteLine("\nOpen result? (y/n) ");
                    if (Console.ReadKey().Key == ConsoleKey.Y)
                        Process.Start(result.URL);
                }

                Console.WriteLine("\nSearch something else? (y/n) ");
                keepSearching = Console.ReadKey().Key == ConsoleKey.Y;
            }
        }

        private static void BuildDB()
        {
            try
            {
                totalTimer.Start();
                Log("Starting DB Build...");

                List<Area> destAreas = Parsers.GetDestAreas();
                List<Task> areaTasks = new List<Task>();
                Parallel.ForEach(destAreas, destArea =>
                {
                    areaTasks.Add(Parsers.ParseAreaAsync(destArea));
                });

                Task.WaitAll(areaTasks.ToArray());

                totalTimer.Stop();
                Log($"------PROGRAM FINISHED------ ({totalTimer.Elapsed})");
                SerializeResults(destAreas);
                SendReport($"MountainProjectDBBuilder completed SUCCESSFULLY in {totalTimer.Elapsed}", "");
            }
            catch (Exception ex)
            {

                Log(Environment.NewLine + Environment.NewLine);
                Log("!!!-------------EXCEPTION ENCOUNTERED-------------!!!");
                Log($"EXCEPTION MESSAGE: {ex?.Message}\n");
                Log($"INNER EXCEPTION: {ex?.InnerException}\n");
                Log($"STACK TRACE: {ex?.StackTrace}\n");
                SendReport($"MountainProjectDBBuilder completed WITH ERRORS in {totalTimer.Elapsed}",
                    $"{ex?.Message}\n{ex.InnerException}\n{ex?.StackTrace}");
            }
            finally
            {
                SaveLogToFile();
            }
        }

        private static void SerializeResults(List<Area> inputAreas)
        {
            Log("[SerializeResults] Serializing areas to file");
            TextWriter writer = new StreamWriter(serializationPath);
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Area>));
            xmlSerializer.Serialize(writer, inputAreas);
            writer.Close();
        }

        public static void Log(string itemToLog)
        {
            logString += itemToLog + "\n";
            Console.WriteLine(itemToLog);
        }

        public static void SaveLogToFile()
        {
            File.AppendAllText(logPath, logString);
        }

        private static void SendReport(string subject, string message)
        {
            try
            {
                Log("[SendReport] Sending report");
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
                Log("[SendReport] Could not send email: " + ex.Message);
            }
        }
    }
}
