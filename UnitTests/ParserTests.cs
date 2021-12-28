using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectAPI;
using static MountainProjectAPI.Grade;
using static MountainProjectAPI.Route;

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
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            Area testSubDestArea = new Area() { URL = url };

            Parsers.ParseAreaAsync(testSubDestArea, false).Wait();

            int subsubareasCount = testSubDestArea.SubAreas == null ? 0 : testSubDestArea.SubAreas.Count;
            Assert.AreEqual(expectedSubAreas, subsubareasCount);
        }

        [DataTestMethod]
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", 0)] //Subdest areas don't have routes
        [DataRow("/area/108184422/deception-wall", 20)] //Walls have routes
        public void TestAreaRouteParse(string url, int expectedRoutes)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            Area testSubDestArea = new Area() { URL = url };

            Parsers.ParseAreaAsync(testSubDestArea, false).Wait();

            int routesCount = testSubDestArea.Routes == null ? 0 : testSubDestArea.Routes.Count;
            Assert.AreEqual(expectedRoutes, routesCount);
        }

        [DataTestMethod]
        [DataRow("/area/105841134/red-river-gorge", new object[] { new[] { "/route/105860741/roadside-attraction", "/route/106405603/crack-attack", "/route/105868000/rock-wars" } })] //Some popular routes
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", new object[] { new string[0] })] //No popular routes listed
        public void TestAreaPopularClimbsParse(string url, object[] expectedPopClimbs)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            List<string> popularRoutes = Parsers.GetPopularRouteIDs(Utilities.GetHtmlDoc(url), 3);
            for (int i = 0; i < expectedPopClimbs.Length; i++)
                expectedPopClimbs[i] = Utilities.GetID(Utilities.MPBASEURL + (string)expectedPopClimbs[i]);

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
        public void TestNameParse(string url, string expectedName)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            string name;
            if (url.Contains("route"))
                name = Parsers.ParseNameFromHeader(Utilities.GetHtmlDoc(url));
            else
                name = Utilities.CleanExtraPartsFromName(Parsers.ParseAreaNameFromSidebar(Utilities.GetHtmlDoc(url)));

            Assert.AreEqual(expectedName, name);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", new[] { RouteType.Sport })]
        [DataRow("/route/109063052/geflugelfrikadelle", new[] { RouteType.Trad, RouteType.Aid })]
        [DataRow("/route/116650912/dark-crystal", new[] { RouteType.Trad, RouteType.Mixed, RouteType.Ice })] //Many Types
        [DataRow("/route/110425910/birds-of-a-feather", new[] { RouteType.Sport, RouteType.TopRope })] //Top Rope
        public void TestRouteTypeParse(string url, RouteType[] expectedTypes)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            List<RouteType> routeTypes = Parsers.ParseRouteTypes(Utilities.GetHtmlDoc(url));

            CollectionAssert.AreEquivalent(expectedTypes, routeTypes); //Compare collections WITHOUT order
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", GradeSystem.YDS, "5.10c")]
        [DataRow("/route/109063052/geflugelfrikadelle", GradeSystem.YDS, "5.12b/c")] //Has a slash
        [DataRow("/route/105890633/black-dike", GradeSystem.Ice, "WI4-5 M3")] //No YDS/Heuco present
        [DataRow("/route/105931000/myan-problem", GradeSystem.Hueco, "V-easy")] //Not a usual grade format
        [DataRow("/route/106238998/price-glacier", GradeSystem.YDS, "Easy 5th")] //Not a usual grade format
        [DataRow("/route/108170851/new-dawn", GradeSystem.Aid, "A3")] //Includes "Aid rating"
        public void TestRouteGradeParse(string url, GradeSystem expectedGradeSystem, string expectedValue)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            List<Grade> parsedGrades = Parsers.ParseRouteGrades(Utilities.GetHtmlDoc(url));
            Grade gradeMatchingExpected = parsedGrades.Find(p => p.System == expectedGradeSystem && p.Value == expectedValue);

            Assert.IsNotNull(gradeMatchingExpected);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", 1.9)] //Decimal
        [DataRow("/route/109063052/geflugelfrikadelle", 4)] //No decimal
        public void TestRouteRatingParse(string url, double expectedRating)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

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
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            string additionalInfo = Parsers.ParseAdditionalRouteInfo(Utilities.GetHtmlDoc(url));
            Dimension height = Parsers.ParseRouteHeight(ref additionalInfo);

            Assert.AreEqual(expectedAdditionalInfo, additionalInfo);

            if (!expectedHeightInFeet.HasValue)
                Assert.IsNull(height);
            else
                Assert.AreEqual(expectedHeightInFeet.Value, height.GetValue(Dimension.Units.Feet));
        }

        [DataTestMethod] //https://stackoverflow.com/a/54296734/2246411
        [DataRow("/area/105791955/deception-crags", new object[] { new[] { "/area/105708966/washington", "/area/108471374/central-west-cascades-seattle", "/area/108471684/north-bend-vicinity", "/area/114278624/exit-38" } })] //Area
        [DataRow("/route/112177605/no-rang-na-rang", new object[] { new[] { "/area/105907743/international", "/area/106661515/asia", "/area/106225629/south-korea", "/area/119456750/seoulgyeonggi-do-northwest-korea", "/area/120088159/gwanaksan-anyangsouth-seoul", "/area/112177596/jah-un-crag" } })] //Route
        public void TestParentParse(string url, object[] expectedParentUrls)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            List<string> parents = Parsers.GetParentIDs(Utilities.GetHtmlDoc(url));
            for (int i = 0; i < expectedParentUrls.Length; i++)
                expectedParentUrls[i] = Utilities.GetID(Utilities.MPBASEURL + (string)expectedParentUrls[i]);

            CollectionAssert.AreEqual(expectedParentUrls, parents); //Compare collections WITH order
        }

        [DataTestMethod]
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", "TR (2), Trad (1)")]
        [DataRow("/area/108184422/deception-wall", "TR (1), Sport (20)")]
        [DataRow("/route/111859673/side-dish", "")] //Statistics aren't generated for routes
        public void TestStatisticsParse(string url, string expectedStatistics)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

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
    }
}