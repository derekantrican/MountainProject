using MountainProjectAPI;
using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MountainProjectBot
{
    class Program
    {
        const string XMLNAME = "MountainProjectAreas.xml";
        static string xmlPath = Path.Combine(@"..\..\MountainProjectDBBuilder\bin\", XMLNAME);
        const string CREDENTIALSNAME = "Credentials.txt";
        static string credentialsPath = Path.Combine(@"..\", CREDENTIALSNAME);
        static string repliedToPath = "RepliedTo.txt";
        const string SUBREDDITNAME = "/r/climbing";
        const string BOTKEYWORDREGEX = @"(?i)!mountain\s*project";

        static Reddit redditService;
        static Subreddit subReddit;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (args.FirstOrDefault(p => p.Contains("xmlpath=")) != null)
                xmlPath = args.FirstOrDefault(p => p.Contains("xmlpath=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("credentials=")) != null)
                credentialsPath = args.FirstOrDefault(p => p.Contains("credentials=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("repliedto=")) != null)
                repliedToPath = args.FirstOrDefault(p => p.Contains("repliedto=")).Split('=')[1];

            CheckRequiredFiles();
            MountainProjectDataSearch.InitMountainProjectData(xmlPath);
            AuthReddit().Wait();
            DoBotLoop().Wait();
        }


        #region Error Handling
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            string exceptionString = "";
            exceptionString += $"[{DateTime.Now}] EXCEPTION TYPE: {ex?.GetType()}" + Environment.NewLine + Environment.NewLine;
            exceptionString += $"[{DateTime.Now}] EXCEPTION MESSAGE: {ex?.Message}" + Environment.NewLine + Environment.NewLine;
            exceptionString += $"[{DateTime.Now}] STACK TRACE: {ex?.StackTrace}" + Environment.NewLine + Environment.NewLine;
            if (ex.InnerException != null)
            {
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION: {ex?.InnerException}" + Environment.NewLine + Environment.NewLine;
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION STACK TRACE: {ex?.InnerException.StackTrace}" + Environment.NewLine + Environment.NewLine;
            }

            File.AppendAllText("CRASHREPORT (" + DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss") + ").log", exceptionString);
        }
        #endregion Error Handling

        private static void CheckRequiredFiles()
        {
            Console.WriteLine("Checking required files...");

            if (!File.Exists(xmlPath))
            {
                if (File.Exists(XMLNAME)) //If the file does not exist in the built directory, check for it in the same directory
                    xmlPath = XMLNAME;
                else
                {
                    Console.WriteLine("The xml has not been built");
                    ExitAfterKeyPress();
                }
            }

            if (!File.Exists(credentialsPath))
            {
                if (File.Exists(CREDENTIALSNAME)) //If the file does not exist in the built directory, check for it in the same directory
                    credentialsPath = CREDENTIALSNAME;
                else
                {
                    Console.WriteLine("The credentials file does not exist");
                    ExitAfterKeyPress();
                }
            }

            Console.WriteLine("All required files present");
        }

        private static async Task AuthReddit()
        {
            Console.WriteLine("Authorizing Reddit...");

            WebAgent webAgent = GetWebAgentCredentialsFromFile();
            redditService = new Reddit(webAgent, true);
            subReddit = await redditService.GetSubredditAsync(SUBREDDITNAME);

            Console.WriteLine("Reddit authed successfully");
        }

        private static WebAgent GetWebAgentCredentialsFromFile()
        {
            List<string> fileLines = File.ReadAllLines(credentialsPath).ToList();

            string username = fileLines.FirstOrDefault(p => p.Contains("username")).Split(':')[1];
            string password = fileLines.FirstOrDefault(p => p.Contains("password")).Split(':')[1];
            string clientId = fileLines.FirstOrDefault(p => p.Contains("clientId")).Split(':')[1];
            string clientSecret = fileLines.FirstOrDefault(p => p.Contains("clientSecret")).Split(':')[1];
            string redirectUri = fileLines.FirstOrDefault(p => p.Contains("redirectUri")).Split(new[] { ':' }, 2)[1]; //Split on first occurence only because redirectUri also contains ':'

            return new BotWebAgent(username, password, clientId, clientSecret, redirectUri);
        }

        private static async Task DoBotLoop()
        {
            while (true)
            {
                _ = Task.Run(() => PingStatus()); //Send the bot status (for Uptime Robot)

                //Get the latest 1000 comments on the subreddit, the filter to the ones that have the keyword
                //and have not already been replied to
                Console.WriteLine("Getting comments...");

                try
                {
                    List<Comment> comments = await subReddit.GetComments(1000, 1000).Where(c => Regex.IsMatch(c.Body, BOTKEYWORDREGEX)).ToList();
                    comments = RemoveAlreadyRepliedTo(comments);

                    foreach (Comment comment in comments)
                    {
                        try
                        {
                            string reply = GetReplyForComment(comment);
                            await comment.ReplyAsync(reply);
                            await comment.UpvoteAsync();
                            Console.WriteLine($"Replied to comment {comment.Id}");
                            LogCommentBeenRepliedTo(comment);
                        }
                        catch (RateLimitException)
                        {
                            Console.WriteLine("Rate limit hit. Postponing reply until next iteration");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Exception occurred with comment https://reddit.com{comment.Permalink}");
                            Console.WriteLine($"{e.Message}\n{e.StackTrace}");
                        }
                    }
                }
                catch (Exception e)
                {
                    //Handle all sorts of "timeout" errors
                    if (e is RedditHttpException ||
                        e is WebException ||
                        (e is TaskCanceledException && !(e as TaskCanceledException).CancellationToken.IsCancellationRequested))
                    {
                        Console.WriteLine($"Issue connecting to reddit: {e.Message}");
                    }
                    else //If it isn't one of the errors above, it might be more serious. So throw it to be caught as an unhandled exception
                        throw;
                }

                Console.WriteLine("Sleeping for 10 seconds...");
                Thread.Sleep(10000); //Sleep for 10 seconds so as not to overload reddit
            }
        }

        public static string GetReplyForComment(Comment replyTo)
        {
            Console.WriteLine("Getting reply for comment");

            string replyText = BotReply.GetReplyForCommentBody(replyTo.Body);

            replyText += Markdown.HRule;

            string commentLink = WebUtility.HtmlEncode("https://reddit.com" + replyTo.Permalink);
            replyText += Markdown.Link("Feedback", "https://docs.google.com/forms/d/e/1FAIpQLSchgbXwXMylhtbA8kXFycZenSKpCMZjmYWMZcqREl_OlCm4Ew/viewform?usp=pp_url&entry.266808192=" + commentLink) + " | ";
            replyText += Markdown.Link("Donate", "https://www.paypal.me/derekantrican") + " | ";
            replyText += Markdown.Link("GitHub", "https://github.com/derekantrican/MountainProject") + " | ";
            replyText += Markdown.Link("FAQ", "https://github.com/derekantrican/MountainProject/wiki/Bot-FAQ");

            return replyText;
        }

        private static List<Comment> RemoveAlreadyRepliedTo(List<Comment> comments)
        {
            if (!File.Exists(repliedToPath))
                File.Create(repliedToPath).Close();

            string text = File.ReadAllText(repliedToPath);
            comments.RemoveAll(c => text.Contains(c.Id));

            return comments;
        }

        private static void LogCommentBeenRepliedTo(Comment comment)
        {
            if (!File.Exists(repliedToPath))
                File.Create(repliedToPath).Close();

            File.AppendAllText(repliedToPath, comment.Id);
        }

        private static void PingStatus()
        {
            string url = "https://script.google.com/macros/s/AKfycbzjGHLRxHDecvJoqZZCG-ZrEs8oOUTHJuAl0xHa0y_iZ2ntbjs/exec?ping";

            try
            {
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(url);
                httpRequest.AutomaticDecompression = DecompressionMethods.GZip;

                using (HttpWebResponse licensingResponse = (HttpWebResponse)httpRequest.GetResponse())
                using (Stream stream = licensingResponse.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string response = reader.ReadToEnd();
                }
            }
            catch { } //Discard any errors
        }

        private static void ExitAfterKeyPress()
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.Read();
            Environment.Exit(0);
        }
    }
}
