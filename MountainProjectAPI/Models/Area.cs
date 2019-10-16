using System.Collections.Generic;
using System.Linq;

namespace MountainProjectAPI
{
    public class Area : MPObject
    {
        public Area(string name, string id) : base(name, id)
        {
            Routes = new List<Route>();
            SubAreas = new List<Area>();
            PopularRouteIDs = new List<string>();
        }

        public Area()
        {
            SubAreas = new List<Area>();
            Routes = new List<Route>();
            PopularRouteIDs = new List<string>();
        }

        public AreaStats Statistics { get; set; }
        public List<Area> SubAreas { get; set; }
        public List<Route> Routes { get; set; }
        public List<string> PopularRouteIDs { get; set; }

        public List<Route> GetPopularRoutes(int numberToReturn)
        {
            List<Route> childRoutes = GetAllRoutes(this);
            childRoutes = childRoutes.OrderByDescending(p => p.Popularity).ToList();
            return childRoutes.Take(numberToReturn).ToList();
        }

        private List<Route> GetAllRoutes(Area area)
        {
            List<Route> routes = new List<Route>();
            routes.AddRange(area.Routes);
            foreach (Area subArea in area.SubAreas)
                routes.AddRange(GetAllRoutes(subArea));

            return routes;
        }

        public override string ToString()
        {
            return $"{this.Name} [{this.Statistics}]";
        }
    }
}
