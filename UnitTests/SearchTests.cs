﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectAPI;
using MountainProjectBot;
using System.Text.RegularExpressions;

namespace UnitTests
{
    [TestClass]
    public class SearchTests
    {
        string[,] testCriteria_search = new string[,]
        {
            { "Red River Gorge", "/area/105841134/red-river-gorge" },
            { "Exit 38: Deception Crags", "/area/105791955/exit-38-deception-crags" },
            { "Deception Crags", "/area/105791955/exit-38-deception-crags" }, //Partial name match
            { "Helm's Deep", "/route/106887440/helms-deep" }, //Special character (apostrophe)
            { "Helm’s Deep", "/route/106887440/helms-deep" }  //Special character (single quotation mark)
        };

        [TestMethod]
        public void TestSearch()
        {
            MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");

            for (int i = 0; i < testCriteria_search.GetLength(0); i++)
            {
                string query = testCriteria_search[i, 0];
                string expectedUrl = testCriteria_search[i, 1];
                SearchResult result = MountainProjectDataSearch.Search(query);

                Assert.AreEqual(Utilities.MPBASEURL + expectedUrl, result.FilteredResult.URL);
            }
        }

        string[,] testCriteria_location = new string[,]
        {
            { "Red River Gorge", "Kentucky" },
            { "Exit 38: Deception Crags", "Exit 38, Washington" },
            { "Deception Crags", "Exit 38, Washington" },
            { "no-rang-na-rang", "South Korea" }, //International
            { "Sweet Dreams", "Blue Mountains, Australia"}, //Australia route (special case for Australia)
            { "Grab Your Balls", "Breakneck, Pennsylvania" } //https://github.com/derekantrican/MountainProject/issues/12
        };

        [TestMethod]
        public void TestLocationString()
        {
            MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");

            for (int i = 0; i < testCriteria_location.GetLength(0); i++)
            {
                string query = testCriteria_location[i, 0];
                string expectedLocation = testCriteria_location[i, 1];
                SearchResult searchResult = MountainProjectDataSearch.Search(query);
                string resultLocation = BotReply.GetLocationString(searchResult.FilteredResult);
                resultLocation = resultLocation.Replace(Markdown.NewLine, ""); //Remove markdown newline
                resultLocation = resultLocation.Replace("Located in ", ""); //Simplify results for unit test
                resultLocation = Regex.Replace(resultLocation, @"\[|\]|\(.*?\)", ""); //Remove markdown link formatting

                Assert.AreEqual(expectedLocation, resultLocation);
            }
        }

        string[,] testCriteria_keyword = new string[,]
        {
            { "!MountainProject deception crags", "/area/105791955/exit-38-deception-crags" },
            { "!MountainProject derekantrican", "I could not find anything" }, //No results
            { "This is a test !MountainProject red river gorge", "/area/105841134/red-river-gorge" }, //Keyword not at beginning
            { "!MountainProject Royale with Cheese -location:Niagara Glen", "/route/114609759/royale-with-cheese" }, //Location operator
            { "!MountainProject Moonstone, Arizona -route", "/route/105960200/moonstone" }, //Location parse (via comma) - TODO: take out "-route" when we start prioritizing levenshtein distance
            { "!MountainProject West Ridge of Prusik Peak", "/route/105808527/west-ridge" }, //Location parse (via word)
            { "!MountainProject Send me on my Way at Red River Gorge", "/route/106085043/send-me-on-my-way" }, //Location parse (via word WITH multiple location words: on, at)
            { "!MountainProject Send me on my Way -location:Exit 38", "I could not find anything" }, //Wrong location
            { "!MountainProject Send me on my Way at derekantrican", "I could not find anything" } //Non-existent location
        };

        [TestMethod]
        public void TestCommentBodyParse()
        {
            MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");

            for (int i = 0; i < testCriteria_keyword.GetLength(0); i++)
            {
                string commentBody = testCriteria_keyword[i, 0];
                string expectedUrl = testCriteria_keyword[i, 1];
                string resultReply = BotReply.GetReplyForRequest(commentBody);

                Assert.IsTrue(resultReply.Contains(expectedUrl), "Failed for " + commentBody);
            }
        }
    }
}
