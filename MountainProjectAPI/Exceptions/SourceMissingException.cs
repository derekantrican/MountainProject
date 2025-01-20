using System;

namespace MountainProjectAPI
{
    public class SourceMissingException : Exception
    {
        public SourceMissingException(string message, Exception innerException = null) : base(message, innerException)
        {

        }

        public MPObject RelatedObject { get; set; }
        public string Html { get; set; }
    }
}
