using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectBot;
using MountainProjectModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class SearchTests
    {
        [DataTestMethod]
        [DataRow("Red River Gorge", "https://www.mountainproject.com/area/105841134/red-river-gorge")]
        [DataRow("Prime Rib of Goat", "https://www.mountainproject.com/route/107730934/prime-rib-of-goat")]
        [DataRow("Exit 38: Deception Crags", "https://www.mountainproject.com/area/105791955/exit-38-deception-crags")]
        public void TestSearch(string query, string expectedUrl)
        {
            MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");
            MPObject result =  MountainProjectDataSearch.SearchMountainProject(query);

            Assert.AreEqual(expectedUrl, result.URL);
        }
    }
}
