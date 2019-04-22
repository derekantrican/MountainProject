using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace MountainProjectDBBuilder
{
    public class Area
    {
        #region Public Properties
        private string name { get; set; }
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                if (Regex.Match(value, ", The", RegexOptions.IgnoreCase).Success)
                {
                    value = Regex.Replace(value, ", The", "", RegexOptions.IgnoreCase);
                    value = "The " + value;
                }

                name = value.Trim();
            }
        }
        public string URL { get; set; }
        public AreaStats Statistics { get; set; }
        public List<Area> SubAreas { get; set; }
        public List<Route> Routes { get; set; }
        #endregion Public Properties

        public Area()
        {
            SubAreas = new List<Area>();
            Routes = new List<Route>();
        }

        public Area(string name, string url)
        {
            this.Name = WebUtility.HtmlDecode(name);
            this.URL = url;
            Routes = new List<Route>();
            SubAreas = new List<Area>();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
