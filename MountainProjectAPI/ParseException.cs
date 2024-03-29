﻿using System;

namespace MountainProjectAPI
{
    public class ParseException : Exception
    {
        public ParseException(string message, Exception innerException = null) : base(message, innerException)
        {

        }

        public MPObject RelatedObject { get; set; }
        public string Html { get; set; }

        public ParseException GetInnermostParseException()
        {
            ParseException ex = this;
            while (ex.InnerException != null && ex.InnerException is ParseException)
            {
                ex = ex.InnerException as ParseException;
            }

            return ex;
        }
    }
}
