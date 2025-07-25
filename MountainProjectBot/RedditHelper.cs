﻿using Newtonsoft.Json.Linq;
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
        public const string REDDITBASEURL = "https://oauth.reddit.com";
        // Using the oauth url allows us to do stuff like getting the bot's comments from the bot's perspective (so we can see the comment even if it hasn't been approved yet).
        // Also, this doesn't seem to affect other things we do with the reddit API (RedditSharp's BotWebAgent uses oauth.reddit.com as the base url for everything anyway)

        WebAgent webAgent;
        Reddit redditService;
        Dictionary<string, int> subredditNamesAndCommentAmounts = new Dictionary<string, int>()
        {
            { "climbing", 10 /*100*/ /*1000*/ },
            { "climbingporn", 10 /*30*/ },
            { "bouldering", 10 /*100*/ /*600*/ },
            { "socalclimbing", 10 /*50*/ },
            { "climbingvids", 10 /*30*/ },
            { "mountainprojectbot", 10 /*100*/ /*500*/ },
            { "climbergirls", 10 /*100*/ /*200*/ },
            { "iceclimbing", 10 /*30*/ },
            { "rockclimbing", 10 /*50*/ },
            { "tradclimbing", 10 /*100*/ },
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
            try
            {
                return await redditService.GetCommentAsync(new Uri(REDDITBASEURL + commentPermalink));
                //BotUtilities.SendDiscordMessage($"AccessToken is {webAgent.AccessToken} and is valid until {(webAgent as BotWebAgent).TokenValidTo.ToLocalTime()}"); //TEMP
                //Comment comment = await redditService.GetCommentAsync(new Uri(REDDITBASEURL + commentPermalink));
                //BotUtilities.SendDiscordMessage($"Got comment {comment.Id} successfully (the normal way)");
                //return comment;
            }
            catch (Exception ex)
            {
				BotUtilities.SendDiscordMessage($"Exception encountered during GetComment: {ex}"); //TEMP

				Uri jsonUrl = new Uri(REDDITBASEURL + commentPermalink + ".json");
				string commentJson = await GetCommentAlternate(jsonUrl);
				//BotUtilities.SendDiscordMessage($"{jsonUrl} :\n\n{commentJson}"); //TEMP
                try
                {
				    Comment comment = Comment.Parse(webAgent, JToken.Parse(commentJson)) as Comment;
				    BotUtilities.SendDiscordMessage($"GetCommentAlternate worked as a fallback"); //TEMP
                    return comment;
                }
                catch
                {
					BotUtilities.SendDiscordMessage($"Unable to parse:\n{commentJson}"); //TEMP
                    throw;
				}
			}
        }

        public async Task<string> GetCommentAlternate(Uri commentPermalink)
        {
			using (HttpClient client = new HttpClient())
			{
				HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, commentPermalink);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0");

                using (HttpResponseMessage response = await client.SendAsync(request))
				{
					return await response.Content.ReadAsStringAsync();
				}
			}
		}

        public async Task<Post> GetPost(string postId)
        {
            // This has a few different fallbacks because the method on line 127 *used* to work, but
            // in the last few months has been throwing 443 errors (and I have no idea why). So I have
            // added some fallbacks.

            try
            {
                return await redditService.GetPostAsync(new Uri($"{REDDITBASEURL}/comments/{postId}"));
                //BotUtilities.SendDiscordMessage($"AccessToken is {webAgent.AccessToken} and is valid until {(webAgent as BotWebAgent).TokenValidTo.ToLocalTime()}"); //TEMP
                //Post post = await redditService.GetPostAsync(new Uri($"{REDDITBASEURL}/comments/{postId}"));
                //BotUtilities.SendDiscordMessage($"Got post {postId} successfully (the normal way)");
                //return post;
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
					BotUtilities.SendDiscordMessage($"https://reddit.com/{postId}.json :\n\n{postJson}"); //TEMP
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
				HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0");

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
