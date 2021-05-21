using Base;
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
        private static StringWriter outputCapture = new StringWriter();
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ConsoleHelper.WriteToAdditionalTarget(outputCapture);

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

            exceptionString += $"[{DateTime.Now}] 500 RECENT LOG LINES:\n\n";
            string[] logLines = outputCapture.ToString().Split('\n');
            foreach (string line in logLines.Skip(Math.Max(0, logLines.Count() - 500)))
                exceptionString += $"{line}\n";

            File.AppendAllText($"CRASHREPORT ({DateTime.Now:yyyy.MM.dd.HH.mm.ss}).log", exceptionString);
        }

        private static string ExceptionDetailsToString(Exception ex, bool inner = true)
        {
            ex = ex.Demystify();

            string exceptionString = "";
            exceptionString += $"[{DateTime.Now}] EXCEPTION TYPE: {ex?.GetType()}\n";
            exceptionString += $"[{DateTime.Now}] EXCEPTION MESSAGE: {ex?.Message}\n";
            exceptionString += $"[{DateTime.Now}] STACK TRACE: {ex?.StackTrace}\n\n";

            if (ex is TaskCanceledException taskCanceledException)
                exceptionString += $"[{DateTime.Now}] IS CANCELLATION REQUESTED: {taskCanceledException.CancellationToken.IsCancellationRequested}\n";

            exceptionString += "\n";

            if (ex?.InnerException != null && inner)
            {
                Exception innerEx = ex.InnerException.Demystify();
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION TYPE: {innerEx.GetType()}\n";
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION: {innerEx.Message}\n";
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION STACK TRACE: {innerEx.StackTrace}\n";

                if (innerEx is TaskCanceledException innerTaskCanceledException)
                    exceptionString += $"[{DateTime.Now}] IS CANCELLATION REQUESTED: {innerTaskCanceledException.CancellationToken.IsCancellationRequested}\n";

                exceptionString += "\n";
            }

            return exceptionString;
        }

        private static bool IsInternetConnectionException(Exception ex)
        {
            return ex is RedditHttpException ||
                ex is HttpRequestException ||
                ex is WebException ||
                ex is TaskCanceledException ||
                ex is OperationCanceledException ||
                (ex is AggregateException aggregateEx && aggregateEx.InnerExceptions.Any(e => IsInternetConnectionException(e)));
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
                if (!BotFunctions.DryRun)
                {
                    ApprovalServerStatusCheck();
                }

                Console.WriteLine("\tGetting comments...");
                Stopwatch stopwatch = Stopwatch.StartNew();
                long elapsed;

                try
                {
                    BotFunctions.RedditHelper.Actions = 0; //Reset number of actions

                    ConsoleHelper.Write("\tChecking monitored comments...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.CheckMonitoredComments();
                    ConsoleHelper.Write($"\tDone checking monitored comments ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);

                    ConsoleHelper.Write("\tChecking posts for auto-reply...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.CheckPostsForAutoReply(BotFunctions.RedditHelper.Subreddits);
                    ConsoleHelper.Write($"\tDone with auto-reply ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);

                    ConsoleHelper.Write("\tReplying to approved posts...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.ReplyToApprovedPosts();
                    ConsoleHelper.Write($"\tDone replying ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);

                    ConsoleHelper.Write("\tGetting recent comments for each subreddit...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    Dictionary<Subreddit, List<Comment>> subredditsAndRecentComments = await BotFunctions.RedditHelper.GetRecentComments();
                    ConsoleHelper.Write($"\tDone getting recent comments ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);

                    ConsoleHelper.Write("\tChecking for requests (comments with !MountainProject)...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.RespondToRequests(subredditsAndRecentComments.SelectMany(p => p.Value).ToList());
                    ConsoleHelper.Write($"\tDone with requests ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);

                    ConsoleHelper.Write("\tChecking for MP links...", ConsoleColor.Blue);
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await BotFunctions.RespondToMPUrls(subredditsAndRecentComments);
                    ConsoleHelper.Write($"\tDone with MP links ({stopwatch.ElapsedMilliseconds - elapsed} ms)", ConsoleColor.Blue);
                }
                catch (Exception e)
                {
                    if (e is AggregateException aggregateException)
                    {
                        foreach (Exception innerException in aggregateException.InnerExceptions)
                        {
                            ConsoleHelper.Write($"Inner exception ({innerException.GetType()}: {innerException.Message}) thrown at \n\n{innerException.StackTrace}", ConsoleColor.Red);
                        }
                    }

                    //Handle all sorts of "timeout" or internet connection errors
                    if (IsInternetConnectionException(e))
                    {
                        Console.WriteLine($"\tIssue connecting to reddit: {e.Message}");
                    }
                    else //If it isn't one of the errors above, it might be more serious. So throw it to be caught as an unhandled exception
                    {
                        ConsoleHelper.Write($"Exception ({e.GetType()}: {e.Message}) thrown at \n\n{e.StackTrace}", ConsoleColor.Red);
                        throw;
                    }
                }

                Console.WriteLine($"Loop elapsed time: {stopwatch.ElapsedMilliseconds} ms");
                //Console.WriteLine("Sleeping for 10 seconds...");
                //Thread.Sleep(10000); //Sleep for 10 seconds so as not to overload reddit

                //Only run through once for a dry run
                if (BotFunctions.DryRun && !Debugger.IsAttached)
                {
                    Console.WriteLine("All files updated. The program will now exit...");
                    Thread.Sleep(3000);
                    Environment.Exit(0);
                }
            }
        }

        private static bool alerted = false;
        private static void ApprovalServerStatusCheck()
        {
            bool serverUp = BotUtilities.PingUrl($"{BotUtilities.ApprovalServerUrl}?status");
            if (!serverUp && !alerted)
            {
                BotUtilities.SendDiscordMessage("Approval server is down (ping timed out)\nAttempting to restart...");
                alerted = true;

                BotUtilities.ApprovalServer.Restart();
            }
            else if (serverUp && alerted)
            {
                BotUtilities.SendDiscordMessage("Approval server is back up");
                alerted = false;
            }
        }
    }
}
