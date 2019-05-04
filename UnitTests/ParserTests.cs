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
            if (!url.Contains(Utilities.BaseUrl))
                url = Utilities.BaseUrl + url;

            Area testSubDestArea = new Area() { URL = url };

            Parsers.ParseAreaAsync(testSubDestArea, false).Wait();

            int subsubareasCount = testSubDestArea.SubAreas == null ? 0 : testSubDestArea.SubAreas.Count;
            Assert.AreEqual(expectedSubAreas, subsubareasCount);

            int routesCount = testSubDestArea.Routes == null ? 0 : testSubDestArea.Routes.Count;
            Assert.AreEqual(expectedRoutes, routesCount);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", "Side Dish", Route.RouteType.Sport, "5.10c")]
        public void TestRouteParse(string url, string expectedName, Route.RouteType expectedType, string expectedGrade)
        {
            if (!url.Contains(Utilities.BaseUrl))
                url = Utilities.BaseUrl + url;

            Route testRoute = new Route() { URL = url };
            Parsers.ParseRouteAsync(testRoute).Wait();

            Assert.AreEqual(expectedName, testRoute.Name);
            Assert.AreEqual(expectedType, testRoute.Type);
            Assert.AreEqual(expectedGrade, testRoute.Grade);
        }

        [DataTestMethod]
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", "TR (2), Trad (1)")]
        [DataRow("/area/108184422/deception-wall", "Sport (17)")]
        [DataRow("/route/111859673/side-dish", "")] //Statistics aren't generated for routes
        public void TestStatisticsParse(string url, string expectedStatistics)
        {
            if (!url.Contains(Utilities.BaseUrl))
                url = Utilities.BaseUrl + url;

            AreaStats testStats = Parsers.PopulateStatistics(Utilities.GetHtmlDoc(url));

            Assert.AreEqual(expectedStatistics, testStats.ToString());
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", "50 ft")]
        public void TestAdditionalInfoParse(string url, string expectedAdditionalInfo)
        {
            if (!url.Contains(Utilities.BaseUrl))
                url = Utilities.BaseUrl + url;

            Route testRoute = new Route() { URL = url };
            Parsers.ParseRouteAsync(testRoute).Wait();

            Assert.AreEqual(expectedAdditionalInfo, testRoute.AdditionalInfo);
        }
    }
}
