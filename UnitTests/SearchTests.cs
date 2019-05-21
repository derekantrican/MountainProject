using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectAPI;
using System.Collections.Generic;

namespace UnitTests
{
    [TestClass]
    public class SearchTests
    {
        string[,] testCriteria = new string[,]
        {
            { "Red River Gorge", "/area/105841134/red-river-gorge" },
            { "Prime Rib of Goat", "/route/107730934/prime-rib-of-goat" },
            { "Exit 38: Deception Crags", "/area/105791955/exit-38-deception-crags" },
            { "Deception Crags", "/area/105791955/exit-38-deception-crags" }, //Partial name match
            { "Helm's Deep", "/route/106887440/helms-deep" }, //Special character (apostrophe)
            { "Helm’s Deep", "/route/106887440/helms-deep" }  //Special character (single quotation mark)
        };

        [TestMethod]
        public void TestSearch()
        {
            MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");

            for (int i = 0; i < testCriteria.GetLength(0); i++)
            {
                string query = testCriteria[i, 0];
                string expectedUrl = testCriteria[i, 1];
                MPObject result = MountainProjectDataSearch.SearchMountainProject(query);

                Assert.AreEqual(Utilities.MPBASEURL + expectedUrl, result.URL);
            }
        }
    }
}
