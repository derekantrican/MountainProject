using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

namespace MountainProjectDBBuilder
{
    public class Route
    {
        public enum RouteType
        {
            Boulder,
            TopRope,
            Sport,
            Trad
        }

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
        public string Grade { get; set; }
        public RouteType Type { get; set; }
        public string URL { get; set; }
        public string AdditionalInfo { get; set; }
        #endregion Public Properties

        public Route()
        {

        }

        public Route(string name, string grade, RouteType type, string url)
        {
            this.Name = WebUtility.HtmlDecode(name);
            this.Grade = grade;
            this.Type = type;
            this.URL = url;
        }

        public override string ToString()
        {
            return Name + " (" + Type.ToString() + ", " + Grade + ")";
        }
    }
}
