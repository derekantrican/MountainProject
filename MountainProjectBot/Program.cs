using MountainProjectAPI;
using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        static string blacklistedPath = "BlacklistedUsers.txt";
        static List<string> subredditNames = new List<string>() { "climbing", "climbingporn", "bouldering", "socalclimbing", "climbingvids", "mountainprojectbot",
                                                                  "climbergirls", "climbingcirclejerk", "iceclimbing", "rockclimbing", "tradclimbing"};
        const string BOTKEYWORDREGEX = @"(?i)!mountain\s*project";

        static Reddit redditService;
        static List<Subreddit> subreddits = new List<Subreddit>();

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (args.FirstOrDefault(p => p.Contains("xmlpath=")) != null)
                xmlPath = args.FirstOrDefault(p => p.Contains("xmlpath=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("credentials=")) != null)
                credentialsPath = args.FirstOrDefault(p => p.Contains("credentials=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("repliedto=")) != null)
                repliedToPath = args.FirstOrDefault(p => p.Contains("repliedto=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("blacklisted=")) != null)
                repliedToPath = args.FirstOrDefault(p => p.Contains("blacklisted=")).Split('=')[1];

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
            if (ex?.InnerException != null)
            {
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION: {ex.InnerException}" + Environment.NewLine + Environment.NewLine;
                exceptionString += $"[{DateTime.Now}] INNER EXCEPTION STACK TRACE: {ex.InnerException.StackTrace}" + Environment.NewLine + Environment.NewLine;
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

            foreach (string subRedditName in subredditNames)
                subreddits.Add(await redditService.GetSubredditAsync(subRedditName));

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
                Stopwatch stopwatch = Stopwatch.StartNew();
                long elapsed;
                
                try
                {
                    Console.WriteLine("Getting recent comments for each subreddit");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    Dictionary<Subreddit, List<Comment>> subredditsAndRecentComments = new Dictionary<Subreddit, List<Comment>>();
                    foreach (Subreddit subreddit in subreddits)
                        subredditsAndRecentComments.Add(subreddit, await subreddit.GetComments(1000, 1000).ToList());

                    Console.WriteLine($"Done getting recent comments ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("Checking for requests (comments with !MountainProject)");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await RespondToRequests(subredditsAndRecentComments.SelectMany(p => p.Value).ToList());
                    Console.WriteLine($"Done with requests ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("Checking for MP links");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await RespondToMPUrls(subredditsAndRecentComments);
                    Console.WriteLine($"Done with MP links ({stopwatch.ElapsedMilliseconds - elapsed} ms)");
                }
                catch (Exception e)
                {
                    //Handle all sorts of "timeout" or internet connection errors
                    if (e is RedditHttpException ||
                        e is HttpRequestException ||
                        e is WebException ||
                        (e is TaskCanceledException && !(e as TaskCanceledException).CancellationToken.IsCancellationRequested))
                    {
                        Console.WriteLine($"Issue connecting to reddit: {e.Message}");
                    }
                    else //If it isn't one of the errors above, it might be more serious. So throw it to be caught as an unhandled exception
                        throw;
                }

                Console.WriteLine($"Loop elapsed time: {stopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine("Sleeping for 10 seconds...");
                Thread.Sleep(10000); //Sleep for 10 seconds so as not to overload reddit
            }
        }

        private static async Task RespondToRequests(List<Comment> recentComments) //Respond to comments that specifically called the bot (!MountainProject)
        {
            List<Comment> botRequestComments = recentComments.Where(c => Regex.IsMatch(c.Body, BOTKEYWORDREGEX)).ToList();
            botRequestComments.RemoveAll(c => c.IsArchived);
            botRequestComments.RemoveAll(c => c.AuthorName == "MountainProjectBot" || c.AuthorName == "ClimbingRouteBot"); //Don't reply to bots
            botRequestComments = RemoveAlreadyRepliedTo(botRequestComments);

            foreach (Comment comment in botRequestComments)
            {
                try
                {
                    Console.WriteLine("Getting reply for comment");

                    string reply = BotReply.GetReplyForCommentBody(comment.Body);
                    reply += Markdown.HRule;
                    reply += GetBotLinks(comment);

                    if (!Debugger.IsAttached)
                    {
                        await comment.ReplyAsync(reply);
                        Console.WriteLine($"Replied to comment {comment.Id}");
                    }

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

        private static async Task RespondToMPUrls(Dictionary<Subreddit, List<Comment>> subredditsAndRecentComments) //Respond to comments that have a mountainproject url
        {
            foreach (Subreddit subreddit in subredditsAndRecentComments.Keys.ToList())
            {
                List<Comment> filteredComments = subredditsAndRecentComments[subreddit].Where(c => c.Body.Contains("mountainproject.com")).ToList();
                filteredComments.RemoveAll(c => c.IsArchived);
                filteredComments.RemoveAll(c => c.AuthorName == "MountainProjectBot" || c.AuthorName == "ClimbingRouteBot"); //Don't reply to bots
                filteredComments = RemoveBlacklisted(filteredComments); //Remove comments from users who don't want the bot to automatically reply to them
                filteredComments = RemoveAlreadyRepliedTo(filteredComments);
                filteredComments = await RemoveCommentsOnSelfPosts(subreddit, filteredComments); //Don't reply to self posts (aka text posts)
                subredditsAndRecentComments[subreddit] = filteredComments;
            }

            foreach (Comment comment in subredditsAndRecentComments.SelectMany(p => p.Value))
            {
                try
                {
                    string mpUrl = Regex.Match(comment.Body, @"(https:\/\/)?(www.)?mountainproject\.com.*?(?=\)|\s|$)").Value;
                    if (!mpUrl.Contains("www."))
                        mpUrl = "www." + mpUrl;

                    if (!mpUrl.Contains("https://"))
                        mpUrl = "https://" + mpUrl;

                    MPObject foundObject;
                    try
                    {
                        mpUrl = GetRedirectURL(mpUrl);
                        MPObject mpObjectWithUrl = MountainProjectDataSearch.GetItemWithMatchingUrl(mpUrl, MountainProjectDataSearch.DestAreas.Cast<MPObject>().ToList());
                        foundObject = MountainProjectDataSearch.FilterByPopularity(MountainProjectDataSearch.SearchMountainProject(mpObjectWithUrl.Name));

                        if (mpObjectWithUrl == null || foundObject == null || foundObject.URL != mpObjectWithUrl.URL)
                        {
                            LogCommentBeenRepliedTo(comment); //Don't check this comment again
                            continue;
                        }
                    }
                    catch //Something went wrong. We'll assume that it was because the url didn't match anything
                    {
                        LogCommentBeenRepliedTo(comment); //Don't check this comment again
                        continue;
                    }

                    Console.WriteLine("Getting reply for comment");

                    string reply = $"(FYI in the future you can call me by saying {Markdown.InlineCode($"!MountainProject {foundObject.Name}")})" + Markdown.NewLine;
                    reply += BotReply.GetFormattedString(foundObject, withPrefix: false);
                    reply += Markdown.HRule;
                    reply += GetBotLinks(comment);

                    if (!Debugger.IsAttached)
                    {
                        await comment.ReplyAsync(reply);
                        Console.WriteLine($"Replied to comment {comment.Id}");
                    }

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

        private static string GetRedirectURL(string url)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                return ((HttpWebResponse)req.GetResponse()).ResponseUri.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static string GetBotLinks(Comment relatedComment = null)
        {
            string botLinks = "";

            if (relatedComment != null)
            {
                string commentLink = WebUtility.HtmlEncode("https://reddit.com" + relatedComment.Permalink);
                botLinks += Markdown.Link("Feedback", "https://docs.google.com/forms/d/e/1FAIpQLSchgbXwXMylhtbA8kXFycZenSKpCMZjmYWMZcqREl_OlCm4Ew/viewform?usp=pp_url&entry.266808192=" + commentLink) + " | ";
            }
            else
                botLinks += Markdown.Link("Feedback", "https://docs.google.com/forms/d/e/1FAIpQLSchgbXwXMylhtbA8kXFycZenSKpCMZjmYWMZcqREl_OlCm4Ew/viewform?usp=pp_url") + " | ";

            botLinks += Markdown.Link("FAQ", "https://github.com/derekantrican/MountainProject/wiki/Bot-FAQ") + " | ";
            botLinks += Markdown.Link("Operators", "https://github.com/derekantrican/MountainProject/wiki/Bot-%22Operators%22") + " | ";
            botLinks += Markdown.Link("GitHub", "https://github.com/derekantrican/MountainProject") + " | ";
            botLinks += Markdown.Link("Donate", "https://www.paypal.me/derekantrican");

            return botLinks;
        }

        private static List<Comment> RemoveAlreadyRepliedTo(List<Comment> comments)
        {
            if (!File.Exists(repliedToPath))
                File.Create(repliedToPath).Close();

            string text = File.ReadAllText(repliedToPath);
            comments.RemoveAll(c => text.Contains(c.Id));

            return comments;
        }

        private static List<Comment> RemoveBlacklisted(List<Comment> comments)
        {
            if (!File.Exists(blacklistedPath))
                File.Create(blacklistedPath).Close();

            string text = File.ReadAllText(blacklistedPath);
            comments.RemoveAll(c => text.Contains(c.AuthorName));

            return comments;
        }

        private static async Task<List<Comment>> RemoveCommentsOnSelfPosts(Subreddit subreddit, List<Comment> comments)
        {
            List<Comment> result = new List<Comment>();

            if (comments.Count == 0)
                return result;

            List<Post> subredditPosts = await subreddit.GetPosts(100).ToList();
            subredditPosts.RemoveAll(p => p.IsSelfPost);

            foreach (Comment comment in comments)
            {
                string postLink = comment.Permalink.ToString().Replace(comment.Id + "/", "");
                Post parentPost = subredditPosts.Find(p => p.Permalink.ToString() == postLink);
                if (parentPost != null)
                    result.Add(comment);
            }

            return result;
        }

        private static void LogCommentBeenRepliedTo(Comment comment)
        {
            if (!File.Exists(repliedToPath))
                File.Create(repliedToPath).Close();

            File.AppendAllLines(repliedToPath, new string[] { comment.Id });
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
