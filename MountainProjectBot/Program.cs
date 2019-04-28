using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MountainProjectBot
{
    class Program
    {
        static string xmlPath = @"C:\Users\derek.antrican\source\repos\MountainProjectDBBuilder\MountainProjectDBBuilder\bin\Debug\MountainProjectAreas.xml";
        static string dbEXE = @"C:\Users\derek.antrican\source\repos\MountainProjectDBBuilder\MountainProjectDBBuilder\bin\Debug\MountainProjectDBBuilder.exe";
        static string repliedToComments = "RepliedTo.txt";
        static Reddit redditService;
        static Subreddit subReddit;
        static string botKeyword = "!MountainProject";

        static void Main(string[] args)
        {
            //CheckRequiredPaths();
            AuthReddit().Wait();
            DoBotLoop().Wait();
        }

        private static void CheckRequiredPaths()
        {
            Console.WriteLine("Checking required paths...");

            if (!File.Exists(xmlPath))
            {
                Console.WriteLine("The xml has not been built");
                ExitAfterKeyPress();
            }

            if (!File.Exists(dbEXE))
            {
                Console.WriteLine("The db reader does not exist");
                ExitAfterKeyPress();
            }

            Console.WriteLine("All required paths present");
        }

        private static async Task AuthReddit()
        {
            Console.WriteLine("Authorizing Reddit...");

            //These things should not be here if I'm going to publish it to github. Read from an xml file instead
            WebAgent webAgent = new BotWebAgent("MountainProjectBot", "Helenelove9", "WvZ1ULvIBGunVw", "i884CTdWsnUn9dcrWL3CCmFhho8", "http://example.com");
            redditService = new Reddit(webAgent, true);
            subReddit = await redditService.GetSubredditAsync("/r/DerekAntProjectTest");

            Console.WriteLine("Reddit authed successfully");
        }

        private static async Task DoBotLoop()
        {
            while (true)
            {
                //Get the latest 1000 comments on the subreddit, the filter to the ones that have the keyword
                //and have not already been replied to
                Console.WriteLine("Getting comments...");
                List<Comment> comments = await subReddit.GetComments(1000, 1000).Where(c => c.Body.Contains(botKeyword)).ToList();
                comments = RemoveAlreadyRepliedTo(comments);

                foreach (Comment comment in comments)
                {
                    string reply = GetReplyForComment(comment);
                    if (!string.IsNullOrEmpty(reply))
                    {
                        try
                        {
                            await comment.ReplyAsync(reply);
                            Console.WriteLine($"Replied to comment {comment.Id}");
                            LogCommentBeenRepliedTo(comment);
                        }
                        catch (RateLimitException ex)
                        {
                            Console.WriteLine("Rate limit hit. Postponing reply until next iteration");
                        }
                    }
                }

                Console.WriteLine("Sleeping for 10 seconds...");
                Thread.Sleep(10000); //Sleep for 10 seconds so as not to overload reddit
            }
        }

        private static string GetReplyForComment(Comment replyTo)
        {
            Console.WriteLine("Getting reply for comment");

            string queryText = replyTo.Body.Replace(botKeyword, "").Trim();
            string routeInfo = SearchMountainProject(queryText);
            if (string.IsNullOrEmpty(routeInfo))
                return null;

            string replyText = routeInfo;

            //Todo: here we could also add text like the auto tldr bot has (eg "feedback", "github", "donate" links).
            //On top of that, we can add the "replyTo" comment id into those links so if there is a bug
            //reported, we can quickly find the source comment

            replyText += "\n\n";
            replyText += CreateLink("Feedback", "mailto://derekantrican@gmail.com&subject=Mountain%20Project%20Bot%20Feedback") + " | "; //Todo: make this a Google Form later (and somehow include comment id)
            replyText += CreateLink("Donate", "https://www.paypal.me/derekantrican") + " | ";
            replyText += CreateLink("GitHub", "https://github.com/derekantrican/MountainProjectScraper") + " | "; //Todo: direct this to the proper GitHub later

            return replyText;
        }

        private static string SearchMountainProject(string searchText)
        {
            Console.WriteLine("Getting info from MountainProject");

            Process p = new Process();
            p.StartInfo = new ProcessStartInfo()
            {
                FileName = dbEXE,
                Arguments = $"-parsedirect \"{searchText}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            p.Start();

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            return output;
        }

        private static List<Comment> RemoveAlreadyRepliedTo(List<Comment> comments)
        {
            if (!File.Exists(repliedToComments))
                File.Create(repliedToComments).Close();

            string text = File.ReadAllText(repliedToComments);
            comments.RemoveAll(c => text.Contains(c.Id));

            return comments;
        }

        private static void LogCommentBeenRepliedTo(Comment comment)
        {
            if (!File.Exists(repliedToComments))
                File.Create(repliedToComments).Close();

            File.AppendAllText(repliedToComments, comment.Id);
        }

        private static string CreateLink(string linkText, string linkUrl)
        {
            return $"[{linkText}]({linkUrl})";
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
