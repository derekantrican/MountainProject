using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            { "Helm’s Deep", "/route/106887440/helms-deep" },  //Special character (single quotation mark)
            { "Royale with Cheese -location:Niagara Glen", "/route/114609759/royale-with-cheese" }, //Location operator
            { "Moonstone, Arizona -route", "/route/105960200/moonstone" }, //Location parse (via comma) - TODO: take out "-route" when we start prioritizing levenshtein distance
            { "West Ridge of Prusik Peak", "/route/105808527/west-ridge" }, //Location parse (via word)
            { "Send me on my Way at Red River Gorge", "/route/106085043/send-me-on-my-way" }, //Location parse (via word WITH multiple location words: on, at)
            { "Edge of Time", "/route/105756826/edge-of-time" }, //Location parse possibly matches "Edge of Space" in a place that contains "CochiTI MEsa" (TIME)
            { "Send me on my Way -location:Exit 38", null }, //Wrong location
            { "Send me on my Way at derekantrican", null }, //Non-existent location
            { "Lifeline, Portland", "/route/113696621/lifeline" }, //Location that will likely match something else first ("Portland" should be UK but more likely matches Oregon)
            { "Sin Gaz", "/route/108244424/sin-gaz" } //The text "singaz" is contained by a higher popularity route. This is more a test for "DetermineBestMatch"
        };

        [TestMethod]
        public void TestSearch()
        {
            MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");

            for (int i = 0; i < testCriteria_search.GetLength(0); i++)
            {
                string query = testCriteria_search[i, 0];
                string expectedUrl = testCriteria_search[i, 1];

                _ = ResultParameters.ParseParameters(ref query); //This is here just to filter out any query items (not to be used)
                SearchParameters searchParameters = SearchParameters.ParseParameters(ref query);

                SearchResult searchResult = MountainProjectDataSearch.Search(query, searchParameters);

                if (string.IsNullOrEmpty(expectedUrl))
                    Assert.IsNull(searchResult.FilteredResult, "Failed for " + testCriteria_search[i, 0]);
                else
                    Assert.AreEqual(Utilities.MPBASEURL + expectedUrl, searchResult.FilteredResult.URL, "Failed for " + testCriteria_search[i, 0]);

                Assert.IsTrue(searchResult.TimeTaken.TotalSeconds < 5, $"{query} took too long ({searchResult.TimeTaken.TotalMilliseconds} ms)");
            }
        }

        string[,] testCriteria_location = new string[,]
        {
            { "Red River Gorge", "Kentucky" },
            { "Exit 38: Deception Crags", "Exit 38, Washington" },
            { "Deception Crags", "Exit 38, Washington" },
            { "no-rang-na-rang", "South Korea" }, //International
            { "Sweet Dreams", "Blue Mountains, Australia"}, //Australia route (special case for Australia)
            { "Grab Your Balls", "Breakneck, Pennsylvania" }, //https://github.com/derekantrican/MountainProject/issues/12
            { "Highball aka Pocket Problem", "Grand Ledge, Michigan" } //Parent is 1 less than state (ie Michigan -> Grand Ledge -> Pocket Problem)
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

                Assert.AreEqual(expectedLocation, resultLocation, "Failed for " + testCriteria_location[i, 0]);
                Assert.IsTrue(searchResult.TimeTaken.TotalSeconds < 5, $"{query} took too long ({searchResult.TimeTaken.Milliseconds} ms)");
            }
        }

        string[,] testCriteria_keyword = new string[,]
        {
            { "!MountainProject deception crags", "/area/105791955/exit-38-deception-crags" },
            { "!MountainProject derekantrican", "I could not find anything" }, //No results
            { "This is a test !MountainProject red river gorge", "/area/105841134/red-river-gorge" }, //Keyword not at beginning
            { "!MountainProject Earth Wind & Fire", "/route/106293533/earth-wind-fire" } //Ampersand
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

                Assert.IsTrue(resultReply.Contains(expectedUrl), "Failed for " + testCriteria_keyword[i, 0]);
            }
        }
    }
}
