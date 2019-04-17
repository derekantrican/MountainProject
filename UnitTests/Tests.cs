using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectDBBuilder;

namespace UnitTests
{
    [TestClass]
    public class Tests
    {
        [DataTestMethod]
        [DataRow("/area/107605102/bankhead-forest-thompson-creek-trail", 2, 0)]
        [DataRow("/area/108184422/deception-wall", 0, 17)]
        public void TestSubAreaParse(string url, int expectedSubAreas, int expectedRoutes)
        {
            if (!url.Contains(Common.BaseUrl))
                url = Common.BaseUrl + url;

            SubDestArea testSubDestArea = new SubDestArea() { URL = url };

            Parsers.PopulateRoutes(testSubDestArea);

            int subsubareasCount = testSubDestArea.SubSubAreas == null ? 0 : testSubDestArea.SubSubAreas.Count;
            Assert.AreEqual(expectedSubAreas, subsubareasCount);

            int routesCount = testSubDestArea.Routes == null ? 0 : testSubDestArea.Routes.Count;
            Assert.AreEqual(expectedRoutes, routesCount);
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", "Side Dish", Route.RouteType.Sport, "5.10c")]
        public void TestRouteParse(string url, string expectedName, Route.RouteType expectedType, string expectedGrade)
        {
            if (!url.Contains(Common.BaseUrl))
                url = Common.BaseUrl + url;

            Route testRoute = Parsers.ParseRoute(url);

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
            if (!url.Contains(Common.BaseUrl))
                url = Common.BaseUrl + url;

            AreaStats testStats = Parsers.PopulateStatistics(url);

            Assert.AreEqual(expectedStatistics, testStats.ToString());
        }

        [DataTestMethod]
        [DataRow("/route/111859673/side-dish", "50 ft")]
        public void TestAdditionalInfoParse(string url, string expectedAdditionalInfo)
        {
            if (!url.Contains(Common.BaseUrl))
                url = Common.BaseUrl + url;

            Route testRoute = Parsers.ParseRoute(url);

            Assert.AreEqual(expectedAdditionalInfo, testRoute.AdditionalInfo);
        }
    }
}
