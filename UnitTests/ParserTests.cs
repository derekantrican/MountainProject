using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectAPI;

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
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", 2, 0)] //Subdest areas have subareas but no routes
        [DataRow("/area/108184422/deception-wall", 0, 17)] //Walls have routes but no subareas
        public void TestSubAreaParse(string url, int expectedSubAreas, int expectedRoutes)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            Area testSubDestArea = new Area() { URL = url };

            Parsers.ParseAreaAsync(testSubDestArea, false).Wait();

            int subsubareasCount = testSubDestArea.SubAreas == null ? 0 : testSubDestArea.SubAreas.Count;
            Assert.AreEqual(expectedSubAreas, subsubareasCount);

            int routesCount = testSubDestArea.Routes == null ? 0 : testSubDestArea.Routes.Count;
            Assert.AreEqual(expectedRoutes, routesCount);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", "Side Dish")]
        [DataRow("/route/109063052/geflugelfrikadelle", "Geflügelfrikadelle")] //Special characters
        [DataRow("/route/112177605/no-rang-na-rang", "너랑나랑 (no-rang-na-rang)")] //Special characters
        public void TestRouteNameParse(string url, string expectedName)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            Route testRoute = new Route() { URL = url };
            Parsers.ParseRouteAsync(testRoute).Wait();

            Assert.AreEqual(expectedName, testRoute.Name);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", new[] { Route.RouteType.Sport })]
        [DataRow("/route/109063052/geflugelfrikadelle", new[] { Route.RouteType.Trad, Route.RouteType.Aid })]
        [DataRow("/route/116181996/13-above-the-night", new[] { Route.RouteType.Trad, Route.RouteType.Mixed, Route.RouteType.Ice, Route.RouteType.Alpine })] //Many Types
        [DataRow("/route/110425910/birds-of-a-feather", new[] { Route.RouteType.Sport, Route.RouteType.TopRope })] //Top Rope
        public void TestRouteTypeParse(string url, Route.RouteType[] expectedTypes)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            Route testRoute = new Route() { URL = url };
            Parsers.ParseRouteAsync(testRoute).Wait();

            CollectionAssert.AreEquivalent(expectedTypes, testRoute.Types); //Compare collections WITHOUT order
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", "5.10c")]
        [DataRow("/route/109063052/geflugelfrikadelle", "5.12b/c")] //Has a slash
        [DataRow("/route/116181996/13-above-the-night", "WI4 M5")] //No YDS/Heuco present
        public void TestRouteGradeParse(string url, string expectedGrade)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            Route testRoute = new Route() { URL = url };
            Parsers.ParseRouteAsync(testRoute).Wait();

            Assert.AreEqual(expectedGrade, testRoute.Grade);
        }

        [DataTestMethod]
        [DataRow("/route/109063052/geflugelfrikadelle", "40 ft")]
        [DataRow("/route/116181996/13-above-the-night", "1000 ft, 5 pitches, Grade IV")] //Lots of additional info
        [DataRow("/route/110425910/birds-of-a-feather", "50 ft")] //Had a weird (multiple comma) parsing issue before
        public void TestRouteAdditionalInfoParse(string url, string expectedAdditionalInfo)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            Route testRoute = new Route() { URL = url };
            Parsers.ParseRouteAsync(testRoute).Wait();

            Assert.AreEqual(expectedAdditionalInfo, testRoute.AdditionalInfo);
        }

        [DataTestMethod] //https://stackoverflow.com/a/54296734/2246411
        [DataRow("/area/105791955/exit-38-deception-crags", new object[] { new[] { "/route-guide", "/area/105708966/washington", "/area/108471374/central-west-cascades-seattle", "/area/108471684/north-bend-vicinity", "/area/114278624/exit-38" } })] //Area
        [DataRow("/route/112177605/no-rang-na-rang", new object[] { new[] { "/route-guide", "/area/105907743/international", "/area/106661515/asia", "/area/106225629/south-korea", "/area/112177596/gwanaksankwanaksan-cha-oon-am-san-sleeping-rock-mountain" } })] //Route
        public void TestParentParse(string url, object[] expectedParentUrls)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            List<string> parents = Parsers.PopulateParentUrls(Utilities.GetHtmlDoc(url));
            for (int i = 0; i < parents.Count; i++)
                parents[i] = parents[i].Replace(Utilities.MPBASEURL, "");

            CollectionAssert.AreEqual(expectedParentUrls, parents); //Compare collections WITH order
        }

        [DataTestMethod]
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", "TR (2), Trad (1)")]
        [DataRow("/area/108184422/deception-wall", "Sport (17)")]
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
