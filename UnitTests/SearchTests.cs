﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectAPI;
using MountainProjectBot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
            { "Deception Crags", "/area/105791955/exit-38-deception-crags" },
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
            { "East Ridge, Mt Temple", "/route/106997654/east-ridge" },
            { "East Face of Pingora", "/route/105827735/east-face-left-side-cracks" }, //Location also has a route called "Northeast face" with a higher priority
            { "Five gallon buckets", "/route/105789060/5-gallon-buckets" }, //Number words/Numbers interchangable
            { "5 gallon buckets", "/route/105789060/5-gallon-buckets" }, //Number words/Numbers interchangable
            { "Earth Wind & Fire", "/route/106293533/earth-wind-fire" }, //"And"/& interchangable
            { "Earth Wind and Fire", "/route/106293533/earth-wind-fire" }, //"And"/& interchangable
            { "Mt Temple", "/area/106997567/mt-temple" }, //"Mt"/"Mount" interchangable
            { "Mount Temple", "/area/106997567/mt-temple" }, //"Mt"/"Mount" interchangable
            { "Mister Masters", "/route/105733163/mister-masters" }, //"Mr"/"Mister" interchangable
            { "Mr Masters", "/route/105733163/mister-masters" }, //"Mr"/"Mister" interchangable
            { "Landjäger", "/route/117251258/landjager" }, //Diacritical marks optional
            { "Landjager", "/route/117251258/landjager" }, //Diacritical marks optional
            { "Alexisizer", "/route/106657518/alexisizer" }, //There is an "Alexisizer" area and an "Alexisizer" route. This is to make sure the route is picked
            { "Wood, HP40", "/route/106286494/the-wood" }, //Test for nickname "HP40"
            { "Golden, RRG", "/route/108521478/golden-ticket" }, //Test for nickname "RRG"
        };

        [TestMethod]
        public void TestSearch()
        {
            InitMPData();

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
                    Assert.IsTrue(Url.Equals(Utilities.GetSimpleURL(Utilities.MPBASEURL + expectedUrl),
                                    searchResult.FilteredResult.URL), "Failed for " + testCriteria_search[i, 0]);
                }

                Assert.IsTrue(searchResult.TimeSpanTaken().TotalSeconds < 5, $"{query} took too long ({searchResult.TimeTakenMS} ms)");
            }
        }

        string[,] testCriteria_location = new string[,]
        {
            { "Red River Gorge", "Kentucky" },
            { "Deception Crags", "Exit 38, Washington" },
            { "no-rang-na-rang", "Gwanaksan (Anyang/South Seoul), South Korea" }, //International
            { "Sweet Dreams", "Blue Mountains, Australia"}, //Australia route (special case for Australia)
            { "Grab Your Balls", "Breakneck, Pennsylvania" }, //https://github.com/derekantrican/MountainProject/issues/12
            { "Sandy Cheeks", "Kistachie Boulder Longleaf Vista, Louisiana" }, //Parent is 1 less than state (ie Louisiana -> Kistachie Boulder Longleaf Vista -> Sandy Cheeks)
            { "Chúc sức khoẻ", "Hữu Lũng, Vietnam" } //Special characters
        };

        [TestMethod]
        public void TestLocationString()
        {
            InitMPData();

            for (int i = 0; i < testCriteria_location.GetLength(0); i++)
            {
                string query = testCriteria_location[i, 0];
                string expectedLocation = testCriteria_location[i, 1];
                SearchResult searchResult = MountainProjectDataSearch.Search(query);
                string resultLocation = BotReply.GetLocationString(searchResult.FilteredResult);
                resultLocation = resultLocation.Replace(Markdown.NewLine, ""); //Remove markdown newline
                resultLocation = resultLocation.Replace("Located in ", ""); //Simplify results for unit test
                resultLocation = Regex.Replace(resultLocation, @"\[|\]\(.*?\)", ""); //Remove markdown link formatting

                Assert.AreEqual(expectedLocation, resultLocation, "Failed for " + testCriteria_location[i, 0]);
                Assert.IsTrue(searchResult.TimeSpanTaken().TotalSeconds < 5, $"{query} took too long ({searchResult.TimeTakenMS} ms)");
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
            InitMPData();

            for (int i = 0; i < testCriteria_keyword.GetLength(0); i++)
            {
                string commentBody = testCriteria_keyword[i, 0];
                string expectedUrl = testCriteria_keyword[i, 1];
                string resultReply = BotReply.GetReplyForRequest(commentBody);

                Assert.IsTrue(Url.TextContains(resultReply, Utilities.GetSimpleURL(Utilities.MPBASEURL + expectedUrl)), "Failed for " + testCriteria_keyword[i, 0]);
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
        [DataRow(@"5.11c/6c+", GradeSystem.YDS, "5.11c")] //Match YDS grade out of mixed grades
        public void TestRouteGradeParse(string inputGrade, GradeSystem expectedSystem, string expectedValue)
        {
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
            {"/route/106095521/water-saps", GradeSystem.Hueco, "V7/8"}, //V7/8 should equal V7-8
            {"/route/106095521/water-saps", GradeSystem.Hueco, @"V7\8"}, //V7\8 should equal V7-8
            {"/route/106095521/water-saps", GradeSystem.Hueco, "V7-8"}, //V7-8 should equal V7-8
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
            InitMPData();

            for (int i = 0; i < testCriteria_gradeEquality.GetLength(0); i++)
            {
                string inputUrl = testCriteria_gradeEquality[i, 0].ToString();
                GradeSystem gradeSystem = (GradeSystem)testCriteria_gradeEquality[i, 1];
                string inputGrade = testCriteria_gradeEquality[i, 2].ToString();
                Grade expectedGrade = Grade.ParseString(inputGrade)[0];

                Route route = MountainProjectDataSearch.GetItemWithMatchingID(Utilities.GetID(inputUrl)) as Route;

                Assert.IsTrue(route.Grades.Any(g => expectedGrade.Equals(g, true, true)));
            }
        }

        //[DataTestMethod]
        //[DataRow(false, false)] //This would be preferred but causes duplicate runs: https://github.com/microsoft/testfx/issues/488
        [TestMethod]
        public void TestPostTitleParse(/*bool outputExtraInfo, bool isGoogleSheetsTest*/)
        {
            bool outputExtraInfo = false;
            bool isGoogleSheetsTest = false;

            Stopwatch totalStopwatch = Stopwatch.StartNew();

            int totalPasses = 0;
            int totalFailures = 0;
            int yessesWithConfidence1 = 0;
            List<string> failingPostsWithConfidence1 = new List<string>();

            StringWriter writer = new StringWriter();
            Console.SetOut(writer);
            MountainProjectDataSearch.OutputExtraInfo = outputExtraInfo;

            InitMPData();
            string[] testCriteria;
            if (isGoogleSheetsTest)
            {
                string requestUrl = BotUtilities.GetCredentialValue(@"..\..\MountainProjectBot\Credentials.txt", "spreadsheetURL") + "PostHistory";
                testCriteria = Utilities.GetHtml(requestUrl).Split('\n');
            }
            else
                testCriteria = File.ReadAllLines(@"..\PostTitleTest.txt");

            for (int i = 0; i < testCriteria.Length; i++)
            {
                Stopwatch routeStopwatch = Stopwatch.StartNew();

                string[] lineParts = testCriteria[i].Split('\t');

                string inputPostTitle, expectedMPLink, comment;
                if (isGoogleSheetsTest)
                {
                    inputPostTitle = lineParts[2];
                    expectedMPLink = lineParts[9].ToUpper() == "YES" ? Utilities.GetSimpleURL(lineParts[7]) : !string.IsNullOrEmpty(lineParts[10]) ? Url.BuildFullUrl($"{Utilities.MPROUTEURL}/{lineParts[10]}") : null;
                    comment = lineParts.Length > 11 && !string.IsNullOrEmpty(lineParts[11]) ? $"//{lineParts[11]}" : null;
                }
                else
                {
                    inputPostTitle = lineParts[0];
                    expectedMPLink = lineParts[1] == "null" ? null : Utilities.GetSimpleURL(lineParts[1]);
                    comment = lineParts.Length > 2 && !string.IsNullOrEmpty(lineParts[2]) ? $"//{lineParts[2]}" : null;
                }

                //Override input title (uncomment only for debugging)
                //inputPostTitle = "Happy Star Wars Day! Had to practice my mental game for this one: \"Jedi Mind Tricks\" (v4). One of the Best.";

                writer.WriteLine($"POST TITLE: {inputPostTitle}");

                SearchResult result = MountainProjectDataSearch.ParseRouteFromString(WebUtility.HtmlDecode(inputPostTitle));
                Route route = result.FilteredResult as Route;

                if (expectedMPLink == null)
                {
                    if (route == null)
                    {
                        writer.WriteLine($"PASS (Correctly guessed no match)");
                        totalPasses++;
                    }
                    else
                    {
                        writer.WriteLine($"FAILED (confidence {result.Confidence}). EXPECTED: , ACTUAL: {route?.URL} {comment}");
                        totalFailures++;

                        if (result.Confidence == 1)
                            failingPostsWithConfidence1.Add($"{inputPostTitle} {comment}");
                    }
                }
                else
                {
                    if (route == null || !Url.Equals(route.URL, expectedMPLink))
                    {
                        writer.WriteLine($"FAILED (confidence {result.Confidence}). EXPECTED: {expectedMPLink} , ACTUAL: {route?.URL} {comment}");
                        totalFailures++;

                        if (result.Confidence == 1)
                            failingPostsWithConfidence1.Add($"{inputPostTitle} {comment}");
                    }
                    else
                    {
                        writer.WriteLine($"PASS (confidence: {result.Confidence})");
                        totalPasses++;

                        if (isGoogleSheetsTest && result.Confidence == 1)
                            yessesWithConfidence1++;
                    }
                }

                if (outputExtraInfo)
                    writer.WriteLine($"Time taken: {routeStopwatch.Elapsed}");
            }

            if (outputExtraInfo)
                Debug.WriteLine($"Test completed in {totalStopwatch.Elapsed}\n");

            if (!isGoogleSheetsTest)
            {
                Debug.WriteLine($"Passes: {totalPasses}, Failures: {totalFailures}, Pass percentage: {Math.Round((double)totalPasses / (totalPasses + totalFailures) * 100, 2)}%\n");
            }
            else
            {
                Debug.WriteLine($"Yesses that now have confidence 1: {yessesWithConfidence1} (out of {testCriteria.Count(p => p.Split('\t')[9].ToUpper() == "YES")} total yesses)\n");
                Debug.WriteLine($"Passes: {totalPasses}, Failures: {totalFailures}\n");
            }

            if (failingPostsWithConfidence1.Any())
                Debug.WriteLine($"Failing posts with confidence 1 ({failingPostsWithConfidence1.Count()}):\n\n{string.Join("\n", failingPostsWithConfidence1)}\n");

            Debug.WriteLine(writer.ToString());
            writer.Dispose();

            Assert.IsTrue(failingPostsWithConfidence1.Count == 0, "Some failed matches have a confidence of 1");

            if (!isGoogleSheetsTest) //Todo: may want to rework how the spreadsheet is setup so that this line is also relevant for GoogleSheetsTest
                Assert.IsTrue((double)totalPasses / (totalPasses + totalFailures) > 0.95);
        }

        private void InitMPData()
        {
            //The MountainProject data will actually persist between unit tests if multiple unit tests are run at once. Doing it this way
            //ensures that the delay time to init the MountainProject data is only part of the first test
            if (MountainProjectDataSearch.DestAreas.Count == 0)
                MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");
        }
    }
}
