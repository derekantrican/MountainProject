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
                //"Mississippi", //Listed under "In Progress" on MountainProject
                "Missouri",
                "Montana",
                //"Nebraska",    //Listed under "In Progress" on MountainProject
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
        [DataRow("/area/108184422/deception-wall", 18)] //Walls have routes
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

            List<string> popularRoutes = Parsers.GetPopularRouteUrls(Utilities.GetHtmlDoc(url), 3);
            for (int i = 0; i < popularRoutes.Count; i++)
                popularRoutes[i] = popularRoutes[i].Replace(Utilities.MPBASEURL, "");

            CollectionAssert.AreEqual(expectedPopClimbs, popularRoutes);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", "Side Dish")]
        [DataRow("/route/109063052/geflugelfrikadelle", "Geflügelfrikadelle")] //Special characters
        [DataRow("/route/112177605/no-rang-na-rang", "너랑나랑 (no-rang-na-rang)")] //Special characters
        [DataRow("/area/115968522/huu-lung", "Hữu Lũng Rock Climbing")] //Special characters
        public void TestNameParse(string url, string expectedName)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            string name = Parsers.ParseName(Utilities.GetHtmlDoc(url));

            Assert.AreEqual(expectedName, name);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", new[] { RouteType.Sport })]
        [DataRow("/route/109063052/geflugelfrikadelle", new[] { RouteType.Trad, RouteType.Aid })]
        [DataRow("/route/116181996/13-above-the-night", new[] { RouteType.Trad, RouteType.Mixed, RouteType.Ice, RouteType.Alpine })] //Many Types
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
        [DataRow("/route/116181996/13-above-the-night", GradeSystem.Unlabled, "WI4 M5")] //No YDS/Heuco present
        [DataRow("/route/105931000/myan-problem", GradeSystem.Hueco, "V-easy")] //Not a usual grade format
        public void TestRouteGradeParse(string url, GradeSystem gradeSystem, string expectedValue)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            Grade expectedGrade = new Grade(gradeSystem, expectedValue, false);
            Grade routeGrade = Parsers.ParseRouteGrades(Utilities.GetHtmlDoc(url)).Find(p => p.System == gradeSystem);

            Assert.AreEqual(expectedGrade, routeGrade);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", 1.8)] //Decimal
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
        [DataRow("/route/116181996/13-above-the-night", "5 pitches, Grade IV", 1000.0)] //Lots of additional info
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
        [DataRow("/area/105791955/exit-38-deception-crags", new object[] { new[] { "/route-guide", "/area/105708966/washington", "/area/108471374/central-west-cascades-seattle", "/area/108471684/north-bend-vicinity", "/area/114278624/exit-38" } })] //Area
        [DataRow("/route/112177605/no-rang-na-rang", new object[] { new[] { "/route-guide", "/area/105907743/international", "/area/106661515/asia", "/area/106225629/south-korea", "/area/112177596/gwanaksankwanaksan-cha-oon-am-san-sleeping-rock-mountain" } })] //Route
        public void TestParentParse(string url, object[] expectedParentUrls)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            List<string> parents = Parsers.GetParentUrls(Utilities.GetHtmlDoc(url));
            for (int i = 0; i < parents.Count; i++)
                parents[i] = parents[i].Replace(Utilities.MPBASEURL, "");

            CollectionAssert.AreEqual(expectedParentUrls, parents); //Compare collections WITH order
        }

        [DataTestMethod]
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", "TR (2), Trad (1)")]
        [DataRow("/area/108184422/deception-wall", "TR (1), Sport (18)")]
        [DataRow("/route/111859673/side-dish", "")] //Statistics aren't generated for routes
        public void TestStatisticsParse(string url, string expectedStatistics)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            AreaStats testStats = Parsers.PopulateStatistics(Utilities.GetHtmlDoc(url));

            Assert.AreEqual(expectedStatistics, testStats.ToString());
        }
    }
}
