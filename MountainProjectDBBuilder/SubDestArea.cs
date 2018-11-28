using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

namespace MountainProjectDBBuilder
{
    public class SubDestArea
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
        public List<SubDestArea> SubSubAreas { get; set; }
        public List<Route> Routes { get; set; }
        #endregion Public Properties

        public SubDestArea()
        {
            SubSubAreas = new List<SubDestArea>();
        }

        public SubDestArea(string name, string url)
        {
            this.Name = WebUtility.HtmlDecode(name);
            this.URL = url;
            Routes = new List<Route>();
            SubSubAreas = new List<SubDestArea>();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
