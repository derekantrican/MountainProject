using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

namespace MountainProjectDBBuilder
{
    public class Route : Thing
    {
        public Route(string name, string grade, RouteType type, string url) : base(name, url)
        {
            this.Grade = grade;
            this.Type = type;
        }

        public Route()
        {

        }

        public enum RouteType
        {
            Boulder,
            TopRope,
            Sport,
            Trad
        }

        public string Grade { get; set; }
        public RouteType Type { get; set; }
        public string AdditionalInfo { get; set; }

        public override string ToString()
        {
            return Name + " (" + Type.ToString() + ", " + Grade + ")";
        }
    }
}
