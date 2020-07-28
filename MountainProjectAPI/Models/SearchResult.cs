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

        public SearchResult(MPObject singleResult, Area relatedLocation = null)
        {
            FilteredResult = singleResult;
            AllResults = new List<MPObject> { singleResult };
            RelatedLocation = relatedLocation;
        }

        public MPObject FilteredResult { get; set; }
        public List<MPObject> AllResults { get; set; }
        public Area RelatedLocation { get; set; }
        /// <summary>
        /// The amount of time taken to search in milliseconds
        /// </summary>
        public long TimeTakenMS { get; set; }
        /// <summary>
        /// A value determining the confidence of the result. The LOWER the value, the higher the confidence (1 = 100% confidence)
        /// </summary>
        public int Confidence { get; set; }

        public string UnconfidentReason { get; set; }

        public bool IsEmpty()
        {
            return FilteredResult == null || AllResults == null || AllResults.Count == 0;
        }

        public TimeSpan TimeSpanTaken()
        {
            return TimeSpan.FromMilliseconds(TimeTakenMS);
        }
    }
}
