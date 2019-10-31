using MountainProjectAPI;
using Newtonsoft.Json.Linq;
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static MountainProjectBot.Enums;

namespace MountainProjectBot
{
    class Program
    {
        const string XMLNAME = "MountainProjectAreas.xml";
        static string xmlPath = Path.Combine(@"..\..\MountainProjectDBBuilder\bin\", XMLNAME);
        const string CREDENTIALSNAME = "Credentials.txt";
        static string credentialsPath = Path.Combine(@"..\", CREDENTIALSNAME);
        static string repliedToPath = "RepliedTo.txt";
        static string repliedToPostsPath = @"RepliedToPosts.txt";
        static string requestForApprovalURL = "";
        static string blacklistedPath = "BlacklistedUsers.txt";
        const string BOTKEYWORDREGEX = @"(?i)!mountain\s*project";

        static RedditHelper redditHelper = new RedditHelper();
        static List<CommentMonitor> monitoredComments = new List<CommentMonitor>();

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (args.FirstOrDefault(p => p.Contains("xmlpath=")) != null)
                xmlPath = args.FirstOrDefault(p => p.Contains("xmlpath=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("credentials=")) != null)
                credentialsPath = args.FirstOrDefault(p => p.Contains("credentials=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("repliedto=")) != null)
                repliedToPath = args.FirstOrDefault(p => p.Contains("repliedto=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("blacklisted=")) != null)
                blacklistedPath = args.FirstOrDefault(p => p.Contains("blacklisted=")).Split('=')[1];

            CheckRequiredFiles();
            MountainProjectDataSearch.InitMountainProjectData(xmlPath);
            redditHelper.Auth(credentialsPath).Wait();
            GetRequestServerURL(credentialsPath);
            DoBotLoop().Wait();
        }

        private static void GetRequestServerURL(string filePath)
        {
            List<string> fileLines = File.ReadAllLines(filePath).ToList();
            requestForApprovalURL = fileLines.FirstOrDefault(p => p.Contains("requestForApprovalURL")).Split(new[] { ':' }, 2)[1]; //Split on first occurence only because requestForApprovalURL also contains ':'
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

        private static async Task DoBotLoop()
        {
            while (true)
            {
                _ = Task.Run(() => PingStatus()); //Send the bot status (for Uptime Robot)

                Console.WriteLine("\tGetting comments...");
                Stopwatch stopwatch = Stopwatch.StartNew();
                long elapsed;
                
                try
                {
                    redditHelper.Actions = 0; //Reset number of actions

                    Console.WriteLine("\tChecking monitored comments...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await CheckMonitoredComments();
                    Console.WriteLine($"\tDone checking monitored comments ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("\tReplying to approved posts...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await ReplyToApprovedPosts();
                    Console.WriteLine($"\tDone replying ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("\tChecking posts for auto-reply...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await CheckPostsForAutoReply(redditHelper.Subreddits);
                    Console.WriteLine($"\tDone with auto-reply ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("\tGetting recent comments for each subreddit...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    Dictionary<Subreddit, List<Comment>> subredditsAndRecentComments = await redditHelper.GetRecentComments();
                    Console.WriteLine($"\tDone getting recent comments ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("\tChecking for requests (comments with !MountainProject)...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await RespondToRequests(subredditsAndRecentComments.SelectMany(p => p.Value).ToList());
                    Console.WriteLine($"\tDone with requests ({stopwatch.ElapsedMilliseconds - elapsed} ms)");

                    Console.WriteLine("\tChecking for MP links...");
                    elapsed = stopwatch.ElapsedMilliseconds;
                    await RespondToMPUrls(subredditsAndRecentComments);
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
                Console.WriteLine("Sleeping for 10 seconds...");
                Thread.Sleep(10000); //Sleep for 10 seconds so as not to overload reddit
            }
        }

        private static async Task CheckMonitoredComments()
        {
            monitoredComments.RemoveAll(c => (DateTime.Now - c.Created).TotalHours > 1); //Remove any old monitors

            for (int i = monitoredComments.Count - 1; i >= 0; i--)
            {
                CommentMonitor monitor = monitoredComments[i];

                string oldParentBody = monitor.ParentComment.Body;
                string oldResponseBody = monitor.BotResponseComment.Body;

                try
                {
                    Comment updatedParent = await redditHelper.GetComment(monitor.ParentComment.Permalink);
                    if (updatedParent.Body == "[deleted]" || 
                        (updatedParent.IsRemoved.HasValue && updatedParent.IsRemoved.Value)) //If comment is deleted or removed, delete the bot's response
                    {
                        await redditHelper.DeleteComment(monitor.BotResponseComment);
                        monitoredComments.Remove(monitor);
                    }
                    else if (updatedParent.Body != oldParentBody) //If the parent comment's request has changed, edit the bot's response
                    {
                        if (Regex.IsMatch(updatedParent.Body, BOTKEYWORDREGEX))
                        {
                            string reply = BotReply.GetReplyForRequest(updatedParent);

                            if (reply != oldResponseBody)
                            {
                                if (!string.IsNullOrEmpty(reply))
                                    await redditHelper.EditComment(monitor.BotResponseComment, reply);
                                else
                                {
                                    await redditHelper.DeleteComment(monitor.BotResponseComment);
                                    monitoredComments.Remove(monitor);
                                }
                            }

                            monitor.ParentComment = updatedParent;
                        }
                        else if (updatedParent.Body.Contains("mountainproject.com")) //If the parent comment's MP url has changed, edit the bot's response
                        {
                            string reply = BotReply.GetReplyForMPLinks(updatedParent);

                            if (reply != oldResponseBody)
                            {
                                if (!string.IsNullOrEmpty(reply))
                                    await redditHelper.EditComment(monitor.BotResponseComment, reply);
                                else
                                {
                                    await redditHelper.DeleteComment(monitor.BotResponseComment);
                                    monitoredComments.Remove(monitor);
                                }
                            }

                            monitor.ParentComment = updatedParent;
                        }
                        else  //If the parent comment is no longer a request or contains a MP url, delete the bot's response
                        {
                            await redditHelper.DeleteComment(monitor.BotResponseComment);
                            monitoredComments.Remove(monitor);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"\tException occured when checking monitor for comment {RedditHelper.GetFullLink(monitor.ParentComment.Permalink)}");
                    Console.WriteLine($"\t{e.Message}\n{e.StackTrace}");
                }
            }
        }

        static List<KeyValuePair<Post, SearchResult>> postsPendingApproval = new List<KeyValuePair<Post, SearchResult>>();
        private static async Task ReplyToApprovedPosts()
        {
            postsPendingApproval.RemoveAll(p => (DateTime.UtcNow - p.Key.CreatedUTC).TotalMinutes > 15); //Remove posts that have "timed out"
            postsPendingApproval = await RemoveWhereClimbingRouteBotHasReplied(postsPendingApproval);
            if (postsPendingApproval.Count == 0)
                return;

            List<string> approvedUrls = GetApprovedPostUrls();
            List<KeyValuePair<Post, SearchResult>> approvedPosts = postsPendingApproval.Where(p => approvedUrls.Contains(p.Key.Shortlink) || p.Value.Confidence == 1).ToList();
            foreach (KeyValuePair<Post, SearchResult> post in approvedPosts)
            {
                string reply = BotReply.GetFormattedString(post.Value);
                reply += Markdown.HRule;
                reply += BotReply.GetBotLinks(post.Key);

                if (!Debugger.IsAttached)
                {
                    await redditHelper.CommentOnPost(post.Key, reply);
                    Console.WriteLine($"\n    Auto-replied to post {post.Key.Id}");
                }

                postsPendingApproval.RemoveAll(p => p.Key == post.Key);
            }
        }

        private static async Task CheckPostsForAutoReply(List<Subreddit> subreddits)
        {
            List<Post> recentPosts = new List<Post>();
            foreach (Subreddit subreddit in subreddits)
            {
                List<Post> subredditPosts = await redditHelper.GetPosts(subreddit, 10);
                subredditPosts.RemoveAll(p => p.IsSelfPost);
                subredditPosts.RemoveAll(p => (DateTime.UtcNow - p.CreatedUTC).TotalMinutes > 10); //Only check recent posts
                //subredditPosts.RemoveAll(p => (DateTime.UtcNow - p.CreatedUTC).TotalMinutes < 3); //Wait till posts are 3 minutes old (gives poster time to add a comment with a MP link or for the ClimbingRouteBot to respond)
                subredditPosts = RemoveBlacklisted(subredditPosts, new[] { BlacklistLevel.NoPostReplies, BlacklistLevel.OnlyKeywordReplies, BlacklistLevel.Total }); //Remove posts from users who don't want the bot to automatically reply to them
                subredditPosts = RemoveAlreadyRepliedTo(subredditPosts);
                recentPosts.AddRange(subredditPosts);
            }

            foreach (Post post in recentPosts)
            {
                try
                {
                    string postTitle = WebUtility.HtmlDecode(post.Title);

                    Console.WriteLine($"\tTrying to get an automatic reply for post (/r/{post.SubredditName}): {postTitle}");

                    SearchResult searchResult = MountainProjectDataSearch.ParseRouteFromString(postTitle);
                    if (!searchResult.IsEmpty())
                    {
                        postsPendingApproval.Add(new KeyValuePair<Post, SearchResult>(post, searchResult));

                        //Until we are more confident with automatic results, we're going to request for approval for confidence values greater than 1 (less than 100%)
                        if (searchResult.Confidence > 1)
                        {
                            string locationString = Regex.Replace(BotReply.GetLocationString(searchResult.FilteredResult), @"\[|\]\(.*?\)", "").Replace("Located in ", "").Replace("\n", "");
                            NotifyFoundPost(WebUtility.HtmlDecode(post.Title), post.Shortlink, searchResult.FilteredResult.Name, locationString,
                                        (searchResult.FilteredResult as Route).GetRouteGrade(Grade.GradeSystem.YDS).ToString(false), searchResult.FilteredResult.URL, searchResult.FilteredResult.ID);
                        }
                    }
                    else
                        Console.WriteLine("\tNothing found");

                    LogPostBeenSeen(post);
                }
                catch (RateLimitException)
                {
                    Console.WriteLine("\tRate limit hit. Postponing reply until next iteration");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"\tException occurred with post {RedditHelper.GetFullLink(post.Permalink)}");
                    Console.WriteLine($"\t{e.Message}\n{e.StackTrace}");
                }
            }
        }

        private static async Task RespondToRequests(List<Comment> recentComments) //Respond to comments that specifically called the bot (!MountainProject)
        {
            List<Comment> botRequestComments = recentComments.Where(c => Regex.IsMatch(c.Body, BOTKEYWORDREGEX)).ToList();
            botRequestComments.RemoveAll(c => c.IsArchived);
            botRequestComments = RemoveBlacklisted(botRequestComments, new[] { BlacklistLevel.Total }); //Don't reply to bots
            botRequestComments = RemoveAlreadyRepliedTo(botRequestComments);

            foreach (Comment comment in botRequestComments)
            {
                try
                {
                    Console.WriteLine($"\tGetting reply for comment: {comment.Id}");

                    string reply = BotReply.GetReplyForRequest(comment);

                    if (!Debugger.IsAttached)
                    {
                        Comment botReplyComment = await redditHelper.ReplyToComment(comment, reply);
                        Console.WriteLine($"\tReplied to comment {comment.Id}");
                        monitoredComments.Add(new CommentMonitor() { ParentComment = comment, BotResponseComment = botReplyComment });
                    }

                    LogCommentBeenRepliedTo(comment);
                }
                catch (RateLimitException)
                {
                    Console.WriteLine("\tRate limit hit. Postponing reply until next iteration");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"\tException occurred with comment {RedditHelper.GetFullLink(comment.Permalink)}");
                    Console.WriteLine($"\t{e.Message}\n{e.StackTrace}");
                }
            }
        }

        private static async Task RespondToMPUrls(Dictionary<Subreddit, List<Comment>> subredditsAndRecentComments) //Respond to comments that have a mountainproject url
        {
            foreach (Subreddit subreddit in subredditsAndRecentComments.Keys.ToList())
            {
                List<Comment> filteredComments = subredditsAndRecentComments[subreddit].Where(c => c.Body.Contains("mountainproject.com")).ToList();
                filteredComments.RemoveAll(c => c.IsArchived);
                filteredComments = RemoveBlacklisted(filteredComments, new[] { BlacklistLevel.OnlyKeywordReplies, BlacklistLevel.Total }); //Remove comments from users who don't want the bot to automatically reply to them
                filteredComments = RemoveAlreadyRepliedTo(filteredComments);
                filteredComments = await RemoveCommentsOnSelfPosts(subreddit, filteredComments); //Don't reply to self posts (aka text posts)
                subredditsAndRecentComments[subreddit] = filteredComments;
            }

            foreach (Comment comment in subredditsAndRecentComments.SelectMany(p => p.Value))
            {
                try
                {
                    Console.WriteLine($"\tGetting reply for comment: {comment.Id}");

                    string reply = BotReply.GetReplyForMPLinks(comment);

                    if (string.IsNullOrEmpty(reply))
                    {
                        LogCommentBeenRepliedTo(comment); //Don't check this comment again
                        continue;
                    }

                    if (!Debugger.IsAttached)
                    {
                        Comment botReplyComment = await redditHelper.ReplyToComment(comment, reply);
                        Console.WriteLine($"\tReplied to comment {comment.Id}");
                        monitoredComments.Add(new CommentMonitor() { ParentComment = comment, BotResponseComment = botReplyComment });
                    }

                    LogCommentBeenRepliedTo(comment);
                }
                catch (RateLimitException)
                {
                    Console.WriteLine("\tRate limit hit. Postponing reply until next iteration");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"\tException occurred with comment {RedditHelper.GetFullLink(comment.Permalink)}");
                    Console.WriteLine($"\t{e.Message}\n{e.StackTrace}");
                }
            }
        }

        private static List<Comment> RemoveAlreadyRepliedTo(List<Comment> comments)
        {
            if (!File.Exists(repliedToPath))
                File.Create(repliedToPath).Close();

            string text = File.ReadAllText(repliedToPath);
            comments.RemoveAll(c => text.Contains(c.Id));

            return comments;
        }

        private static Dictionary<string, BlacklistLevel> GetBlacklist()
        {
            if (!File.Exists(blacklistedPath))
                File.Create(blacklistedPath).Close();

            Dictionary<string, BlacklistLevel> result = new Dictionary<string, BlacklistLevel>();

            List<string> lines = File.ReadAllLines(blacklistedPath).ToList();
            foreach (string line in lines)
            {
                string username = line.Split(',')[0];
                BlacklistLevel blacklist = (BlacklistLevel)Convert.ToInt32(line.Split(',')[1]);
                result.Add(username, blacklist);
            }

            return result;
        }

        private static List<Comment> RemoveBlacklisted(List<Comment> comments, BlacklistLevel[] blacklistLevels)
        {
            Dictionary<string, BlacklistLevel> usersWithBlacklist = GetBlacklist();

            List<Comment> result = comments.ToList();
            foreach (Comment comment in comments)
            {
                if (usersWithBlacklist.ContainsKey(comment.AuthorName))
                {
                    if (blacklistLevels.Contains(usersWithBlacklist[comment.AuthorName]))
                        result.Remove(comment);
                }
            }

            return result;
        }

        private static List<Post> RemoveBlacklisted(List<Post> posts, BlacklistLevel[] blacklistLevels)
        {
            Dictionary<string, BlacklistLevel> usersWithBlacklist = GetBlacklist();

            List<Post> result = posts.ToList();
            foreach (Post post in posts)
            {
                if (usersWithBlacklist.ContainsKey(post.AuthorName))
                {
                    if (blacklistLevels.Contains(usersWithBlacklist[post.AuthorName]))
                        result.Remove(post);
                }
            }

            return result;
        }

        private static async Task<List<KeyValuePair<Post, SearchResult>>> RemoveWhereClimbingRouteBotHasReplied(List<KeyValuePair<Post, SearchResult>> posts)
        {
            List<KeyValuePair<Post, SearchResult>> result = posts.ToList();
            foreach (Post post in posts.Select(p => p.Key))
            {
                List<Comment> postComments = await post.GetCommentsAsync(100);
                if (postComments.Any(c => c.AuthorName == "ClimbingRouteBot"))
                    result.RemoveAll(p => p.Key == post);
            }

            return result;
        }

        private static async Task<List<Comment>> RemoveCommentsOnSelfPosts(Subreddit subreddit, List<Comment> comments)
        {
            List<Comment> result = new List<Comment>();

            if (comments.Count == 0)
                return result;

            List<Post> subredditPosts = await redditHelper.GetPosts(subreddit);
            subredditPosts.RemoveAll(p => p.IsSelfPost);

            foreach (Comment comment in comments)
            {
                string postLink = RedditHelper.GetPostLinkFromComment(comment);
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

        private static void LogPostBeenSeen(Post post)
        {
            if (!File.Exists(repliedToPostsPath))
                File.Create(repliedToPostsPath).Close();

            File.AppendAllLines(repliedToPostsPath, new string[] { post.Id });
        }

        private static List<Post> RemoveAlreadyRepliedTo(List<Post> posts)
        {
            if (!File.Exists(repliedToPostsPath))
                File.Create(repliedToPostsPath).Close();

            string text = File.ReadAllText(repliedToPostsPath);
            posts.RemoveAll(p => text.Contains(p.Id));

            return posts;
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

        public static void NotifyFoundPost(string postTitle, string postUrl, string mpResultTitle, string mpResultLoc, string mpResultGrade, string mpResultUrl, string mpResultID)
        {
            if (string.IsNullOrEmpty(requestForApprovalURL))
                return;

            List<string> parameters = new List<string>
            {
                "postTitle=" + Uri.EscapeDataString(postTitle),
                "postURL=" + Uri.EscapeDataString(postUrl),
                "mpResultTitle=" + Uri.EscapeDataString(mpResultTitle),
                "mpResultLocation=" + Uri.EscapeDataString(mpResultLoc),
                "mpResultGrade=" + Uri.EscapeDataString(mpResultGrade),
                "mpResultURL=" + Uri.EscapeDataString(mpResultUrl),
                "mpResultID=" + Uri.EscapeDataString(mpResultID)
            };

            string postData = string.Join("&", parameters);
            byte[] data = Encoding.ASCII.GetBytes(postData);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestForApprovalURL);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;

            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            using (HttpWebResponse serverResponse = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(serverResponse.GetResponseStream()))
            {
                _ = reader.ReadToEnd(); //For POST requests, we don't care about what we get back
            }
        }

        public static List<string> GetApprovedPostUrls()
        {
            if (string.IsNullOrEmpty(requestForApprovalURL))
                return new List<string>();

            string response;
            HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(requestForApprovalURL);
            httpRequest.AutomaticDecompression = DecompressionMethods.GZip;

            using (HttpWebResponse serverResponse = (HttpWebResponse)httpRequest.GetResponse())
            using (StreamReader reader = new StreamReader(serverResponse.GetResponseStream()))
            {
                response = reader.ReadToEnd();
            }

            if (response.Contains("<title>Error</title>") || string.IsNullOrEmpty(response)) //Hit an error when contacting server code
                return new List<string>();

            JObject json = JObject.Parse(response);
            return json["approvedPosts"].ToObject<List<string>>();
        }
    }
}
