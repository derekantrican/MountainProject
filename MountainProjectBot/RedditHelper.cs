using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MountainProjectBot
{
    public class RedditHelper
    {
        public const string REDDITPREFIX = "https://reddit.com";
        Reddit redditService;
        Dictionary<string, int> subredditNamesAndCommentAmounts = new Dictionary<string, int>()
        {
            {"climbing", 1000 }, {"climbingporn", 30}, {"bouldering", 600}, {"socalclimbing", 50}, {"climbingvids", 30}, {"mountainprojectbot", 500},
            {"climbergirls", 200 }, {"iceclimbing", 30 }, {"rockclimbing", 50}, {"tradclimbing", 100}
        };
        public List<Subreddit> Subreddits = new List<Subreddit>();

        int actionLimit = 20;

        public RedditHelper()
        {
            Actions = 0;
        }

        public int Actions { get; set; }

        private void IncrementActionAndCheckLimit()
        {
            //The purpose of this is to kill the bot if too many actions happen at once (indicative of something going wrong)
            Actions++;
            if (Actions > actionLimit)
                throw new Exception("Action limit has been reached. Killing bot");
        }

        public async Task Auth(string filePath)
        {
            Console.WriteLine("Authorizing Reddit...");

            WebAgent webAgent = GetWebAgentCredentialsFromFile(filePath);
            redditService = new Reddit(webAgent, true);

            foreach (string subRedditName in subredditNamesAndCommentAmounts.Keys)
                Subreddits.Add(await redditService.GetSubredditAsync(subRedditName));

            Console.WriteLine("Reddit authed successfully");
        }

        private static WebAgent GetWebAgentCredentialsFromFile(string filePath)
        {
            List<string> fileLines = File.ReadAllLines(filePath).ToList();

            string username = fileLines.FirstOrDefault(p => p.Contains("username")).Split(':')[1];
            string password = fileLines.FirstOrDefault(p => p.Contains("password")).Split(':')[1];
            string clientId = fileLines.FirstOrDefault(p => p.Contains("clientId")).Split(':')[1];
            string clientSecret = fileLines.FirstOrDefault(p => p.Contains("clientSecret")).Split(':')[1];
            string redirectUri = fileLines.FirstOrDefault(p => p.Contains("redirectUri")).Split(new[] { ':' }, 2)[1]; //Split on first occurence only because redirectUri also contains ':'

            return new BotWebAgent(username, password, clientId, clientSecret, redirectUri);
        }

        public async Task<List<Post>> GetPosts(Subreddit subreddit, int amount = 100)
        {
            return await subreddit.GetPosts(Subreddit.Sort.New, amount).ToList();
        }

        public async Task<Dictionary<Subreddit, List<Comment>>> GetRecentComments()
        {
            Dictionary<Subreddit, List<Comment>> subredditsAndRecentComments = new Dictionary<Subreddit, List<Comment>>();
            foreach (Subreddit subreddit in Subreddits)
            {
                int amountOfCommentsToGet = subredditNamesAndCommentAmounts[subreddit.Name.ToLower()];
                subredditsAndRecentComments.Add(subreddit, await subreddit.GetComments(amountOfCommentsToGet, amountOfCommentsToGet).ToList());
            }

            return subredditsAndRecentComments;
        }

        public async Task<Comment> GetComment(Uri commentPermalink)
        {
            return await redditService.GetCommentAsync(new Uri(REDDITPREFIX + commentPermalink));
        }

        public async Task<Post> GetPost(string postId)
        {
            return await redditService.GetPostAsync(new Uri($"{REDDITPREFIX}/comments/{postId}"));
        }

        public async Task<Comment> ReplyToComment(Comment comment, string replyText)
        {
            IncrementActionAndCheckLimit();
            return await comment.ReplyAsync(replyText);
        }

        public async Task EditComment(Comment comment, string newText)
        {
            IncrementActionAndCheckLimit();
            await comment.EditTextAsync(newText);
        }

        public async Task DeleteComment(Comment comment)
        {
            IncrementActionAndCheckLimit();
            await comment.DelAsync();
        }

        public async Task<Comment> CommentOnPost(Post post, string comment)
        {
            IncrementActionAndCheckLimit();
            return await post.CommentAsync(comment);
        }

        public async Task<Post> GetParentPostForComment(Comment comment)
        {
            string postLink = GetPostLinkFromComment(comment);
            return await redditService.GetPostAsync(new Uri(GetFullLink(postLink)));
        }

        public async Task<List<PrivateMessage>> GetMessages()
        {
            return await redditService.User.GetPrivateMessages().ToList();
        }

        public static string GetFullLink(Uri relativeLink)
        {
            return GetFullLink(relativeLink.ToString());
        }

        public static string GetFullLink(string relativeLink)
        {
            return REDDITPREFIX + relativeLink;
        }

        public static string GetPostLinkFromComment(Comment comment)
        {
            return comment.Permalink.ToString().Replace(comment.Id + "/", "");
        }
    }
}
