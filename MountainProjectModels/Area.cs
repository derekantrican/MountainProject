using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MountainProjectModels
{
    public class Area : MPObject
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

        public AreaStats Statistics { get; set; }
        public List<Area> SubAreas { get; set; }
        public List<Route> Routes { get; set; }
    }
}
