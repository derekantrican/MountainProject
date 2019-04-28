﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MountainProjectDBBuilder
{
    public class Enums
    {
        public enum Thing
        {
            DestArea,
            SubDestArea,
            Route
        }

        public enum Mode
        {
            None,
            Parse,
            ParseDirect,
            BuildDB
        }
    }
}
