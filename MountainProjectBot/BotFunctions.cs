using MountainProjectAPI;
using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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

        public static RedditHelper RedditHelper { get; set; }
        public static ConcurrentDictionary<string, ApprovalRequest> PostsPendingApproval { get; set; } = new ConcurrentDictionary<string, ApprovalRequest>();
        public static bool DryRun { get; set; }

        public static async Task CheckMonitoredComments()
        {
            monitoredComments.RemoveAll(c => (DateTime.Now - c.Created).TotalHours > c.ExpirationHours); //Remove any old monitors

            for (int i = monitoredComments.Count - 1; i >= 0; i--)
            {
                CommentMonitor monitor = monitoredComments[i];

                try
                {
                    Comment botResponseComment = null;
                    try
                    {
                        botResponseComment = await RedditHelper.GetComment(monitor.BotResponseComment.Permalink);
                    }
                    catch (Exception ex)
                    {
                        string discordMessage = $"Exception thrown when trying to get the bot's comment from a CommentMonitor ({monitor.BotResponseComment.Permalink}).";
                        using (WebClient client = new WebClient())
                        {
                            string htmlCode = client.DownloadString(RedditHelper.GetFullLink(monitor.BotResponseComment.Permalink));
                            if (htmlCode.Contains("There doesn't seem to be anything here"))
                            {
                                discordMessage += " Comment missing. Blocked by AutoModerator?";
                            }
                        }

                        discordMessage += $"\n\n{ex.Message}\n\n{ex.StackTrace}";
                        BotUtilities.SendDiscordMessage(discordMessage);

                        BotUtilities.WriteToConsoleWithColor($"Exception thrown when getting comment: {ex.Message}\n{ex.StackTrace}", ConsoleColor.Red);
                        BotUtilities.WriteToConsoleWithColor("Removing monitor...", ConsoleColor.Red);
                        monitoredComments.Remove(monitor);
                        continue;
                    }

                    if (!monitor.Alerted && botResponseComment.Comments.Any(c => Regex.IsMatch(c.Body, "bad bot", RegexOptions.IgnoreCase)))
                    {
                        BotUtilities.SendDiscordMessage($"There was a \"bad bot\" reply to this comment. Might want to investigate:\n\n{botResponseComment.Shortlink}");
                        monitor.Alerted = true;
                    }

                    if (monitor.Parent is Post && botResponseComment.Score <= -3)
                    {
                        await RedditHelper.DeleteComment(monitor.BotResponseComment);
                        monitoredComments.Remove(monitor);
                        BotUtilities.WriteToConsoleWithColor($"Deleted comment {monitor.BotResponseComment.Id} (score too low)", ConsoleColor.Green);

                        //If we've made a bad reply, update the Google sheet to reflect that
                        if (monitor.Parent is Post parentPost)
                            BotUtilities.LogBadReply(parentPost);

                        continue;
                    }

                    if (monitor.Parent is Comment parentComment)
                    {
                        string oldParentBody = parentComment.Body;
                        string oldResponseBody = monitor.BotResponseComment.Body;

                        Comment updatedParent = await RedditHelper.GetComment(parentComment.Permalink);
                        if (updatedParent.Body == "[deleted]" ||
                            (updatedParent.IsRemoved.HasValue && updatedParent.IsRemoved.Value)) //If comment is deleted or removed, delete the bot's response
                        {
                            await RedditHelper.DeleteComment(monitor.BotResponseComment);
                            monitoredComments.Remove(monitor);
                            BotUtilities.WriteToConsoleWithColor($"Deleted comment {monitor.BotResponseComment.Id} (parent deleted)", ConsoleColor.Green);
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
                                        BotUtilities.WriteToConsoleWithColor($"Edited comment {monitor.BotResponseComment.Id} (parent edited)", ConsoleColor.Green);
                                    }
                                    else
                                    {
                                        await RedditHelper.DeleteComment(monitor.BotResponseComment);
                                        monitoredComments.Remove(monitor);
                                        BotUtilities.WriteToConsoleWithColor($"Deleted comment {monitor.BotResponseComment.Id} (parent doesn't require a response)", ConsoleColor.Green);
                                    }
                                }

                                monitor.Parent = updatedParent;
                            }
                            else if (updatedParent.Body.Contains("mountainproject.com")) //If the parent comment's MP url has changed, edit the bot's response
                            {
                                string reply = BotReply.GetReplyForMPLinks(updatedParent);

                                if (reply != oldResponseBody)
                                {
                                    if (!string.IsNullOrEmpty(reply))
                                    {
                                        await RedditHelper.EditComment(monitor.BotResponseComment, reply);
                                        BotUtilities.WriteToConsoleWithColor($"Edited comment {monitor.BotResponseComment.Id} (parent edited)", ConsoleColor.Green);
                                    }
                                    else
                                    {
                                        await RedditHelper.DeleteComment(monitor.BotResponseComment);
                                        monitoredComments.Remove(monitor);
                                        BotUtilities.WriteToConsoleWithColor($"Deleted comment {monitor.BotResponseComment.Id} (parent doesn't require a response)", ConsoleColor.Green);
                                    }
                                }

                                monitor.Parent = updatedParent;
                            }
                            else  //If the parent comment is no longer a request or contains a MP url, delete the bot's response
                            {
                                await RedditHelper.DeleteComment(monitor.BotResponseComment);
                                monitoredComments.Remove(monitor);
                                BotUtilities.WriteToConsoleWithColor($"Deleted comment {monitor.BotResponseComment.Id} (parent doesn't require a response)", ConsoleColor.Green);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"\tException occured when checking monitor for comment {RedditHelper.GetFullLink(monitor.Parent.Permalink)}");
                    Console.WriteLine($"\t{e.Message}\n{e.StackTrace}");
                }
            }
        }

        public static async Task ReplyToApprovedPosts()
        {
            int removed = 0;
            foreach (string approvalRequestId in PostsPendingApproval.Keys) //Remove approval requests that have "timed out"
            {
                if ((DateTime.UtcNow - PostsPendingApproval[approvalRequestId].RedditPost.CreatedUTC).TotalMinutes > 30)
                {
                    //Try to remove until we are able (another thread may be accessing it at this time)
                    bool removeSuccess = PostsPendingApproval.TryRemove(approvalRequestId, out _);
                    while (!removeSuccess)
                    {
                        removeSuccess = PostsPendingApproval.TryRemove(approvalRequestId, out _);
                    }

                    removed++;
                }
            }

            if (removed > 0)
                BotUtilities.WriteToConsoleWithColor($"\tRemoved {removed} pending auto-replies that got too old", ConsoleColor.Red);

            List<ApprovalRequest> approvedPosts = PostsPendingApproval.Where(p => p.Value.IsApproved).Select(p => p.Value).ToList();
            foreach (ApprovalRequest approvalRequest in approvedPosts)
            {
                string reply = "";
                if (approvalRequest.ApproveFiltered)
                {
                    reply += BotReply.GetFormattedString(approvalRequest.SearchResult);
                    reply += Markdown.HRule;
                }
                else if (approvalRequest.ApproveAll)
                {
                    foreach (MPObject mpObject in approvalRequest.SearchResult.AllResults)
                    {
                        reply += BotReply.GetFormattedString(new SearchResult(mpObject, approvalRequest.SearchResult.RelatedLocation));
                        reply += Markdown.HRule;
                    }
                }

                reply += BotReply.GetBotLinks(approvalRequest.RedditPost);

                if (!DryRun)
                {
                    Comment botReplyComment = await RedditHelper.CommentOnPost(approvalRequest.RedditPost, reply);
                    monitoredComments.Add(new CommentMonitor() { Parent = approvalRequest.RedditPost, BotResponseComment = botReplyComment });
                    BotUtilities.WriteToConsoleWithColor($"\tAuto-replied to post {approvalRequest.RedditPost.Id}", ConsoleColor.Green);
                    BotUtilities.LogOrUpdateSpreadsheet(approvalRequest);
                }

                //Try to remove until we are able (another thread may be accessing it at this time)
                bool removeSuccess = PostsPendingApproval.TryRemove(approvalRequest.RedditPost.Id, out _);
                while (!removeSuccess)
                {
                    removeSuccess = PostsPendingApproval.TryRemove(approvalRequest.RedditPost.Id, out _);
                }
            }
        }

        public static async Task CheckPostsForAutoReply(List<Subreddit> subreddits)
        {
            List<Post> recentPosts = new List<Post>();
            foreach (Subreddit subreddit in subreddits)
            {
                List<Post> subredditPosts = await RedditHelper.GetPosts(subreddit, 10);
                subredditPosts = BotUtilities.RemoveAlreadySeenPosts(subredditPosts);
                subredditPosts = BotUtilities.RemoveBlacklisted(subredditPosts, new[] { BlacklistLevel.NoPostReplies, BlacklistLevel.OnlyKeywordReplies, BlacklistLevel.Total }); //Remove posts from users who don't want the bot to automatically reply to them

                foreach (Post post in subredditPosts.ToList())
                {
                    if (post.IsSelfPost)
                    {
                        subredditPosts.Remove(post);
                        BotUtilities.WriteToConsoleWithColor($"\tSkipping {post.Id} (self-post)", ConsoleColor.Red);
                        BotUtilities.LogPostBeenSeen(post, "self-post");
                    }

                    double ageInMin = (DateTime.UtcNow - post.CreatedUTC).TotalMinutes;
                    if (ageInMin > 30)
                    {
                        subredditPosts.Remove(post);
                        BotUtilities.WriteToConsoleWithColor($"\tSkipping {post.Id} (too old: {Math.Round(ageInMin,2)} min)", ConsoleColor.Red);
                        BotUtilities.LogPostBeenSeen(post, $"too old ({Math.Round(ageInMin, 2)} min)");
                    }
                }

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
                        ApprovalRequest approvalRequest = new ApprovalRequest
                        {
                            RedditPost = post,
                            SearchResult = searchResult
                        };

                        PostsPendingApproval.TryAdd(post.Id, approvalRequest);

                        BotUtilities.LogPostBeenSeen(post, searchResult.Confidence == 1 ? "auto-replying" : "pending approval");

                        if (!DryRun)
                        {
                            if (searchResult.Confidence == 1)
                            {
                                string reply = BotReply.GetFormattedString(searchResult);
                                reply += Markdown.HRule;
                                reply += BotReply.GetBotLinks(post);

                                Comment botReplyComment = await RedditHelper.CommentOnPost(post, reply);
                                monitoredComments.Add(new CommentMonitor() { Parent = post, BotResponseComment = botReplyComment });
                                BotUtilities.WriteToConsoleWithColor($"\n\tAuto-replied to post {post.Id}", ConsoleColor.Green);
                            }
                            else
                            {
                                //Until we are more confident with automatic results, we're going to request for approval for confidence values greater than 1 (less than 100%)
                                BotUtilities.WriteToConsoleWithColor($"\tRequesting approval for post {post.Id}", ConsoleColor.Yellow);
                                BotUtilities.RequestApproval(approvalRequest);
                            }

                            BotUtilities.LogOrUpdateSpreadsheet(approvalRequest);
                        }
                    }
                    else
                    {
                        Console.WriteLine("\tNothing found");
                        BotUtilities.LogPostBeenSeen(post, "nothing found");
                    }
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

                    if (!DryRun)
                    {
                        Comment botReplyComment = await RedditHelper.ReplyToComment(comment, reply);
                        BotUtilities.WriteToConsoleWithColor($"\tReplied to comment {comment.Id}", ConsoleColor.Green);
                        monitoredComments.Add(new CommentMonitor() { Parent = comment, BotResponseComment = botReplyComment });
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

                    if (!DryRun)
                    {
                        Comment botReplyComment = await RedditHelper.ReplyToComment(comment, reply);
                        BotUtilities.WriteToConsoleWithColor($"\tReplied to comment {comment.Id}", ConsoleColor.Green);
                        monitoredComments.Add(new CommentMonitor() { Parent = comment, BotResponseComment = botReplyComment });
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
