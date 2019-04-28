using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace MountainProjectDBBuilder
{
    public class Area : Thing
    {
        public Area(string name, string url) : base(name, url)
        {
            Routes = new List<Route>();
            SubAreas = new List<Area>();
        }

        public Area()
        {
            SubAreas = new List<Area>();
            Routes = new List<Route>();
        }

        #region Public Properties
        public AreaStats Statistics { get; set; }
        public List<Area> SubAreas { get; set; }
        public List<Route> Routes { get; set; }
        #endregion Public Properties
    }
}
