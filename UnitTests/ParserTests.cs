﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectAPI;
using static MountainProjectAPI.Grade;
using static MountainProjectAPI.Route;
using Url = MountainProjectAPI.Url;

namespace UnitTests
{
    [TestClass]
    public class ParserTests
    {
        /* ===========================================================================
         *              NOTE ABOUT FAILED TESTS:
         * - If a test fails (particularly the statistics test), first make sure it 
         *   is not due to updates on the MountainProject website
         * ===========================================================================
         */
        [TestMethod]
        public void TestGetDestAreas()
        {
            List<string> expectedDestAreas = new List<string>()
            {
                "Alabama",
                "Alaska",
                "Arizona",
                "Arkansas",
                "California",
                "Colorado",
                "Connecticut",
                "Delaware",
                "Florida",
                "Georgia",
                "Hawaii",
                "Idaho",
                "Illinois",
                "Indiana",
                "Iowa",
                "Kansas",
                "Kentucky",
                "Louisiana",
                "Maine",
                "Maryland",
                "Massachusetts",
                "Michigan",
                "Minnesota",
                "Mississippi",
                "Missouri",
                "Montana",
                "Nebraska",
                "Nevada",
                "New Hampshire",
                "New Jersey",
                "New Mexico",
                "New York",
                "North Carolina",
                "North Dakota",
                "Ohio",
                "Oklahoma",
                "Oregon",
                "Pennsylvania",
                "Rhode Island",
                "South Carolina",
                "South Dakota",
                "Tennessee",
                "Texas",
                "Utah",
                "Vermont",
                "Virginia",
                "Washington",
                "West Virginia",
                "Wisconsin",
                "Wyoming",
                "International"
            };

            List<Area> destAreas = Parsers.GetDestAreas();
            destAreas.ForEach(a => a.Name = Utilities.CleanExtraPartsFromName(Parsers.ParseAreaNameFromSidebar(Utilities.GetHtmlDoc(a.URL))));
            List<string> resultNames = destAreas.Select(p => p.Name).ToList();

            CollectionAssert.AreEqual(expectedDestAreas, resultNames); //Compare collections WITH order
        }

        [DataTestMethod]
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", 2)] //Subdest areas have subareas
        [DataRow("/area/108184422/deception-wall", 0)] //Walls don't have subareas
        public void TestAreaSubAreaParse(string url, int expectedSubAreas)
        {
            if (!Url.Contains(url, Utilities.MPBASEURL))
                url = Url.BuildFullUrl(Utilities.MPBASEURL + url);

            Area testSubDestArea = new Area() { URL = url };

            Parsers.ParseAreaAsync(testSubDestArea, false).Wait();

            int subsubareasCount = testSubDestArea.SubAreas == null ? 0 : testSubDestArea.SubAreas.Count;
            Assert.AreEqual(expectedSubAreas, subsubareasCount);
        }

        [DataTestMethod]
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", 0)] //Subdest areas don't have routes
        [DataRow("/area/108184422/deception-wall", 21)] //Walls have routes
        public void TestAreaRouteParse(string url, int expectedRoutes)
        {
            if (!Url.Contains(url, Utilities.MPBASEURL))
                url = Url.BuildFullUrl(Utilities.MPBASEURL + url);

            Area testSubDestArea = new Area() { URL = url };

            Parsers.ParseAreaAsync(testSubDestArea, false).Wait();

            int routesCount = testSubDestArea.Routes == null ? 0 : testSubDestArea.Routes.Count;
            Assert.AreEqual(expectedRoutes, routesCount);
        }

