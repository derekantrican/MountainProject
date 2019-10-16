using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectAPI;
using MountainProjectBot;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using static MountainProjectAPI.Grade;

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
            { "Moonstone, Arizona", "/route/105960200/moonstone" }, //Location parse (via comma)
            { "West Ridge of Prusik Peak", "/route/105808527/west-ridge" }, //Location parse (via word)
            { "Send me on my Way at Red River Gorge", "/route/106085043/send-me-on-my-way" }, //Location parse (via word WITH multiple location words: on, at)
            { "Edge of Time", "/route/105756826/edge-of-time" }, //Location parse possibly matches "Edge of Space" in a place that contains "CochiTI MEsa" (TIME)
            { "Send me on my Way -location:Exit 38", null }, //Wrong location
            { "Send me on my Way at derekantrican", null }, //Non-existent location
            { "Lifeline, Portland", "/route/113696621/lifeline" }, //Location that will likely match something else first ("Portland" should be UK but more likely matches Oregon)
            { "Sin Gaz", "/route/108244424/sin-gaz" }, //The text "singaz" is contained by a higher popularity route. This is more a test for "DetermineBestMatch"
            { "East Ridge", "/route/105848762/east-ridge" }, //"East Ridge" is contained in the text "Northeast Ridges and Valleys"
            { "East Ridge, Mt Temple", "/route/106997654/east-ridge" }, //Todo: in the future support "Mt" vs "Mount"
            { "East Face of Pingora", "/route/105827735/east-face-left-side-cracks" } //Location also has a route called "Northeast face" with a higher priority
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
                {
                    Assert.AreEqual(Utilities.GetSimpleURL(Utilities.MPBASEURL + expectedUrl), 
                                    searchResult.FilteredResult.URL, "Failed for " + testCriteria_search[i, 0]);
                }

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
            { "Highball aka Pocket Problem", "Grand Ledge, Michigan" }, //Parent is 1 less than state (ie Michigan -> Grand Ledge -> Pocket Problem)
            { "Chúc sức khoẻ", "Hữu Lũng, Vietnam" } //Special characters
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

                Assert.IsTrue(resultReply.Contains(Utilities.GetSimpleURL(Utilities.MPBASEURL + expectedUrl)), "Failed for " + testCriteria_keyword[i, 0]);
            }
        }

        [DataTestMethod]
        [DataRow("u/ReeseSeePoo: Psyched and thankful atop The Drifter boulder via High Plains Drifter (V7).", GradeSystem.Hueco, "V7")]
        [DataRow("Public Hanging 5.11c/d Sport, Holcomb Valley Pinnacles - Big Bear Lake, CA", GradeSystem.YDS, "5.11c-d")]
        [DataRow("Hanging out at the second belay station of my first multipitch climb! The Trough 5.4 6 pitches, Taquitz Rock, Idyllwild, CA", GradeSystem.YDS, "5.4")]
        [DataRow("Battling up a Squamish offwidth - Split Beaver 10b", GradeSystem.YDS, "5.10b")]
        [DataRow("Checkerboard V7/8, Buttermilks - Near Bishop, CA", GradeSystem.Hueco, "V7-V8")]
        [DataRow("5.11a", GradeSystem.YDS, "5.11a")]
        [DataRow("5.11-", GradeSystem.YDS, "5.11-")]
        [DataRow("5.11+", GradeSystem.YDS, "5.11+")]
        [DataRow("5.11-/+", GradeSystem.YDS, "5.11-/+")]
        [DataRow(@"5.11-\+", GradeSystem.YDS, "5.11-/+")]
        [DataRow("5.11+/-", GradeSystem.YDS, "5.11-/+")]
        [DataRow(@"5.11+\-", GradeSystem.YDS, "5.11-/+")]
        [DataRow("10b", GradeSystem.YDS, "5.10b")]
        [DataRow("10B", GradeSystem.YDS, "5.10b")]
        [DataRow("11c-d", GradeSystem.YDS, "5.11c-d")]
        [DataRow("11C-D", GradeSystem.YDS, "5.11c-d")]
        [DataRow("11c/d", GradeSystem.YDS, "5.11c-d")]
        [DataRow(@"11c\d", GradeSystem.YDS, "5.11c-d")]
        [DataRow("5.10-5.11", GradeSystem.YDS, "5.10-5.11")]
        [DataRow("5.10/5.11", GradeSystem.YDS, "5.10-5.11")]
        [DataRow(@"5.10\5.11", GradeSystem.YDS, "5.10-5.11")]
        [DataRow("5.10a-5.10c", GradeSystem.YDS, "5.10a-c")]
        [DataRow("V1", GradeSystem.Hueco, "V1")]
        [DataRow("v1", GradeSystem.Hueco, "V1")]
        [DataRow("V1-2", GradeSystem.Hueco, "V1-V2")]
        [DataRow("V1/2", GradeSystem.Hueco, "V1-V2")]
        [DataRow(@"V1\2", GradeSystem.Hueco, "V1-V2")]
        [DataRow("V2+", GradeSystem.Hueco, "V2+")]
        [DataRow("V3-", GradeSystem.Hueco, "V3-")]
        [DataRow("V2-V3", GradeSystem.Hueco, "V2-V3")]
        [DataRow("V2-/+", GradeSystem.Hueco, "V2-/+")]
        [DataRow(@"V2-\+", GradeSystem.Hueco, "V2-/+")]
        [DataRow("V2+/-", GradeSystem.Hueco, "V2-/+")]
        [DataRow(@"V2+\-", GradeSystem.Hueco, "V2-/+")]
        public void TestRouteGradeParse(string inputGrade, GradeSystem expectedSystem, string expectedValue)
        {
            Grade expectedGrade = new Grade(expectedSystem, expectedValue);
            Grade parsedGrade = Grade.ParseString(inputGrade)[0]; //Todo: expand test for multiple grades found

            Assert.AreEqual(expectedSystem, parsedGrade.System);
            Assert.AreEqual(expectedValue, parsedGrade.ToString(false));
        }

        object[,] testCriteria_gradeEquality = new object[,]
        {
            {"/route/106832048/swing-dance", GradeSystem.Hueco, "v7" }, //v7 should equal V7
            {"/route/106832048/swing-dance", GradeSystem.Hueco, "V7"}, //V7 should equal V7
            {"/route/106832048/swing-dance", GradeSystem.Hueco, "V6-8"}, //V6-8 should equal V7
            {"/route/106832048/swing-dance", GradeSystem.Hueco, "V6/8"}, //V6/8 should equal V7
            {"/route/106832048/swing-dance", GradeSystem.Hueco, @"V6\8"}, //V6\8 should equal V7
            {"/route/107371613/pookie", GradeSystem.Hueco, @"V7"}, //V7 should equal V7-8
            {"/route/107362365/chevy", GradeSystem.Hueco, @"V7"}, //V7 should equal V7+
            {"/route/106129151/checkerboard", GradeSystem.Hueco, "V7/8"}, //V7/8 should equal V7-8
            {"/route/106129151/checkerboard", GradeSystem.Hueco, @"V7\8"}, //V7\8 should equal V7-8
            {"/route/106129151/checkerboard", GradeSystem.Hueco, "V7-8"}, //V7-8 should equal V7-8
            {"/route/105734687/the-great-roof", GradeSystem.YDS, "5.10b"}, //5.10b should equal 5.10b
            {"/route/105734687/the-great-roof", GradeSystem.YDS, "5.10"}, //5.10 should equal 5.10b
            {"/route/105988502/pumpkin-patches", GradeSystem.YDS, "5.9"}, //5.9 should equal 5.9+
            {"/route/106048187/the-flake", GradeSystem.YDS, "5.9"}, //5.9 should equal 5.9-
            {"/route/105734687/the-great-roof", GradeSystem.YDS, "5.10a-c"}, //5.10a-c should equal 5.10b
            {"/route/105734687/the-great-roof", GradeSystem.YDS, "5.10a/c"}, //5.10a/c should equal 5.10b
            {"/route/105734687/the-great-roof", GradeSystem.YDS, @"5.10a\c"}, //5.10a\c should equal 5.10b
            {"/route/105965166/public-hanging", GradeSystem.YDS, "5.11c/d"}, //5.11c/d should equal 5.11c/d
            {"/route/105965166/public-hanging", GradeSystem.YDS, @"5.11c\d"}, //5.11c\d should equal 5.11c/d
            {"/route/105965166/public-hanging", GradeSystem.YDS, "5.11c-d"} //5.11c-d should equal 5.11c/d
        };

        [TestMethod]
        public void TestGradeEquality()
        {
            MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");

            for (int i = 0; i < testCriteria_gradeEquality.GetLength(0); i++)
            {
                string inputUrl = testCriteria_gradeEquality[i, 0].ToString();
                GradeSystem gradeSystem = (GradeSystem)testCriteria_gradeEquality[i, 1];
                string inputGrade = testCriteria_gradeEquality[i, 2].ToString();
                Grade expectedGrade = Grade.ParseString(inputGrade)[0];

                Route route = MountainProjectDataSearch.GetItemWithMatchingID(Utilities.GetID(Utilities.MPBASEURL + inputUrl)) as Route;

                Assert.IsTrue(route.Grades.Any(g => expectedGrade.Equals(g, true, true)));
            }
        }

        [TestMethod]
        public void TestPostTitleParse()
        {
            int linkPasses = 0; //Passes where a MP link was expected
            int totalPasses = 0;
            int totalFailures = 0;

            StringWriter writer = new StringWriter();
            Console.SetOut(writer);

            MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");
            string[] testCriteria = File.ReadAllLines(@"..\PostTitleTest.txt");

            //Parallel.For(0, testCriteria.Length, i =>
            for (int i = 0; i < testCriteria.Length; i++)
            {
                string[] lineParts = testCriteria[i].Split('\t');
                string inputPostTitle = lineParts[0];
                string expectedMPLink = lineParts[1] == "null" ? null : Utilities.GetSimpleURL(lineParts[1]);

                writer.WriteLine($"POST TITLE: {inputPostTitle}");

                Route result = BotReply.ParsePostTitleToRoute(WebUtility.HtmlDecode(inputPostTitle));

                if (expectedMPLink == null)
                {
                    if (result == null)
                    {
                        writer.WriteLine("PASS");
                        totalPasses++;
                    }
                    else
                    {
                        writer.WriteLine($"FAILED FOR: {inputPostTitle}");
                        writer.WriteLine($"EXPECTED: {expectedMPLink}, ACTUAL: {result?.URL}");
                        totalFailures++;
                    }
                }
                else
                {
                    if (result == null || result.URL != expectedMPLink)
                    {
                        writer.WriteLine($"FAILED FOR: {inputPostTitle}");
                        writer.WriteLine($"EXPECTED: {expectedMPLink}, ACTUAL: {result?.URL}");
                        totalFailures++;
                    }
                    else
                    {
                        writer.WriteLine("PASS");
                        linkPasses++;
                        totalPasses++;
                    }
                }
            }/*);*/

            System.Diagnostics.Debug.WriteLine($"Passes: {totalPasses}, Failures: {totalFailures}, Pass percentage: {Math.Round((double)totalPasses / (totalPasses + totalFailures) * 100, 2)}%\n");
            System.Diagnostics.Debug.WriteLine(writer.ToString());

            writer.Dispose();
            Assert.IsTrue(totalFailures <= 48);
        }
    }
}
