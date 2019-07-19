using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectAPI;
using MountainProjectBot;
using System.Collections.Generic;

namespace UnitTests
{
    [TestClass]
    public class SearchTests
    {
        string[,] testCriteria_search = new string[,]
        {
            { "Red River Gorge", "/area/105841134/red-river-gorge" },
            { "Prime Rib of Goat", "/route/107730934/prime-rib-of-goat" },
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
                MPObject result = MountainProjectDataSearch.FilterByPopularity(MountainProjectDataSearch.SearchMountainProject(query));

                Assert.AreEqual(Utilities.MPBASEURL + expectedUrl, result.URL);
            }
        }

        string[,] testCriteria_location = new string[,]
        {
            { "Red River Gorge", "Kentucky" },
            { "Prime Rib of Goat", "Mazama, Washington" },
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
                string resultLocation = BotReply.GetLocationString(MountainProjectDataSearch.FilterByPopularity(MountainProjectDataSearch.SearchMountainProject(query)));
                resultLocation = resultLocation.Replace("\n\n", ""); //Remove markdown newline
                resultLocation = resultLocation.Replace("Located in ", ""); //Simplify results for unit test

                Assert.AreEqual(expectedLocation, resultLocation);
            }
        }

        string[,] testCriteria_keyword = new string[,]
        {
            { "!MountainProject deception crags", "/area/105791955/exit-38-deception-crags" },
            { "This is a test !MountainProject red river gorge", "/area/105841134/red-river-gorge" } //Keyword not at beginning
        };

        [TestMethod]
        public void TestCommentBodyParse()
        {
            MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");

            for (int i = 0; i < testCriteria_keyword.GetLength(0); i++)
            {
                string commentBody = testCriteria_keyword[i, 0];
                string expectedUrl = testCriteria_keyword[i, 1];
                string resultReply = BotReply.GetReplyForCommentBody(commentBody);

                Assert.IsTrue(resultReply.Contains(expectedUrl));
            }
        }
    }
}
