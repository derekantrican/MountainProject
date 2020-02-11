using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MountainProjectBot
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            BotUtilities.ParseCommandLineArguments(args);

            if (!BotUtilities.HasAllRequiredFiles())
                ExitAfterKeyPress();

            BotUtilities.InitStreams();
            DoBotLoop().Wait();
        }

        #region Error Handling
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            ex = ex.Demystify();

            string exceptionString;
            if (ex is AggregateException aggregateException)
            {
                exceptionString = ExceptionDetailsToString(ex, false);

                foreach (Exception exceptionPart in aggregateException.Flatten().InnerExceptions)
                    exceptionString += ExceptionDetailsToString(exceptionPart);
            }
            else
                exceptionString = ExceptionDetailsToString(ex);

            File.AppendAllText($"CRASHREPORT ({DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss")}).log", exceptionString);
        }

        private static string ExceptionDetailsToString(Exception ex, bool inner = true)
        {
            ex = ex.Demystify();

            string exceptionString = "";
            exceptionString += $"[{DateTime.Now}] EXCEPTION TYPE: {ex?.GetType()}\n";
            exceptionString += $"[{DateTime.Now}] EXCEPTION MESSAGE: {ex?.Message}\n";
            exceptionString += $"[{DateTime.Now}] STACK TRACE: {ex?.StackTrace}\n\n";
            if (ex?.InnerException != null && inner)
            {
                Exception innerEx = ex.InnerException.Demystify();
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION TYPE: {innerEx.GetType()}\n";
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION: {innerEx.Message}\n";
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION STACK TRACE: {innerEx.StackTrace}\n\n";
            }

            return exceptionString;
        }
        #endregion Error Handling

        private static void ExitAfterKeyPress()
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.Read();
            Environment.Exit(0);
        }

        private static async Task DoBotLoop()
        {
            while (true)
            {
                _ = Task.Run(() => BotUtilities.PingStatus()); //Send the bot status (for Uptime Robot)

                Console.WriteLine("\tGetting comments...");
                Stopwatch stopwatch = Stopwatch.StartNew();
                long elapsed;
                
                try
                {
                    BotFunctions.RedditHelper.Actions = 0; //Reset number of actions

                    BotUtilities.WriteToConsoleWithColor("\tChecking monitored comments...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.CheckMonitoredComments();
                    BotUtilities.WriteToConsoleWithColor($"\tDone checking monitored comments ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);

                    BotUtilities.WriteToConsoleWithColor("\tChecking posts for auto-reply...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.CheckPostsForAutoReply(BotFunctions.RedditHelper.Subreddits);
                    BotUtilities.WriteToConsoleWithColor($"\tDone with auto-reply ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);

                    BotUtilities.WriteToConsoleWithColor("\tReplying to approved posts...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.ReplyToApprovedPosts();
                    BotUtilities.WriteToConsoleWithColor($"\tDone replying ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);

                    BotUtilities.WriteToConsoleWithColor("\tGetting recent comments for each subreddit...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    Dictionary<Subreddit, List<Comment>> subredditsAndRecentComments = await BotFunctions.RedditHelper.GetRecentComments();
                    BotUtilities.WriteToConsoleWithColor($"\tDone getting recent comments ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);

                    BotUtilities.WriteToConsoleWithColor("\tChecking for requests (comments with !MountainProject)...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.RespondToRequests(subredditsAndRecentComments.SelectMany(p => p.Value).ToList());
                    BotUtilities.WriteToConsoleWithColor($"\tDone with requests ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);

                    BotUtilities.WriteToConsoleWithColor("\tChecking for MP links...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.RespondToMPUrls(subredditsAndRecentComments);
                    BotUtilities.WriteToConsoleWithColor($"\tDone with MP links ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);
                }
                catch (Exception e)
                {
                    //Handle all sorts of "timeout" or internet connection errors
                    if (e is RedditHttpException ||
                        e is HttpRequestException ||
                        e is WebException ||
                        (e is TaskCanceledException && !(e as TaskCanceledException).CancellationToken.IsCancellationRequested))
                    {
                        Console.WriteLine($"\tIssue connecting to reddit: {e.Message}");
                    }
                    else //If it isn't one of the errors above, it might be more serious. So throw it to be caught as an unhandled exception
                        throw;
                }

                Console.WriteLine($"Loop elapsed time: {stopwatch.ElapsedMilliseconds} ms");
                //Console.WriteLine("Sleeping for 10 seconds...");
                //Thread.Sleep(10000); //Sleep for 10 seconds so as not to overload reddit

                //Only run through once for a dry run
                if (BotFunctions.DryRun)
                {
                    Console.WriteLine("All files updated. The program will now exit...");
                    Thread.Sleep(3000);
                    Environment.Exit(0);
                }
            }
        }
    }
}
