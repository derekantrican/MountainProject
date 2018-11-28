using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

namespace MountainProjectDBBuilder
{
    public class DestArea
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
        public List<SubDestArea> SubAreas { get; set; }
        #endregion Public Properties

        public DestArea()
        {
            SubAreas = new List<SubDestArea>();
        }

        public DestArea(string name, string url)
        {
            this.Name = WebUtility.HtmlDecode(name);
            this.URL = url;
            SubAreas = new List<SubDestArea>();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
