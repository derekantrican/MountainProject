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

        public Exception GetInnermostException()
        {
            Exception ex = InnerException;
            while (ex?.InnerException != null)
            {
                ex = ex.InnerException;
            }

            return ex;
        }

        public override string ToString()
        {
            return $"SourceMissingException: {Message}\n{GetInnermostException()}";
        }
    }
}
