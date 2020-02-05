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
using System.Threading.Tasks;
using static MountainProjectBot.Enums;

namespace MountainProjectBot
{
    public static class BotUtilities
    {
        private const string XMLNAME = "MountainProjectAreas.xml";
        private const string CREDENTIALSNAME = "Credentials.txt";
        private static string requestForApprovalURL = "";
        private static string repliedToPath = "RepliedTo.txt";
        private static string seenPostsPath = @"RepliedToPosts.txt";
        private static string blacklistedPath = "BlacklistedUsers.txt";
        private static string xmlPath = Path.Combine(@"..\..\MountainProjectDBBuilder\bin\", XMLNAME);
        private static string credentialsPath = Path.Combine(@"..\", CREDENTIALSNAME);

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
                BotFunctions.DryRun = true;
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
            requestForApprovalURL = GetRequestServerURL(credentialsPath);
        }

        public static string GetRequestServerURL(string filePath)
        {
            List<string> fileLines = File.ReadAllLines(filePath).ToList();
            return fileLines.FirstOrDefault(p => p.Contains("requestForApprovalURL")).Split(new[] { ':' }, 2)[1]; //Split on first occurence only because requestForApprovalURL also contains ':'
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

        public static void LogPostBeenSeen(Post post)
        {
            if (!File.Exists(seenPostsPath))
                File.Create(seenPostsPath).Close();

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

            string text = File.ReadAllText(seenPostsPath);
            posts.RemoveAll(p => text.Contains(p.Id));

            return posts;
        }
        #endregion Replied To

        #region Server Calls
        public static void PingStatus()
        {
            string url = "https://script.google.com/macros/s/AKfycbzjGHLRxHDecvJoqZZCG-ZrEs8oOUTHJuAl0xHa0y_iZ2ntbjs/exec?ping";

            try
            {
                DoPOST(url, new List<string>());
            }
            catch { } //Discard any errors
        }

        public static void NotifyFoundPost(string postTitle, string postUrl, string mpResultTitle, string mpResultLoc, string mpResultGrade, string mpResultUrl, string mpResultID, string unconfidentReason, bool alreadyApproved = false)
        {
            if (string.IsNullOrEmpty(requestForApprovalURL))
                return;

            List<string> parameters = new List<string>
            {
                "reason=" + (string.IsNullOrEmpty(unconfidentReason) ? "" : Uri.EscapeDataString(unconfidentReason)),
                "postTitle=" + Uri.EscapeDataString(postTitle),
                "postURL=" + Uri.EscapeDataString(postUrl),
                "mpResultTitle=" + Uri.EscapeDataString(mpResultTitle),
                "mpResultLocation=" + Uri.EscapeDataString(mpResultLoc),
                "mpResultGrade=" + Uri.EscapeDataString(mpResultGrade),
                "mpResultURL=" + Uri.EscapeDataString(mpResultUrl),
                "mpResultID=" + Uri.EscapeDataString(mpResultID)
            };

            if (alreadyApproved)
                parameters.Add("alreadyApproved=true");

            DoPOST(requestForApprovalURL, parameters);
        }

        public static void LogBadReply(Post redditPost)
        {
            if (string.IsNullOrEmpty(requestForApprovalURL))
                return;

            List<string> parameters = new List<string>
            {
                "badReply=" + redditPost.Shortlink
            };

            DoPOST(requestForApprovalURL, parameters);
        }

        public static List<string> GetApprovedPostUrls()
        {
            if (string.IsNullOrEmpty(requestForApprovalURL))
                return new List<string>();

            string response = DoGET(requestForApprovalURL);
            if (response.Contains("<title>Error</title>") || string.IsNullOrEmpty(response)) //Hit an error when contacting server code
                return new List<string>();

            JObject json = JObject.Parse(response);
            return json["approvedPosts"].ToObject<List<string>>();
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
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}
