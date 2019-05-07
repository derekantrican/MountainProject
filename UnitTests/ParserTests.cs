using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectDBBuilder;
using MountainProjectModels;

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

            CollectionAssert.AreEqual(expectedDestAreas, resultNames);
        }

        [DataTestMethod]
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", 2, 0)]
        [DataRow("/area/108184422/deception-wall", 0, 17)]
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
        [DataRow("/route/111859673/side-dish", "Side Dish", new[] { Route.RouteType.Sport }, "5.10c", "50 ft")]
        [DataRow("/route/109063052/geflugelfrikadelle", "Geflügelfrikadelle", new[] { Route.RouteType.Trad, Route.RouteType.Aid }, "5.12b/c", "40 ft")]
        [DataRow("/route/116181996/13-above-the-night", "13 Above the Night", new[] { Route.RouteType.Trad, Route.RouteType.Mixed, Route.RouteType.Ice, Route.RouteType.Alpine }, "WI4 M5", "1000 ft, 5 pitches, Grade IV")]
        [DataRow("/route/110425910/birds-of-a-feather", "Birds of a Feather", new[] { Route.RouteType.Sport, Route.RouteType.TopRope }, "5.8", "50 ft")]
        public void TestRouteParse(string url, string expectedName, Route.RouteType[] expectedTypes, string expectedGrade, string expectedAddInfo)
        {
            if (!url.Contains(Utilities.MPBASEURL))
                url = Utilities.MPBASEURL + url;

            Route testRoute = new Route() { URL = url };
            Parsers.ParseRouteAsync(testRoute).Wait();

            Assert.AreEqual(expectedName, testRoute.Name);
            CollectionAssert.AreEquivalent(expectedTypes, testRoute.Types);
            Assert.AreEqual(expectedGrade, testRoute.Grade);
            Assert.AreEqual(expectedAddInfo, testRoute.AdditionalInfo);
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
