using Base;
using MountainProjectAPI;
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
    public static class BotUtilities
    {
        private const string XMLNAME = "MountainProjectAreas.xml";
        private const string CREDENTIALSNAME = "Credentials.txt";
        private static string repliedToPath = "RepliedTo.txt";
        public static string SeenPostsPath = @"RepliedToPosts.txt";
        private static string blacklistedPath = "BlacklistedUsers.txt";
        private static string xmlPath = Path.Combine(@"..\..\MountainProjectDBBuilder\bin\", XMLNAME);
        private static string credentialsPath = Path.Combine(@"..\", CREDENTIALSNAME);

        private static string requestForApprovalURL = "";
        private static string spreadsheetHistoryURL = "";

        public static string WebServerURL = "";
        public static string WebServerUsername = "";
        public static string WebServerPassword = "";
        public static string ApprovalServerUrl
        {
            get { return Debugger.IsAttached ? $"http://localhost:{ApprovalServer.Port}" : WebServerURL; }
        }
        public static Server ApprovalServer;

        #region Init
        public static void ParseCommandLineArguments(string[] args) //Todo: use CommandLineParser package
        {
            if (args.FirstOrDefault(p => p.Contains("xmlpath=")) != null)
                xmlPath = args.FirstOrDefault(p => p.Contains("xmlpath=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("credentials=")) != null)
                credentialsPath = args.FirstOrDefault(p => p.Contains("credentials=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("repliedto=")) != null)
                repliedToPath = args.FirstOrDefault(p => p.Contains("repliedto=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("repliedtoposts=")) != null)
                SeenPostsPath = args.FirstOrDefault(p => p.Contains("repliedtoposts=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("blacklisted=")) != null)
                blacklistedPath = args.FirstOrDefault(p => p.Contains("blacklisted=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("dryrun")) != null || Debugger.IsAttached)
            {
                ConsoleHelper.Write("============== STARTING IN \"DRY RUN\" MODE ==============", ConsoleColor.Blue);
                BotFunctions.DryRun = true;
            }
        }

        public static bool HasAllRequiredFiles()
        {
            Console.WriteLine("Checking required files...");

            if (!File.Exists(xmlPath))
            {
                if (File.Exists(XMLNAME)) //If the file does not exist in the built directory, check for it in the same directory
                    xmlPath = XMLNAME;
                else
                {
                    Console.WriteLine($"The xml can not be found here or at {xmlPath}");
                    return false;
                }
            }

            if (!File.Exists(credentialsPath))
            {
                if (File.Exists(CREDENTIALSNAME)) //If the file does not exist in the built directory, check for it in the same directory
                    credentialsPath = CREDENTIALSNAME;
                else
                {
                    Console.WriteLine($"The credentials file can not be found here or at {credentialsPath}");
                    return false;
                }
            }

            Console.WriteLine("All required files present");
            return true;
        }

        public static void InitStreams()
        {
            while (true)
            {
                try
                {
                    MountainProjectDataSearch.InitMountainProjectData(xmlPath);
                    break;
                }
                catch //Note: if the xml file cannot be found, this will just infinitely loop. We should scope down this "catch" a bit
                {
                    ConsoleHelper.Write("MountainProjectAreas.xml is in use. Waiting 5s before trying again...");
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            }

            BotFunctions.RedditHelper = new RedditHelper();
            BotFunctions.RedditHelper.Auth(credentialsPath).Wait();
            requestForApprovalURL = Settings.ReadSettingValue(credentialsPath, "requestForApprovalURL");
            WebServerURL = Settings.ReadSettingValue(credentialsPath, "webServerURL");
            WebServerUsername = Settings.ReadSettingValue(credentialsPath, "webServerUsername");
            WebServerPassword = Settings.ReadSettingValue(credentialsPath, "webServerPassword");
            spreadsheetHistoryURL = Settings.ReadSettingValue(credentialsPath, "spreadsheetURL");

            //Start approval server
            ApprovalServer = new Server(9999)
            {
                HandleRequest = ApprovalServerRequestHandler.HandleRequest
            };
            ApprovalServer.Start();
        }
        #endregion Init

        public static async Task<List<Comment>> RemoveCommentsOnSelfPosts(Subreddit subreddit, List<Comment> comments)
        {
            List<Comment> result = new List<Comment>();

            if (comments.Count == 0)
                return result;

            List<Post> subredditPosts = await BotFunctions.RedditHelper.GetPosts(subreddit);
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

        public static bool PingUrl(string url, out Exception ex)
        {
            ex = null;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(3);
                    HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Head, url);

                    if (!string.IsNullOrEmpty(WebServerUsername))
                    {
                        byte[] authBytes = Encoding.GetEncoding("UTF-8").GetBytes($"{WebServerUsername}:{WebServerPassword}");
                        request.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(authBytes)}");
                    }

                    using (var response = client.Send(request))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                ex = exception;
                return false;
            }
        }

        #region Blacklist
        public static Dictionary<string, BlacklistLevel> GetBlacklist()
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

        public static List<Comment> RemoveBlacklisted(List<Comment> comments, BlacklistLevel[] blacklistLevels)
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

        public static List<Post> RemoveBlacklisted(List<Post> posts, BlacklistLevel[] blacklistLevels)
        {
            Dictionary<string, BlacklistLevel> usersWithBlacklist = GetBlacklist();

            List<Post> result = posts.ToList();
            foreach (Post post in posts)
            {
                if (usersWithBlacklist.ContainsKey(post.AuthorName))
                {
                    if (blacklistLevels.Contains(usersWithBlacklist[post.AuthorName]))
                    {
                        result.Remove(post);
                        LogPostBeenSeen(post, "blacklisted"); //Log the reason we skipped it for accessing posthistory later
                    }
                }
            }

            return result;
        }
        #endregion Blacklist

        #region Replied To
        public static void LogCommentBeenRepliedTo(Comment comment, string reason = "")
        {
            if (!File.Exists(repliedToPath))
                File.Create(repliedToPath).Close();

            if (!string.IsNullOrEmpty(reason))
                File.AppendAllLines(repliedToPath, new string[] { $"{comment.Id}\t{reason}" });
            else
                File.AppendAllLines(repliedToPath, new string[] { comment.Id });
        }

        public static void LogPostBeenSeen(Post post, string reason = "")
        {
            if (!File.Exists(SeenPostsPath))
                File.Create(SeenPostsPath).Close();

            if (!string.IsNullOrEmpty(reason))
                File.AppendAllLines(SeenPostsPath, new string[] { $"{post.Id}\t{reason}" });
            else
                File.AppendAllLines(SeenPostsPath, new string[] { post.Id });
        }

        public static List<Comment> RemoveAlreadyRepliedTo(List<Comment> comments)
        {
            if (!File.Exists(repliedToPath))
                File.Create(repliedToPath).Close();

            string[] lines = File.ReadAllLines(repliedToPath);
            comments.RemoveAll(c => lines.Any(l => l.StartsWith(c.Id)));

            return comments;
        }

        public static List<Post> RemoveAlreadySeenPosts(List<Post> posts)
        {
            if (!File.Exists(SeenPostsPath))
                File.Create(SeenPostsPath).Close();

            string[] lines = File.ReadAllLines(SeenPostsPath);
            posts.RemoveAll(p => lines.Any(l => l.StartsWith(p.Id)));

            return posts;
        }
        #endregion Replied To

        #region Server Calls
        public static void RequestApproval(ApprovalRequest approvalRequest)
        {
            if (string.IsNullOrEmpty(requestForApprovalURL) || string.IsNullOrEmpty(WebServerURL))
                return;

            Post post = approvalRequest.RedditPost;
            SearchResult searchResult = approvalRequest.SearchResult;

            string messageText = "--------------------------------\n" +
                                 $"**Possible AutoReply found:**\n" +
                                 $"{searchResult.UnconfidentReason}\n\n" +
                                 $"**PostTitle:** {post.Title}\n" +
                                 $"**PostURL:** <{post.Shortlink}>\n\n";

            if (searchResult.AllResults.Count > 1)
            {
                messageText += $"**All results found:**\n";
                foreach (MPObject result in searchResult.AllResults /*Todo: Make sure this is ordered by the likely response*/)
                {
                    messageText += $"\t- [{result.Name} ({(result as Route).GetRouteGrade(Grade.GradeSystem.YDS).ToString(false)})](<{result.URL}>)\n";
                }

                messageText += "\n" +
                               $"**Filtered Result:** [{searchResult.FilteredResult.Name} ({(searchResult.FilteredResult as Route).GetRouteGrade(Grade.GradeSystem.YDS).ToString(false)})](<{searchResult.FilteredResult.URL}>)\n" +
                               $"{Regex.Replace(BotReply.GetLocationString(searchResult.FilteredResult, searchResult.RelatedLocation), @"\[|\]\(.*?\)", "").Replace("Located in ", "").Replace("\n", "")}\n\n" +
                               $"[[APPROVE FILTERED]](<{ApprovalServerUrl}?approve&postid={post.Id}>)  " +
                               $"[[APPROVE ALL]](<{ApprovalServerUrl}?approveall&postid={post.Id}>)  " +
                               $"[[APPROVE OTHER]](<{ApprovalServerUrl}?approveother&postid={post.Id}>)";
            }
            else
            {
                messageText += $"**MPResult:** {searchResult.FilteredResult.Name} ({(searchResult.FilteredResult as Route).GetRouteGrade(Grade.GradeSystem.YDS).ToString(false)})\n" +
                               $"{Regex.Replace(BotReply.GetLocationString(searchResult.FilteredResult, searchResult.RelatedLocation), @"\[|\]\(.*?\)", "").Replace("Located in ", "").Replace("\n", "")}\n" +
                               $"<{searchResult.FilteredResult.URL}>\n\n" +
                               $"[[APPROVE]](<{ApprovalServerUrl}?approve&postid={post.Id}>)  " +
                               $"[[APPROVE OTHER]](<{ApprovalServerUrl}?approveother&postid={post.Id}>)";
            }

            messageText += "\n--------------------------------";

            SendDiscordMessage(messageText);
        }

        public static void SendDiscordMessage(string message)
        {
            try
            {
                DoPOST(requestForApprovalURL, new Dictionary<string, string>
                {
                    { "username", "MountainProjectBot" },
                    { "avatar_url", "https://i.imgur.com/iMhyiUP.png" },
                    { "content", message },
                });
            }
            catch (Exception e)
            {
                string text = $"Error sending discord message: {e.Message} ({e.GetType()})\n\n" +
                              $"Content:\n\n{Uri.EscapeDataString(message)}";
                File.WriteAllText($"PROBLEMATIC DISCORD MESSAGE ({DateTime.Now:yyyy.MM.dd.HH.mm.ss}).log", text);
            }
        }

        public static void LogOrUpdateSpreadsheet(ApprovalRequest approvalRequest)
        {
            if (string.IsNullOrEmpty(spreadsheetHistoryURL))
            {
                return;
            }

            string locationString = Regex.Replace(BotReply.GetLocationString(approvalRequest.SearchResult.FilteredResult, approvalRequest.SearchResult.RelatedLocation), @"\[|\]\(.*?\)", "").Replace("Located in ", "").Replace("\n", "");

            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "reason", string.IsNullOrEmpty(approvalRequest.SearchResult.UnconfidentReason) ? "" : Uri.EscapeDataString(approvalRequest.SearchResult.UnconfidentReason) },
                { "postTitle", WebUtility.HtmlDecode(approvalRequest.RedditPost.Title) },
                { "postUrl", approvalRequest.RedditPost.Shortlink },
                { "mpResultTitle", approvalRequest.SearchResult.FilteredResult.Name },
                { "mpResultLocation", locationString },
                { "mpResultURL", approvalRequest.SearchResult.FilteredResult.URL },
                { "mpResultID", approvalRequest.SearchResult.FilteredResult.ID },
            };

            if (approvalRequest.SearchResult.FilteredResult is Route route)
            {
                parameters.Add("mpResultGrade", route.GetRouteGrade(Grade.GradeSystem.YDS).ToString(false));
            }

            if (approvalRequest.SearchResult.Confidence == 1 || approvalRequest.IsApproved)
            {
                parameters.Add("alreadyApproved", "true");
            }

            DoPOST(spreadsheetHistoryURL, parameters);
        }

        public static void LogBadReply(Post redditPost)
        {
            if (string.IsNullOrEmpty(spreadsheetHistoryURL))
            {
                return;
            }

            DoPOST(spreadsheetHistoryURL, new Dictionary<string, string>
            {
                { "badReply", redditPost.Shortlink },
            });
        }

        private static string DoGET(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                using (Stream stream = client.Send(new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url)).Content.ReadAsStream())
                {
                    using (StreamReader streamReader = new StreamReader(stream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
        }

        private static void DoPOST(string url, Dictionary<string, string> parameters = null)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, url);

                if (parameters != null)
                {
                    request.Content = new FormUrlEncodedContent(parameters);
                }

                using (client.Send(request)) { }
            }
        }
        #endregion Server Calls
    }

    public class ApprovalRequest //Todo: rather than modifying ApprovalRequest.SearchResult.FilteredResult (or similar) - ApprovalRequest should have its own list of approved items (maybe also with a related area?)
    {
        public Post RedditPost { get; set; }
        public SearchResult SearchResult { get; set; }
        public List<MPObject> ApprovedResults { get; set; } = new List<MPObject>();
        public Area RelatedLocation { get; set; }
        public bool Force { get; set; }

        public bool IsApproved
        {
            get { return ApprovedResults.Any(); }
        }
    }
}