        [DataTestMethod]
        [DataRow("/area/105841134/red-river-gorge", [new[] { "/route/105880926/eureka", "/route/106125099/27-years-of-climbing", "/route/105868000/rock-wars" }])] //Some popular routes
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", [new string[0]])] //No popular routes listed
        public void TestAreaPopularClimbsParse(string url, object[] expectedPopClimbs)
        {
            if (!Url.Contains(url, Utilities.MPBASEURL))
                url = Url.BuildFullUrl(Utilities.MPBASEURL + url);

            List<string> popularRoutes = Parsers.GetPopularRouteIDs(Utilities.GetHtmlDoc(url), 3);
            for (int i = 0; i < expectedPopClimbs.Length; i++)
                expectedPopClimbs[i] = Utilities.GetID((string)expectedPopClimbs[i]);

            CollectionAssert.AreEqual(expectedPopClimbs, popularRoutes);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", "Side Dish")]
        [DataRow("/route/109063052/geflugelfrikadelle", "Geflügelfrikadelle")] //Special characters
        [DataRow("/route/112177605/no-rang-na-rang", "너랑나랑 (no-rang-na-rang)")] //Special characters
        [DataRow("/area/115968522/huu-lung", "Hữu Lũng")] //Special characters
        [DataRow("/area/107448852/phoenix-areas", "Phoenix")] //Remove "Area" and "*" from title
        [DataRow("/area/108276053/area-51-boulder-area", "Area 51 Boulder")] //Remove only end "Area" from title
        [DataRow("/area/106558306/drop-area-horseshoe-area", "Drop (Horseshoe)")] //Remove "Area" both inside and outside parenthesis
        [DataRow("/area/107373214/turtle-rock-area-corridors-area", "Turtle Rock/ Corridors")] //Remove "Area" both before and after slash
        [DataRow("/route/107889066/redacted", "Bro's Before Holes")] //Parse redacted route name
        [DataRow("/route/113780190/redacted-history", "Trail of Tears")] //Parse redacted route name (but different redacted format)
        [DataRow("/area/107082717/redacted", "Nature Nazi Boulders")] //Parse redacted area name
        public void TestNameParse(string url, string expectedName)
        {
            if (!Url.Contains(url, Utilities.MPBASEURL))
                url = Url.BuildFullUrl(Utilities.MPBASEURL + url);

            string name;
            if (url.Contains("route"))
            {
                Route route = new Route { ID = Utilities.GetID(url) };
                Parsers.ParseRouteAsync(route).Wait();
                name = route.Name;
            }
            else
            {
                Area area = new Area { ID = Utilities.GetID(url) };
                Parsers.ParseAreaAsync(area, false).Wait();
                name = area.Name;
            }

            Assert.AreEqual(expectedName, name);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", new[] { RouteType.Sport })]
        [DataRow("/route/109063052/geflugelfrikadelle", new[] { RouteType.Trad, RouteType.Aid })]
        [DataRow("/route/116650912/dark-crystal", new[] { RouteType.Trad, RouteType.Mixed, RouteType.Ice })] //Many Types
        [DataRow("/route/110425917/deli-faction", new[] { RouteType.Sport, RouteType.TopRope })] //Top Rope
        public void TestRouteTypeParse(string url, RouteType[] expectedTypes)
        {
            if (!Url.Contains(url, Utilities.MPBASEURL))
                url = Url.BuildFullUrl(Utilities.MPBASEURL + url);

            List<RouteType> routeTypes = Parsers.ParseRouteTypes(Utilities.GetHtmlDoc(url));

            CollectionAssert.AreEquivalent(expectedTypes, routeTypes); //Compare collections WITHOUT order
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", GradeSystem.YDS, "5.10c")]
        [DataRow("/route/109063052/geflugelfrikadelle", GradeSystem.YDS, "5.12b/c")] //Has a slash
        [DataRow("/route/105890633/black-dike", GradeSystem.Ice, "WI4+ M3")] //No YDS/Heuco present
        [DataRow("/route/105931000/myan-problem", GradeSystem.Hueco, "V-easy")] //Not a usual grade format
        [DataRow("/route/106238998/price-glacier", GradeSystem.YDS, "Easy 5th")] //Not a usual grade format
        [DataRow("/route/108170851/new-dawn", GradeSystem.Aid, "C3+")] //Includes "Aid rating"
		//[DataRow("/route/108878033/wolfenstein", GradeSystem.YDS, "5.10")] //Todo: uncomment this one when this is resolved: https://www.mountainproject.com/forum/topic/126874784/mountainproject-lists-more-subareas-if-you-are-not-logged-in#ForumMessage-127092247
		public void TestRouteGradeParse(string url, GradeSystem expectedGradeSystem, string expectedValue)
        {
            if (!Url.Contains(url, Utilities.MPBASEURL))
                url = Url.BuildFullUrl(Utilities.MPBASEURL + url);

            List<Grade> parsedGrades = Parsers.ParseRouteGrades(Utilities.GetHtmlDoc(url));
            Grade gradeMatchingExpected = parsedGrades.Find(p => p.System == expectedGradeSystem && p.Value == expectedValue);

            Assert.IsNotNull(gradeMatchingExpected);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", 1.8)] //Decimal
        [DataRow("/route/109063052/geflugelfrikadelle", 4)] //No decimal
        public void TestRouteRatingParse(string url, double expectedRating)
        {
            if (!Url.Contains(url, Utilities.MPBASEURL))
                url = Url.BuildFullUrl(Utilities.MPBASEURL + url);

            double rating = Parsers.ParseRouteRating(Utilities.GetHtmlDoc(url));

            Assert.AreEqual(expectedRating, rating);
        }

        [DataTestMethod]
        [DataRow("/route/109063052/geflugelfrikadelle", "", 40.0)]
        [DataRow("/route/105890633/black-dike", "3 pitches, Grade IV", 500.0)] //Lots of additional info
        [DataRow("/route/110425910/birds-of-a-feather", "", 50.0)] //Had a weird (multiple comma) parsing issue before
        [DataRow("/route/107530893/a-new-beginning", "", null)] //No additional info
        public void TestRouteAdditionalInfoParse(string url, string expectedAdditionalInfo, double? expectedHeightInFeet)
        {
            if (!Url.Contains(url, Utilities.MPBASEURL))
                url = Url.BuildFullUrl(Utilities.MPBASEURL + url);

            string additionalInfo = Parsers.ParseAdditionalRouteInfo(Utilities.GetHtmlDoc(url));
            Dimension height = Parsers.ParseRouteHeight(ref additionalInfo);

            Assert.AreEqual(expectedAdditionalInfo, additionalInfo);

            if (!expectedHeightInFeet.HasValue)
                Assert.IsNull(height);
            else
                Assert.AreEqual(expectedHeightInFeet.Value, height.GetValue(Dimension.Units.Feet));
        }

        [DataTestMethod] //https://stackoverflow.com/a/54296734/2246411
        [DataRow("/area/105791955/deception-crags", [new[] { "/area/105708966/washington", "/area/108471374/central-west-cascades-seattle", "/area/108471684/north-bend-vicinity", "/area/114278624/exit-38" }])] //Area
        [DataRow("/route/112177605/no-rang-na-rang", [new[] { "/area/105907743/international", "/area/106661515/asia", "/area/106225629/south-korea", "/area/119456750/seoulgyeonggi-do-northwest-korea", "/area/120088159/gwanaksan-anyangsouth-seoul", "/area/112177596/jah-un-crag" }])] //Route
        public void TestParentParse(string url, object[] expectedParentUrls)
        {
            if (!Url.Contains(url, Utilities.MPBASEURL))
                url = Url.BuildFullUrl(Utilities.MPBASEURL + url);

            List<string> parents = Parsers.GetParentIDs(Utilities.GetHtmlDoc(url), null);
            for (int i = 0; i < expectedParentUrls.Length; i++)
                expectedParentUrls[i] = Utilities.GetID((string)expectedParentUrls[i]);

            CollectionAssert.AreEqual(expectedParentUrls, parents); //Compare collections WITH order
        }

        [DataTestMethod]
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", "TR (2), Trad (1)")]
        [DataRow("/area/108184422/deception-wall", "TR (1), Sport (21)")]
        [DataRow("/route/111859673/side-dish", "")] //Statistics aren't generated for routes
        public void TestStatisticsParse(string url, string expectedStatistics)
        {
            if (!Url.Contains(url, Utilities.MPBASEURL))
                url = Url.BuildFullUrl(Utilities.MPBASEURL + url);

            AreaStats testStats = Parsers.PopulateStatistics(Utilities.GetHtmlDoc(url));

            Assert.AreEqual(expectedStatistics, testStats.ToString());
        }

        [DataTestMethod]
        [DataRow("Sixty-Nine", "69")]
        [DataRow("Nine to Five", "9 to 5")]
        [DataRow("Dude's Five Nine", "Dude's 5 9")]
        [DataRow("Zero to Sixty-Nine", "0 to 69")]
        [DataRow("Goose Bubbs and 500 Million Years", "Goose Bubbs and 500000000 Years")]
        [DataRow("Five Hundred", "500")]
        [DataRow("Sixty Seven", "67")]
        [DataRow("Four hundred six", "406")]
        [DataRow("Four hundred, six", "400, 6")]
        [DataRow("Four hundred and six", "406")]
        [DataRow("Four hundred, and six", "400, and 6")]
        [DataRow("Six hundred, Seven hundred", "600, 700")]
        [DataRow("One thousand eighteen", "1018")]
        [DataRow("Earth Wind & Fire", "Earth Wind and Fire")]
        [DataRow("Earth, Wind and Fire Dihedral", "Earth, Wind and Fire Dihedral")]
        [DataRow("Mt. Temple", "Mt. Temple")]
        [DataRow("Mount Nemo", "Mt. Nemo")]
        [DataRow("Mr. Masters", "Mr. Masters")]
        [DataRow("Mister Masters", "Mr. Masters")]
        [DataRow("Lone Pine", "Lone Pine")] //Don't convert "Lone" into "L1"
        public void TestWordConsistency(string inputString, string expectedConversion)
        {
            string convertedString = Utilities.EnforceWordConsistency(inputString);
            Assert.AreEqual(expectedConversion, convertedString);
        }

        //[DataTestMethod] //Uncomment for manual testing
        //[DataRow(@"C:\Users\deantric\Downloads\Failing MP objects\Failing Object (116683164).html", "area")]
        //[DataRow(@"C:\Users\deantric\Downloads\Failing MP objects\Failing Object (105877457).html", "area")]
        //[DataRow(@"C:\Users\deantric\Downloads\Failing MP objects\Failing Object (114000292).html", "area")]
        //[DataRow(@"C:\Users\deantric\Downloads\Failing MP objects\Failing Object (112892737).html", "area")]
        //public void TestParseLocalHtmlFile(string filePath, string type)
        //{
        //    HtmlParser parser = new HtmlParser();
        //    using (IHtmlDocument doc = parser.ParseDocument(File.ReadAllText(filePath)))
        //    {
        //        //string htmlString = Utilities.GetHtmlDoc("https://www.mountainproject.com/area/116683164/the-library").Source.Text;

        //        if (type == "route")
        //        {
        //            Route route = new Route { ID = Regex.Match(filePath, @"\d{9}").Value };
        //            Parsers.ParseRouteAsync(doc, route, false, new Stopwatch()).Wait();
        //        }
        //        else if (type == "area")
        //        {
        //            Area area = new Area { ID = Regex.Match(filePath, @"\d{9}").Value };
        //            Parsers.ParseAreaAsync(doc, area, true, false, new Stopwatch()).Wait();
        //        }
        //        else
        //        {
        //            throw new ArgumentException($"\"{type}\" is not a valid type", nameof(type));
        //        }
        //    }
        //}
    }
}