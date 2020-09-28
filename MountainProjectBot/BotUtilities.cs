using MountainProjectAPI;
using Newtonsoft.Json.Linq;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
        private static string seenPostsPath = @"RepliedToPosts.txt";
        private static string blacklistedPath = "BlacklistedUsers.txt";
        private static string xmlPath = Path.Combine(@"..\..\MountainProjectDBBuilder\bin\", XMLNAME);
        private static string credentialsPath = Path.Combine(@"..\", CREDENTIALSNAME);

        private static string requestForApprovalURL = "";
        private static string webServerURL = "";
        private static string spreadsheetHistoryURL = "";
        private static Server approvalServer;

        #region Init
        public static void ParseCommandLineArguments(string[] args)
        {
            if (args.FirstOrDefault(p => p.Contains("xmlpath=")) != null)
                xmlPath = args.FirstOrDefault(p => p.Contains("xmlpath=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("credentials=")) != null)
                credentialsPath = args.FirstOrDefault(p => p.Contains("credentials=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("repliedto=")) != null)
                repliedToPath = args.FirstOrDefault(p => p.Contains("repliedto=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("repliedtoposts=")) != null)
                seenPostsPath = args.FirstOrDefault(p => p.Contains("repliedtoposts=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("blacklisted=")) != null)
                blacklistedPath = args.FirstOrDefault(p => p.Contains("blacklisted=")).Split('=')[1];

            if (args.FirstOrDefault(p => p.Contains("dryrun")) != null || Debugger.IsAttached)
            {
                WriteToConsoleWithColor("============== STARTING IN \"DRY RUN\" MODE ==============", ConsoleColor.Blue);
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
            MountainProjectDataSearch.InitMountainProjectData(xmlPath);
            BotFunctions.RedditHelper = new RedditHelper();
            BotFunctions.RedditHelper.Auth(credentialsPath).Wait();
            requestForApprovalURL = GetCredentialValue(credentialsPath, "requestForApprovalURL");
            webServerURL = GetCredentialValue(credentialsPath, "webServerURL");
            spreadsheetHistoryURL = GetCredentialValue(credentialsPath, "spreadsheetURL");

            StartApprovalServer();
        }

        public static string GetCredentialValue(string filePath, string credential)
        {
            List<string> fileLines = File.ReadAllLines(filePath).ToList();
            return fileLines.FirstOrDefault(p => p.StartsWith(credential)).Split(new[] { ':' }, 2)[1]; //Split on first occurence only because requestForApprovalURL also contains ':'
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
                        result.Remove(post);
                }
            }

            return result;
        }
        #endregion Blacklist

        #region Replied To
        public static void LogCommentBeenRepliedTo(Comment comment)
        {
            if (!File.Exists(repliedToPath))
                File.Create(repliedToPath).Close();

            File.AppendAllLines(repliedToPath, new string[] { comment.Id });
        }

        public static void LogPostBeenSeen(Post post, string reason = "")
        {
            if (!File.Exists(seenPostsPath))
                File.Create(seenPostsPath).Close();

            if (!string.IsNullOrEmpty(reason))
                File.AppendAllLines(seenPostsPath, new string[] { $"{post.Id}\t{reason}" });
            else
                File.AppendAllLines(seenPostsPath, new string[] { post.Id });
        }

        public static List<Comment> RemoveAlreadyRepliedTo(List<Comment> comments)
        {
            if (!File.Exists(repliedToPath))
                File.Create(repliedToPath).Close();

            string text = File.ReadAllText(repliedToPath);
            comments.RemoveAll(c => text.Contains(c.Id));

            return comments;
        }

        public static List<Post> RemoveAlreadySeenPosts(List<Post> posts)
        {
            if (!File.Exists(seenPostsPath))
                File.Create(seenPostsPath).Close();

            string[] lines = File.ReadAllLines(seenPostsPath);
            posts.RemoveAll(p => lines.Any(l => l.StartsWith(p.Id)));

            return posts;
        }
        #endregion Replied To

        #region Server Calls
        public static void StartApprovalServer()
        {
            approvalServer = new Server(9999);
            approvalServer.HandleRequest = HandleRequestToApprovalServer;
            approvalServer.ExceptionHandling = (Exception ex) => WriteToConsoleWithColor($"Sever error: ({ex.GetType()}): {ex.Message}\n{ex.StackTrace}", ConsoleColor.Red);
            approvalServer.Start();
        }

        private static string HandleRequestToApprovalServer(ServerRequest request)
        {
            string result = $"Path '{request.Path}' not understood";

            if (request.RequestMethod == HttpMethod.Get && !request.IsFaviconRequest && !request.IsDefaultPageRequest)
            {
                Dictionary<string, string> parameters = request.GetParameters();
                if (parameters.ContainsKey("status")) //UpTimeRobot will ping this
                {
                    return "UP";
                }
                else if (parameters.ContainsKey("postid") && (parameters.ContainsKey("approve") || parameters.ContainsKey("approveall") || parameters.ContainsKey("approveother")))
                {
                    if (BotFunctions.PostsPendingApproval.ContainsKey(parameters["postid"]))
                    {
                        ApprovalRequest approvalRequest = BotFunctions.PostsPendingApproval[parameters["postid"]];

                        if (parameters.ContainsKey("approveother"))
                        {
                            if (parameters.ContainsKey("option"))
                            {
                                MPObject matchingOption = approvalRequest.SearchResult.AllResults.Find(p => p.ID == parameters["option"]) ?? MountainProjectDataSearch.GetItemWithMatchingID(parameters["option"]);
                                if (matchingOption == null)
                                {
                                    result = $"Option '{parameters["option"]}' not found";
                                }
                                else
                                {
                                    approvalRequest.SearchResult.FilteredResult = matchingOption;
                                    approvalRequest.ApproveFiltered = true;
                                }
                            }
                            else
                            {
                                string htmlPicker = "<html><form>";
                                foreach (MPObject option in approvalRequest.SearchResult.AllResults)
                                {
                                    htmlPicker += $"<input type=\"radio\" name=\"options\" value=\"{option.ID}\"{(approvalRequest.SearchResult.AllResults.IndexOf(option) == 0 ? " checked=\"true\"" : "")}>" +
                                                  $"<a href=\"{option.URL}\">{option.Name} ({(option as Route).GetRouteGrade(Grade.GradeSystem.YDS).ToString(false)})</a>" +
                                                  $" ({Regex.Replace(BotReply.GetLocationString(option, approvalRequest.SearchResult.RelatedLocation), @"\[|\]\(.*?\)", "").Replace("\n", "")})<br>";
                                }

                                htmlPicker += "<input type=\"radio\" name=\"options\" id=\"other_option\">Other: <input type=\"text\" id=\"other_option_value\">" +
                                              "<br><input type=\"button\" onclick=\"choose()\" value=\"Choose\"></form><script>" +
                                              "function choose(){" +
                                              "  var options = document.forms[0];" +
                                              "  for (var i = 0; i < options.length; i++){" +
                                              "    if (options[i].checked){" +
                                              "      var chosen = options[i].id != \"other_option\" ? options[i].value : document.getElementById(\"other_option_value\").value.match(/(?<=\\/)\\d+(?=\\/)/g);" +
                                              $"     window.location.replace(\"{(Debugger.IsAttached ? "http://localhost" : webServerURL)}:{approvalServer.Port}?approveother&postid={parameters["postid"]}&option=\" + chosen);" +
                                              "      break;" +
                                              "    }" +
                                              "  }" +
                                              "}" +
                                              "</script></html>";

                                return htmlPicker;
                            }
                        }
                        else if (parameters.ContainsKey("approve"))
                        {
                            approvalRequest.ApproveFiltered = true;
                        }
                        else if (parameters.ContainsKey("approveall"))
                        {
                            approvalRequest.ApproveAll = true;
                        }

                        if (approvalRequest.IsApproved)
                        {
                            BotFunctions.PostsPendingApproval[parameters["postid"]] = approvalRequest;
                            result = $"Approved";
                            if (approvalRequest.ApproveFiltered)
                            {
                                result += $" {approvalRequest.SearchResult.FilteredResult.Name} ({approvalRequest.SearchResult.FilteredResult.ID})";
                            }
                            else
                            {
                                result += "all";
                            }
                        }
                    }
                    else
                    {
                        result = $"Post '{parameters["postid"]}' not found";
                    }
                }
            }

            return $"<h1 style=\"font-size:15vw\">{result}</h1>";
        }

        public static void RequestApproval(ApprovalRequest approvalRequest)
        {
            if (string.IsNullOrEmpty(requestForApprovalURL) || string.IsNullOrEmpty(webServerURL))
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
                               $"[[APPROVE FILTERED]](<{(Debugger.IsAttached ? "http://localhost" : webServerURL)}:{approvalServer.Port}?approve&postid={post.Id}>)  " +
                               $"[[APPROVE ALL]](<{(Debugger.IsAttached ? "http://localhost" : webServerURL)}:{approvalServer.Port}?approveall&postid={post.Id}>)  " +
                               $"[[APPROVE OTHER]](<{(Debugger.IsAttached ? "http://localhost" : webServerURL)}:{approvalServer.Port}?approveother&postid={post.Id}>)";
            }
            else
            {
                messageText += $"**MPResult:** {searchResult.FilteredResult.Name} ({(searchResult.FilteredResult as Route).GetRouteGrade(Grade.GradeSystem.YDS).ToString(false)})\n" +
                               $"{Regex.Replace(BotReply.GetLocationString(searchResult.FilteredResult, searchResult.RelatedLocation), @"\[|\]\(.*?\)", "").Replace("Located in ", "").Replace("\n", "")}\n" +
                               $"<{searchResult.FilteredResult.URL}>\n\n" +
                               $"[[APPROVE]](<{(Debugger.IsAttached ? "http://localhost" : webServerURL)}:{approvalServer.Port}?approve&postid={post.Id}>)  " +
                               $"[[APPROVE OTHER]](<{(Debugger.IsAttached ? "http://localhost" : webServerURL)}:{approvalServer.Port}?approveother&postid={post.Id}>)";
            }

            messageText += "\n--------------------------------";

            SendDiscordMessage(messageText);
        }

        public static void SendDiscordMessage(string message)
        {
            List<string> parameters = new List<string>
            {
                $"username=MountainProjectBot",
                $"avatar_url={Uri.EscapeDataString("https://i.imgur.com/iMhyiUP.png")}",
                $"content={Uri.EscapeDataString(message)}",
            };

            DoPOST(requestForApprovalURL, parameters);
        }

        public static void LogOrUpdateSpreadsheet(ApprovalRequest approvalRequest)
        {
            if (string.IsNullOrEmpty(spreadsheetHistoryURL))
                return;

            string locationString = Regex.Replace(BotReply.GetLocationString(approvalRequest.SearchResult.FilteredResult, approvalRequest.SearchResult.RelatedLocation), @"\[|\]\(.*?\)", "").Replace("Located in ", "").Replace("\n", "");
            List<string> parameters = new List<string>
            {
                $"reason={(string.IsNullOrEmpty(approvalRequest.SearchResult.UnconfidentReason) ? "" : Uri.EscapeDataString(approvalRequest.SearchResult.UnconfidentReason))}",
                $"postTitle={Uri.EscapeDataString(WebUtility.HtmlDecode(approvalRequest.RedditPost.Title))}",
                $"postURL={Uri.EscapeDataString(approvalRequest.RedditPost.Shortlink)}",
                $"mpResultTitle={Uri.EscapeDataString(approvalRequest.SearchResult.FilteredResult.Name)}",
                $"mpResultLocation={Uri.EscapeDataString(locationString)}",
                $"mpResultURL={Uri.EscapeDataString(approvalRequest.SearchResult.FilteredResult.URL)}",
                $"mpResultID={Uri.EscapeDataString(approvalRequest.SearchResult.FilteredResult.ID)}",
            };

            if (approvalRequest.SearchResult.FilteredResult is Route route)
            {
                parameters.Add($"mpResultGrade={Uri.EscapeDataString(route.GetRouteGrade(Grade.GradeSystem.YDS).ToString(false))}");
            }

            if (approvalRequest.SearchResult.Confidence == 1 || approvalRequest.IsApproved)
                parameters.Add("alreadyApproved=true");

            DoPOST(spreadsheetHistoryURL, parameters);
        }

        public static void LogBadReply(Post redditPost)
        {
            if (string.IsNullOrEmpty(spreadsheetHistoryURL))
                return;

            List<string> parameters = new List<string>
            {
                "badReply=" + redditPost.Shortlink
            };

            DoPOST(spreadsheetHistoryURL, parameters);
        }

        private static string DoGET(string url)
        {
            string response;
            HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(url);
            httpRequest.AutomaticDecompression = DecompressionMethods.GZip;

            using (HttpWebResponse serverResponse = (HttpWebResponse)httpRequest.GetResponse())
            using (StreamReader reader = new StreamReader(serverResponse.GetResponseStream()))
            {
                response = reader.ReadToEnd();
            }

            return response;
        }

        private static void DoPOST(string url, List<string> parameters = null)
        {
            string postData = parameters != null ? string.Join("&", parameters) : "";
            byte[] data = Encoding.ASCII.GetBytes(postData);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
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
        #endregion Server Calls

        public static void WriteToConsoleWithColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }

    public class ApprovalRequest
    {
        public Post RedditPost { get; set; }
        public SearchResult SearchResult { get; set; }
        public bool ApproveFiltered { get; set; }
        public bool ApproveAll { get; set; }

        public bool IsApproved
        {
            get { return ApproveFiltered || ApproveAll; }
        }
    }
}
