using Newtonsoft.Json.Linq;
using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MountainProjectBot
{
    public class RedditHelper
    {
        public const string REDDITBASEURL = "https://reddit.com";
        WebAgent webAgent;
        Reddit redditService;
        Dictionary<string, int> subredditNamesAndCommentAmounts = new Dictionary<string, int>()
        {
            { "climbing", 100 /*1000*/ },
            { "climbingporn", 30 },
            { "bouldering", 100 /*600*/ },
            { "socalclimbing", 50 },
            { "climbingvids", 30 },
            { "mountainprojectbot", 100 /*500*/ },
            { "climbergirls", 100 /*200*/ },
            { "iceclimbing", 30 },
            { "rockclimbing", 50 },
            { "tradclimbing", 100 },
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

            webAgent = GetWebAgentCredentialsFromFile(filePath);
            redditService = new Reddit(webAgent, true);

            foreach (string subRedditName in subredditNamesAndCommentAmounts.Keys)
            {
                try
                {
                    Subreddits.Add(await redditService.GetSubredditAsync(subRedditName));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not add subreddit {subRedditName}: {ex.Message}");
                }
            }

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
            return await subreddit.GetPosts(Subreddit.Sort.New, amount).ToListAsync();
        }

        public async Task<Dictionary<Subreddit, List<Comment>>> GetRecentComments()
        {
            Dictionary<Subreddit, List<Comment>> subredditsAndRecentComments = new Dictionary<Subreddit, List<Comment>>();
            foreach (Subreddit subreddit in Subreddits)
            {
                int amountOfCommentsToGet = subredditNamesAndCommentAmounts[subreddit.Name.ToLower()];
                List<Comment> comments = await subreddit.GetComments(amountOfCommentsToGet, amountOfCommentsToGet).ToListAsync();
                subredditsAndRecentComments.Add(subreddit, comments);

                int minAgo = 15;
                Console.WriteLine($"Got {comments.Count} comments from {subreddit.Name}. {comments.Count(c => c.CreatedUTC > DateTime.UtcNow.AddMinutes(-1 * minAgo))} are from the last {minAgo} min.");
            }

            return subredditsAndRecentComments;
        }

        public async Task<List<Comment>> GetOwnComments()
        {
            RedditUser botUser = await redditService.GetUserAsync("MountainProjectBot");
            return await botUser.GetComments(100).ToListAsync();
        }

        public async Task<Thing> GetThing(string id)
        {
            return await redditService.GetThingByFullnameAsync(id);
        }

        public async Task<Comment> GetComment(Uri commentPermalink)
        {
            return await redditService.GetCommentAsync(new Uri(REDDITBASEURL + commentPermalink));
        }

        public async Task<Post> GetPost(string postId)
        {
            // This has a few different fallbacks because the method on line 127 *used* to work, but
            // in the last few months has been throwing 443 errors (and I have no idea why). So I have
            // added some fallbacks.

            try
            {
                return await redditService.GetPostAsync(new Uri($"{REDDITBASEURL}/comments/{postId}"));
            }
            catch
            {
                try
                {
                    Post post = await redditService.GetPostAsync(new Uri($"{REDDITBASEURL}/{postId}"));
                    BotUtilities.SendDiscordMessage($"GetPostAsync (without '/comments/') worked as a fallback"); //TEMP
                    return post;
                }
                catch
                {
                    string postJson = await GetPostAlternate(postId);
                    Post post = Post.Parse(webAgent, JToken.Parse(postJson)) as Post;
					BotUtilities.SendDiscordMessage($"GetPostAlternate worked as a fallback"); //TEMP
					return post;
                }
			}
        }

        public async Task<string> GetPostAlternate(string postId)
        {
            string url = $"https://reddit.com/{postId}.json";
			using (HttpClient client = new HttpClient())
			{
				client.Timeout = TimeSpan.FromSeconds(3);
				HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Head, url);

				using (HttpResponseMessage response = await client.SendAsync(request))
				{
					return await response.Content.ReadAsStringAsync();
				}
			}
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
            return await redditService.User.GetPrivateMessages().ToListAsync();
        }

        public static string GetFullLink(Uri relativeLink)
        {
            return GetFullLink(relativeLink.ToString());
        }

        public static string GetFullLink(string relativeLink)
        {
            return REDDITBASEURL + relativeLink;
        }

        public static string GetPostLinkFromComment(Comment comment)
        {
            return comment.Permalink.ToString().Replace(comment.Id + "/", "");
        }
    }
}
