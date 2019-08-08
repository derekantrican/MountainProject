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

        public MPObject FilteredResult { get; set; }
        public List<MPObject> AllResults { get; set; }
        public Area RelatedLocation { get; set; }
        public TimeSpan TimeTaken { get; set; }

        public bool IsEmpty()
        {
            return FilteredResult == null || AllResults == null || AllResults.Count == 0;
        }
    }
}
