using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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

            string exceptionString = "";
            exceptionString += $"[{DateTime.Now}] EXCEPTION TYPE: {ex?.GetType()}\n\n";
            exceptionString += $"[{DateTime.Now}] EXCEPTION MESSAGE: {ex?.Message}\n\n";
            exceptionString += $"[{DateTime.Now}] STACK TRACE: {ex?.StackTrace}\n\n";
            if (ex?.InnerException != null)
            {
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION: {ex.InnerException}\n\n";
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION STACK TRACE: {ex.InnerException.StackTrace}\n\n";
            }

            File.AppendAllText($"CRASHREPORT ({DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss")}).log", exceptionString);
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

                    Console.WriteLine("\tChecking monitored comments...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.CheckMonitoredComments();
                    Console.WriteLine($"\tDone checking monitored comments ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("\tChecking posts for auto-reply...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.CheckPostsForAutoReply(BotFunctions.RedditHelper.Subreddits);
                    Console.WriteLine($"\tDone with auto-reply ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("\tReplying to approved posts...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.ReplyToApprovedPosts();
                    Console.WriteLine($"\tDone replying ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("\tGetting recent comments for each subreddit...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    Dictionary<Subreddit, List<Comment>> subredditsAndRecentComments = await BotFunctions.RedditHelper.GetRecentComments();
                    Console.WriteLine($"\tDone getting recent comments ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("\tChecking for requests (comments with !MountainProject)...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.RespondToRequests(subredditsAndRecentComments.SelectMany(p => p.Value).ToList());
                    Console.WriteLine($"\tDone with requests ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("\tChecking for MP links...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.RespondToMPUrls(subredditsAndRecentComments);
                    Console.WriteLine($"\tDone with MP links ({stopwatch.ElapsedMilliseconds - elapsed} ms)");
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
            }
        }
    }
}
