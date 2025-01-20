using System;
using System.IO;

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

        public string DumpToString()
        {
            string result = "";

            ParseException innerMostParseException = GetInnermostParseException();
            result += $"FAILING MPOBJECT: {innerMostParseException.RelatedObject.URL}\n";
            result += $"PATH: {string.Join(" -> ", innerMostParseException.RelatedObject.ParentIDs)}\n";
            result += $"EXCEPTION MESSAGE: {innerMostParseException.InnerException?.Message}\n";
            result += $"STACK TRACE: {innerMostParseException.InnerException?.StackTrace}\n\n";

            if (!string.IsNullOrEmpty(innerMostParseException.Html))
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Failing Object ({innerMostParseException.RelatedObject.ID}).html"), innerMostParseException.Html);
            }

            return result;
        }
    }
}
