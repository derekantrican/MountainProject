using System;
using System.Collections.Generic;

namespace MountainProjectAPI
{
    public class SearchResult
    {
        public SearchResult()
        {
            AllResults = new List<MPObject>();
        }

        public SearchResult(MPObject singleResult)
        {
            FilteredResult = singleResult;
            AllResults = new List<MPObject> { singleResult };
        }

        public SearchResult(MPObject singleResult, Area location)
        {
            FilteredResult = singleResult;
            AllResults = new List<MPObject> { singleResult };
            RelatedLocation = location;
        }

        public MPObject FilteredResult { get; set; }
        public List<MPObject> AllResults { get; set; }
        public Area RelatedLocation { get; set; }
        public TimeSpan TimeTaken { get; set; }
        /// <summary>
        /// A value determining the confidence of the result. The LOWER the value, the higher the confidence (1 = 100% confidence)
        /// </summary>
        public int Confidence { get; set; }

        public string UnconfidentReason { get; set; }

        public bool IsEmpty()
        {
            return FilteredResult == null || AllResults == null || AllResults.Count == 0;
        }
    }
}
