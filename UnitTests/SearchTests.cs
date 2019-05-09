using Microsoft.VisualStudio.TestTools.UnitTesting;
using MountainProjectAPI;

namespace UnitTests
{
    [TestClass]
    public class SearchTests
    {
        [DataTestMethod]
        [DataRow("Red River Gorge", "/area/105841134/red-river-gorge")]
        [DataRow("Prime Rib of Goat", "/route/107730934/prime-rib-of-goat")]
        [DataRow("Exit 38: Deception Crags", "/area/105791955/exit-38-deception-crags")]
        [DataRow("Deception Crags", "/area/105791955/exit-38-deception-crags")]
        public void TestSearch(string query, string expectedUrl)
        {
            MountainProjectDataSearch.InitMountainProjectData(@"..\..\MountainProjectDBBuilder\bin\MountainProjectAreas.xml");
            MPObject result =  MountainProjectDataSearch.SearchMountainProject(query);

            Assert.AreEqual(Utilities.MPBASEURL + expectedUrl, result.URL);
        }
    }
}
