using MountainProjectModels;
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
using System.Xml.Serialization;

namespace MountainProjectBot
{
    class Program
    {
        static string xmlPath = @"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml";
        static string credentialsPath = @"..\Credentials.txt";
        static string repliedToCommentsPath = "RepliedTo.txt";
        static Reddit redditService;
        static Subreddit subReddit;
        static string botKeyword = "!MountainProject";

        static void Main(string[] args)
        {
            CheckRequiredFiles();
            AuthReddit().Wait();
            DoBotLoop().Wait();
        }

        private static void CheckRequiredFiles()
        {
            Console.WriteLine("Checking required files...");

            if (!File.Exists(xmlPath))
            {
                Console.WriteLine("The xml has not been built");
                ExitAfterKeyPress();
            }

            if (!File.Exists(credentialsPath))
            {
                Console.WriteLine("The credentials file does not exist");
                ExitAfterKeyPress();
            }

            Console.WriteLine("All required files present");
        }

        private static async Task AuthReddit()
        {
            Console.WriteLine("Authorizing Reddit...");

            WebAgent webAgent = GetWebAgentCredentialsFromFile();
            redditService = new Reddit(webAgent, true);
            subReddit = await redditService.GetSubredditAsync("/r/DerekAntProjectTest");

            Console.WriteLine("Reddit authed successfully");
        }

        private static WebAgent GetWebAgentCredentialsFromFile()
        {
            List<string> fileLines = File.ReadAllLines(credentialsPath).ToList();

            string username = fileLines.FirstOrDefault(p => p.Contains("username")).Split(':')[1];
            string password = fileLines.FirstOrDefault(p => p.Contains("password")).Split(':')[1];
            string clientId = fileLines.FirstOrDefault(p => p.Contains("clientId")).Split(':')[1];
            string clientSecret = fileLines.FirstOrDefault(p => p.Contains("clientSecret")).Split(':')[1];
            string redirectUri = fileLines.FirstOrDefault(p => p.Contains("redirectUri")).Split(':')[1];

            return new BotWebAgent(username, password, clientId, clientSecret, redirectUri);
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
            replyText += CreateMDLink("Feedback", "mailto://derekantrican@gmail.com&subject=Mountain%20Project%20Bot%20Feedback") + " | "; //Todo: make this a Google Form later (and somehow include comment id)
            replyText += CreateMDLink("Donate", "https://www.paypal.me/derekantrican") + " | ";
            replyText += CreateMDLink("GitHub", "https://github.com/derekantrican/MountainProjectScraper") + " | ";

            return replyText;
        }

        private static string GetFormattedString(MPObject inputMountainProjectObject)
        {
            string result = "I found the following info:\n\n";

            if (inputMountainProjectObject is Area)
            {
                Area inputArea = inputMountainProjectObject as Area;
                result += $"{inputArea.Name} [{inputArea.Statistics}]\n" +
                         inputArea.URL;

                //Todo: additional info to add
                // - located in {destArea}
                // - popular routes
            }
            else if (inputMountainProjectObject is Route)
            {
                Route inputRoute = inputMountainProjectObject as Route;
                result += $"{inputRoute.Name} [{inputRoute.Type} {inputRoute.Grade}";

                if (!string.IsNullOrEmpty(inputRoute.AdditionalInfo))
                    result += " " + inputRoute.AdditionalInfo;

                result += "]\n";
                result += inputRoute.URL;

                //Todo: additional info to add
                // - located in {destArea}
                // - # of bolts (if sport)
            }

            return result;
        }

        private static string SearchMountainProject(string searchText)
        {
            Console.WriteLine("Getting info from MountainProject");

            List<Area> destAreas = DeserializeAreas();
            if (destAreas.Count == 0)
                return null;

            MPObject result = DeepSearch(searchText, destAreas);
            if (string.IsNullOrEmpty(result.URL))
                return null;

            return GetFormattedString(result);
        }

        private static MPObject DeepSearch(string input, List<Area> destAreas)
        {
            foreach (Area destArea in destAreas)
            {
                if (input.ToLower().Contains(destArea.Name.ToLower()))
                {
                    //If we're matching the name of a destArea (eg a State), we'll assume that the route/area is within that state
                    return SearchSubAreasForMatch(input, destArea.SubAreas);
                }

                if (destArea.SubAreas != null &&
                    destArea.SubAreas.Count() > 0)
                    return SearchSubAreasForMatch(input, destArea.SubAreas);
            }

            return new MPObject(); //default
        }

        private static MPObject SearchSubAreasForMatch(string input, List<Area> subAreas)
        {
            foreach (Area subDestArea in subAreas)
            {
                if (input.Equals(subDestArea.Name, StringComparison.InvariantCultureIgnoreCase))
                    return subDestArea;

                if (subDestArea.SubAreas != null &&
                    subDestArea.SubAreas.Count() > 0)
                    return SearchSubAreasForMatch(input, subDestArea.SubAreas);

                if (subDestArea.Routes != null &&
                    subDestArea.Routes.Count() > 0)
                    return SearchRoutes(input, subDestArea.Routes);
            }

            return new MPObject(); //default
        }

        private static MPObject SearchRoutes(string input, List<Route> routes)
        {
            foreach (Route route in routes)
            {
                if (input.Equals(route.Name, StringComparison.InvariantCultureIgnoreCase))
                    return route;
            }

            return new MPObject(); //default
        }

        private static List<Area> DeserializeAreas()
        {
            FileStream fileStream = new FileStream(xmlPath, FileMode.Open);
            XmlSerializer xmlDeserializer = new XmlSerializer(typeof(List<Area>));
            return (List<Area>)xmlDeserializer.Deserialize(fileStream);
        }

        private static List<Comment> RemoveAlreadyRepliedTo(List<Comment> comments)
        {
            if (!File.Exists(repliedToCommentsPath))
                File.Create(repliedToCommentsPath).Close();

            string text = File.ReadAllText(repliedToCommentsPath);
            comments.RemoveAll(c => text.Contains(c.Id));

            return comments;
        }

        private static void LogCommentBeenRepliedTo(Comment comment)
        {
            if (!File.Exists(repliedToCommentsPath))
                File.Create(repliedToCommentsPath).Close();

            File.AppendAllText(repliedToCommentsPath, comment.Id);
        }

        private static string CreateMDLink(string linkText, string linkUrl)
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
