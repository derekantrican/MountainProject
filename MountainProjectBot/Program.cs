﻿using Common;
using MountainProjectModels;
using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MountainProjectBot
{
    class Program
    {
        const string XMLPATH = @"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml";
        const string CREDENTIALSPATH = @"..\Credentials.txt";
        const string REPLIEDTOCOMMENTSPATH = "RepliedTo.txt";
        const string SUBREDDITNAME = "/r/DerekAntProjectTest";
        const string BOTKEYWORD = "!MountainProject";

        static Reddit redditService;
        static Subreddit subReddit;

        static void Main(string[] args)
        {
            CheckRequiredFiles();
            MountainProjectDataSearch.InitMountainProjectData(XMLPATH);
            AuthReddit().Wait();
            DoBotLoop().Wait();
        }

        private static void CheckRequiredFiles()
        {
            Console.WriteLine("Checking required files...");

            if (!File.Exists(XMLPATH))
            {
                Console.WriteLine("The xml has not been built");
                ExitAfterKeyPress();
            }

            if (!File.Exists(CREDENTIALSPATH))
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
            subReddit = await redditService.GetSubredditAsync(SUBREDDITNAME);

            Console.WriteLine("Reddit authed successfully");
        }

        private static WebAgent GetWebAgentCredentialsFromFile()
        {
            List<string> fileLines = File.ReadAllLines(CREDENTIALSPATH).ToList();

            string username = fileLines.FirstOrDefault(p => p.Contains("username")).Split(':')[1];
            string password = fileLines.FirstOrDefault(p => p.Contains("password")).Split(':')[1];
            string clientId = fileLines.FirstOrDefault(p => p.Contains("clientId")).Split(':')[1];
            string clientSecret = fileLines.FirstOrDefault(p => p.Contains("clientSecret")).Split(':')[1];
            string redirectUri = fileLines.FirstOrDefault(p => p.Contains("redirectUri")).Split(new[] { ':' }, 2)[1]; //Split on first occurence only because redirectUri also contains ':'

            return new BotWebAgent(username, password, clientId, clientSecret, redirectUri);
        }

        private static async Task DoBotLoop()
        {
            while (true)
            {
                //Get the latest 1000 comments on the subreddit, the filter to the ones that have the keyword
                //and have not already been replied to
                Console.WriteLine("Getting comments...");
                List<Comment> comments = await subReddit.GetComments(1000, 1000).Where(c => c.Body.Contains(BOTKEYWORD)).ToList();
                comments = RemoveAlreadyRepliedTo(comments);

                foreach (Comment comment in comments)
                {
                    try
                    {
                        string reply = GetReplyForComment(comment);
                        await comment.ReplyAsync(reply);
                        Console.WriteLine($"Replied to comment {comment.Id}");
                        LogCommentBeenRepliedTo(comment);
                    }
                    catch (RateLimitException)
                    {
                        Console.WriteLine("Rate limit hit. Postponing reply until next iteration");
                    }
                }

                Console.WriteLine("Sleeping for 10 seconds...");
                Thread.Sleep(10000); //Sleep for 10 seconds so as not to overload reddit
            }
        }

        private static string GetReplyForComment(Comment replyTo)
        {
            Console.WriteLine("Getting reply for comment");

            string queryText = replyTo.Body.Replace(BOTKEYWORD, "").Trim();
            MPObject searchResult = MountainProjectDataSearch.SearchMountainProject(queryText);
            string replyText = GetFormattedString(searchResult);
            if (string.IsNullOrEmpty(replyText))
                replyText = $"I could not find anything for \"{queryText}\". Please use the Feedback button below if you think this is a bug";

            replyText += "\n\n-----\nBot Links: ";
            string commentLink = WebUtility.HtmlEncode("https://reddit.com" + replyTo.Permalink);
            replyText += CreateMDLink("Feedback", "https://docs.google.com/forms/d/e/1FAIpQLSchgbXwXMylhtbA8kXFycZenSKpCMZjmYWMZcqREl_OlCm4Ew/viewform?usp=pp_url&entry.266808192=" + commentLink) + " | ";
            replyText += CreateMDLink("Donate", "https://www.paypal.me/derekantrican") + " | ";
            replyText += CreateMDLink("GitHub", "https://github.com/derekantrican/MountainProject") + " | ";

            return replyText;
        }

        private static string GetFormattedString(MPObject inputMountainProjectObject)
        {
            if (inputMountainProjectObject == null)
                return null;

            string result = "I found the following info:\n\n";

            if (inputMountainProjectObject is Area)
            {
                Area inputArea = inputMountainProjectObject as Area;
                result += $"{inputArea.Name} [{inputArea.Statistics}]\n\n";
                result += GetLocationString(inputArea);

                //Todo: additional info to add
                // - popular routes (this can be parsed from the "Classic Climbing Routes" section on an Area's page)
                // - description?

                result += inputArea.URL;
            }
            else if (inputMountainProjectObject is Route)
            {
                Route inputRoute = inputMountainProjectObject as Route;
                result += $"{inputRoute.Name} [{inputRoute.TypeString} {inputRoute.Grade},";

                if (!string.IsNullOrEmpty(inputRoute.AdditionalInfo))
                    result += " " + inputRoute.AdditionalInfo;

                result += "]\n\n";

                result += GetLocationString(inputRoute);

                //Todo: additional info to add
                // - description?

                result += inputRoute.URL;
            }

            return result;
        }

        private static string GetLocationString(MPObject child)
        {
            MPObject immediateParent = MountainProjectDataSearch.GetParent(child, -1); //Get immediate parent (eg the wall that the route is on)
            if (immediateParent == null ||  //If "child" is a dest area, the parent will be "All Locations" which won't be in our directory
                immediateParent.URL == Utilities.INTERNATIONALURL) //If "child" is an area like "Europe"
                return "";

            MPObject outerParent = MountainProjectDataSearch.GetParent(child, 1); //Get state that route/area is in
            if (outerParent.URL == Utilities.INTERNATIONALURL)
            {
                if (child.ParentUrls.Count > 3)
                    outerParent = MountainProjectDataSearch.GetParent(child, 3); //If this is international, get the country instead of the state (eg "China")
                else
                    return ""; //Return a blank string if we are in an area like "China" (so we don't return a string like "China is located in Asia")
            }

            string locationString = $"Located in {immediateParent.Name}";
            if (outerParent != null && outerParent.URL != immediateParent.URL)
                locationString += $", {outerParent.Name}";

            locationString += "\n\n";

            return locationString;
        }

        private static List<Comment> RemoveAlreadyRepliedTo(List<Comment> comments)
        {
            if (!File.Exists(REPLIEDTOCOMMENTSPATH))
                File.Create(REPLIEDTOCOMMENTSPATH).Close();

            string text = File.ReadAllText(REPLIEDTOCOMMENTSPATH);
            comments.RemoveAll(c => text.Contains(c.Id));

            return comments;
        }

        private static void LogCommentBeenRepliedTo(Comment comment)
        {
            if (!File.Exists(REPLIEDTOCOMMENTSPATH))
                File.Create(REPLIEDTOCOMMENTSPATH).Close();

            File.AppendAllText(REPLIEDTOCOMMENTSPATH, comment.Id);
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
