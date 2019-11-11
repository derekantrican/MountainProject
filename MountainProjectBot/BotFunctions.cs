using MountainProjectAPI;
using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static MountainProjectBot.Enums;

namespace MountainProjectBot
{
    public static class BotFunctions
    {
        private const string BOTKEYWORDREGEX = @"(?i)!mountain\s*project";
        private static List<CommentMonitor> monitoredComments = new List<CommentMonitor>();
        private static List<KeyValuePair<Post, SearchResult>> postsPendingApproval = new List<KeyValuePair<Post, SearchResult>>();

        public static RedditHelper RedditHelper;

        public static async Task CheckMonitoredComments()
        {
            monitoredComments.RemoveAll(c => (DateTime.Now - c.Created).TotalHours > 1); //Remove any old monitors

            for (int i = monitoredComments.Count - 1; i >= 0; i--)
            {
                CommentMonitor monitor = monitoredComments[i];

                string oldParentBody = monitor.ParentComment.Body;
                string oldResponseBody = monitor.BotResponseComment.Body;

                try
                {
                    Comment updatedParent = await RedditHelper.GetComment(monitor.ParentComment.Permalink);
                    if (updatedParent.Body == "[deleted]" ||
                        (updatedParent.IsRemoved.HasValue && updatedParent.IsRemoved.Value)) //If comment is deleted or removed, delete the bot's response
                    {
                        await RedditHelper.DeleteComment(monitor.BotResponseComment);
                        monitoredComments.Remove(monitor);
                        BotUtilities.WriteToConsoleWithColor($"Deleted comment {monitor.BotResponseComment.Id}", ConsoleColor.Green);
                    }
                    else if (updatedParent.Body != oldParentBody) //If the parent comment's request has changed, edit the bot's response
                    {
                        if (Regex.IsMatch(updatedParent.Body, BOTKEYWORDREGEX))
                        {
                            string reply = BotReply.GetReplyForRequest(updatedParent);

                            if (reply != oldResponseBody)
                            {
                                if (!string.IsNullOrEmpty(reply))
                                {
                                    await RedditHelper.EditComment(monitor.BotResponseComment, reply);
                                    BotUtilities.WriteToConsoleWithColor($"Edited comment {monitor.BotResponseComment.Id}", ConsoleColor.Green);
                                }
                                else
                                {
                                    await RedditHelper.DeleteComment(monitor.BotResponseComment);
                                    monitoredComments.Remove(monitor);
                                    BotUtilities.WriteToConsoleWithColor($"Deleted comment {monitor.BotResponseComment.Id}", ConsoleColor.Green);
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
                                {
                                    await RedditHelper.EditComment(monitor.BotResponseComment, reply);
                                    BotUtilities.WriteToConsoleWithColor($"Edited comment {monitor.BotResponseComment.Id}", ConsoleColor.Green);
                                }
                                else
                                {
                                    await RedditHelper.DeleteComment(monitor.BotResponseComment);
                                    monitoredComments.Remove(monitor);
                                    BotUtilities.WriteToConsoleWithColor($"Deleted comment {monitor.BotResponseComment.Id}", ConsoleColor.Green);
                                }
                            }

                            monitor.ParentComment = updatedParent;
                        }
                        else  //If the parent comment is no longer a request or contains a MP url, delete the bot's response
                        {
                            await RedditHelper.DeleteComment(monitor.BotResponseComment);
                            monitoredComments.Remove(monitor);
                            BotUtilities.WriteToConsoleWithColor($"Deleted comment {monitor.BotResponseComment.Id}", ConsoleColor.Green);
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

        public static async Task ReplyToApprovedPosts()
        {
            int removed = postsPendingApproval.RemoveAll(p => (DateTime.UtcNow - p.Key.CreatedUTC).TotalMinutes > 15); //Remove posts that have "timed out"
            if (removed > 0)
                BotUtilities.WriteToConsoleWithColor($"\tRemoved {removed} pending auto-replies that got too old", ConsoleColor.Red);

            if (postsPendingApproval.Count == 0)
                return;

            List<string> approvedUrls = BotUtilities.GetApprovedPostUrls();
            List<KeyValuePair<Post, SearchResult>> approvedPosts = postsPendingApproval.Where(p => approvedUrls.Contains(p.Key.Shortlink) || p.Value.Confidence == 1).ToList();
            foreach (KeyValuePair<Post, SearchResult> post in approvedPosts)
            {
                string reply = BotReply.GetFormattedString(post.Value);
                reply += Markdown.HRule;
                reply += BotReply.GetBotLinks(post.Key);

                if (!Debugger.IsAttached)
                {
                    await RedditHelper.CommentOnPost(post.Key, reply);
                    BotUtilities.WriteToConsoleWithColor($"\n\tAuto-replied to post {post.Key.Id}", ConsoleColor.Green);
                }

                postsPendingApproval.RemoveAll(p => p.Key == post.Key);
            }
        }

        public static async Task CheckPostsForAutoReply(List<Subreddit> subreddits)
        {
            List<Post> recentPosts = new List<Post>();
            foreach (Subreddit subreddit in subreddits)
            {
                List<Post> subredditPosts = await RedditHelper.GetPosts(subreddit, 10);
                subredditPosts = BotUtilities.RemoveAlreadyRepliedTo(subredditPosts);
                subredditPosts.RemoveAll(p => p.IsSelfPost);
                subredditPosts.RemoveAll(p => (DateTime.UtcNow - p.CreatedUTC).TotalMinutes > 10); //Only check recent posts
                //subredditPosts.RemoveAll(p => (DateTime.UtcNow - p.CreatedUTC).TotalMinutes < 3); //Wait till posts are 3 minutes old (gives poster time to add a comment with a MP link or for the ClimbingRouteBot to respond)
                subredditPosts = BotUtilities.RemoveBlacklisted(subredditPosts, new[] { BlacklistLevel.NoPostReplies, BlacklistLevel.OnlyKeywordReplies, BlacklistLevel.Total }); //Remove posts from users who don't want the bot to automatically reply to them
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
                            BotUtilities.WriteToConsoleWithColor($"\tRequesting approval for post {post.Id}", ConsoleColor.Yellow);

                            string locationString = Regex.Replace(BotReply.GetLocationString(searchResult.FilteredResult), @"\[|\]\(.*?\)", "").Replace("Located in ", "").Replace("\n", "");
                            BotUtilities.NotifyFoundPost(WebUtility.HtmlDecode(post.Title), post.Shortlink, searchResult.FilteredResult.Name, locationString,
                                                         (searchResult.FilteredResult as Route).GetRouteGrade(Grade.GradeSystem.YDS).ToString(false), 
                                                         searchResult.FilteredResult.URL, searchResult.FilteredResult.ID);
                        }
                    }
                    else
                        Console.WriteLine("\tNothing found");

                    BotUtilities.LogPostBeenSeen(post);
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

        public static async Task RespondToRequests(List<Comment> recentComments) //Respond to comments that specifically called the bot (!MountainProject)
        {
            List<Comment> botRequestComments = recentComments.Where(c => Regex.IsMatch(c.Body, BOTKEYWORDREGEX)).ToList();
            botRequestComments = BotUtilities.RemoveAlreadyRepliedTo(botRequestComments);
            botRequestComments.RemoveAll(c => c.IsArchived);
            botRequestComments = BotUtilities.RemoveBlacklisted(botRequestComments, new[] { BlacklistLevel.Total }); //Don't reply to bots

            foreach (Comment comment in botRequestComments)
            {
                try
                {
                    Console.WriteLine($"\tGetting reply for comment: {comment.Id}");

                    string reply = BotReply.GetReplyForRequest(comment);

                    if (!Debugger.IsAttached)
                    {
                        Comment botReplyComment = await RedditHelper.ReplyToComment(comment, reply);
                        BotUtilities.WriteToConsoleWithColor($"\tReplied to comment {comment.Id}", ConsoleColor.Green);
                        monitoredComments.Add(new CommentMonitor() { ParentComment = comment, BotResponseComment = botReplyComment });
                    }

                    BotUtilities.LogCommentBeenRepliedTo(comment);
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

        public static async Task RespondToMPUrls(Dictionary<Subreddit, List<Comment>> subredditsAndRecentComments) //Respond to comments that have a mountainproject url
        {
            foreach (Subreddit subreddit in subredditsAndRecentComments.Keys.ToList())
            {
                List<Comment> filteredComments = subredditsAndRecentComments[subreddit].Where(c => c.Body.Contains("mountainproject.com")).ToList();
                filteredComments = BotUtilities.RemoveAlreadyRepliedTo(filteredComments);
                filteredComments.RemoveAll(c => c.IsArchived);
                filteredComments = BotUtilities.RemoveBlacklisted(filteredComments, new[] { BlacklistLevel.OnlyKeywordReplies, BlacklistLevel.Total }); //Remove comments from users who don't want the bot to automatically reply to them
                filteredComments = await BotUtilities.RemoveCommentsOnSelfPosts(subreddit, filteredComments); //Don't reply to self posts (aka text posts)
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
                        BotUtilities.LogCommentBeenRepliedTo(comment); //Don't check this comment again
                        continue;
                    }

                    if (!Debugger.IsAttached)
                    {
                        Comment botReplyComment = await RedditHelper.ReplyToComment(comment, reply);
                        BotUtilities.WriteToConsoleWithColor($"\tReplied to comment {comment.Id}", ConsoleColor.Green);
                        monitoredComments.Add(new CommentMonitor() { ParentComment = comment, BotResponseComment = botReplyComment });
                    }

                    BotUtilities.LogCommentBeenRepliedTo(comment);
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
    }
}
