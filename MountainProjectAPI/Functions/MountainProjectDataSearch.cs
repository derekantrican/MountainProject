using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace MountainProjectAPI
{
    public static class MountainProjectDataSearch
    {
        public static List<Area> DestAreas = new List<Area>();

        public static void InitMountainProjectData(string xmlPath)
        {
            Console.WriteLine("Deserializing info from MountainProject");

            using (FileStream fileStream = new FileStream(xmlPath, FileMode.Open))
            {
                XmlSerializer xmlDeserializer = new XmlSerializer(typeof(List<Area>));
                DestAreas = (List<Area>)xmlDeserializer.Deserialize(fileStream);
            }

            if (DestAreas.Count == 0)
            {
                Console.WriteLine("Problem deserializing MountainProject info");
                Environment.Exit(13); //Invalid data
            }

            Console.WriteLine("MountainProject Info deserialized successfully");
        }

        public static SearchResult Search(string queryText, SearchParameters searchParameters = null)
        {
            Console.WriteLine($"    Getting info from MountainProject for \"{queryText}\"");
            Stopwatch searchStopwatch = Stopwatch.StartNew();
            SearchResult searchResult = new SearchResult();

            List<Tuple<string, string>> possibleQueryAndLocationGroups = GetPossibleQueryAndLocationGroups(queryText, searchParameters);
            List<SearchResult> possibleResults = new List<SearchResult>();
            foreach (Tuple<string, string> group in possibleQueryAndLocationGroups)
            {
                string query = Utilities.FilterStringForMatch(group.Item1);
                string location = Utilities.FilterStringForMatch(group.Item2);

                List<MPObject> locationMatches = DeepSearch(location, DestAreas, true);
                locationMatches.RemoveAll(p => !(p is Area));
                Area foundLocation = FilterByPopularity(locationMatches) as Area;
                if (foundLocation == null)
                    continue;

                List<MPObject> results = new List<MPObject>();
                if (foundLocation.SubAreas.Count > 0)
                    results = FilterBySearchParameters(SearchSubAreasForMatch(query, foundLocation.SubAreas), searchParameters);
                else
                    results = FilterBySearchParameters(SearchRoutes(query, foundLocation.Routes), searchParameters);

                MPObject filteredResult = FilterByPopularity(results);

                SearchResult possibleResult = new SearchResult() { RelatedLocation = foundLocation, AllResults = results, FilteredResult = filteredResult };
                if (!possibleResult.IsEmpty())
                    possibleResults.Add(possibleResult);
            }

            if (possibleResults.Count > 0)
                searchResult = possibleResults.OrderByDescending(p => p.FilteredResult.Popularity).First();
            else
            {
                queryText = Utilities.FilterStringForMatch(queryText);
                List<MPObject> results = FilterBySearchParameters(DeepSearch(queryText, DestAreas), searchParameters);
                MPObject filteredResult = FilterByPopularity(results);
                searchResult = new SearchResult() { AllResults = results, FilteredResult = filteredResult };
            }

            Console.WriteLine($"    Found {searchResult.AllResults.Count} matching results from MountainProject in {searchStopwatch.ElapsedMilliseconds} ms");

            return searchResult;
        }

        private static List<Tuple<string, string>> GetPossibleQueryAndLocationGroups(string queryText, SearchParameters searchParameters = null)
        {
            List<Tuple<string, string>> result = new List<Tuple<string, string>>();

            if (searchParameters != null && !string.IsNullOrEmpty(searchParameters.SpecificLocation))
            {
                result.Add(new Tuple<string, string>(queryText, searchParameters.SpecificLocation));
            }
            else
            {
                Regex locationWordsRegex = new Regex(@"(\s+of\s+)|(\s+on\s+)|(\s+at\s+)|(\s+in\s+)");
                string possibleSearchText, possibleLocation;

                if (queryText.Contains(",")) //Location by comma (eg "Send me on my way, Red River Gorge")
                {
                    possibleSearchText = queryText.Split(',')[0].Trim();
                    possibleLocation = queryText.Split(',')[1].Trim();

                    result.Add(new Tuple<string, string>(possibleSearchText, possibleLocation));
                }
                else if (locationWordsRegex.IsMatch(queryText)) //Location by "location word" (eg "Butterfly Blue in Red River Gorge")
                {
                    List<SearchResult> possibleResults = new List<SearchResult>();
                    foreach (Match match in locationWordsRegex.Matches(queryText))
                    {
                        possibleSearchText = queryText.Split(new string[] { match.Value }, StringSplitOptions.None)[0].Trim();
                        possibleLocation = queryText.Split(new string[] { match.Value }, StringSplitOptions.None)[1].Trim();
                        result.Add(new Tuple<string, string>(possibleSearchText, possibleLocation));
                    }
                }
            }

            return result;
        }

        private static List<MPObject> FilterBySearchParameters(List<MPObject> listToFilter, SearchParameters searchParameters)
        {
            List<MPObject> result = listToFilter.ToList();

            if (searchParameters == null)
                return result;

            if (searchParameters.OnlyRoutes)
                result.RemoveAll(p => !(p is Route));

            if (searchParameters.OnlyAreas)
                result.RemoveAll(p => !(p is Area));

            //Note: searchParameters.SpecificLocation is handled by the Search function

            return result;
        }

        private static MPObject FilterByPopularity(List<MPObject> listToFilter)
        {
            if (listToFilter.Count == 1)
                return listToFilter.First();
            if (listToFilter.Count >= 2)
            {
                List<MPObject> matchedObjectsByPopularity = listToFilter.OrderByDescending(p => p.Popularity).ToList();

                int pop1 = matchedObjectsByPopularity[0].Popularity;
                int pop2 = matchedObjectsByPopularity[1].Popularity;
                double popularityPercentDiff = Math.Round((double)(pop1 - pop2) / pop2 * 100, 2);

                Console.WriteLine($"    Filtering based on popularity (result has popularity {popularityPercentDiff}% higher than next closest)");

                return matchedObjectsByPopularity.First(); //Return the most popular matched object (this may prioritize areas over routes. May want to check that later)
            }
            else
                return null;
        }

        private static List<MPObject> DeepSearch(string input, List<Area> destAreas, bool allowDestAreaMatch = false)
        {
            List<MPObject> matchedObjects = new List<MPObject>();
            foreach (Area destArea in destAreas)
            {
                //Controls whether dest area names should be matched (should the keyword "Alabama" match the state or a route named "Sweet Home Alabama")
                if (allowDestAreaMatch && StringMatch(input, destArea.NameForMatch))
                {
                    matchedObjects.Add(destArea);
                }

                List<MPObject> matchedSubAreas = SearchSubAreasForMatch(input, destArea.SubAreas);
                matchedObjects.AddRange(matchedSubAreas);
            }

            return matchedObjects;
        }

        private static List<MPObject> SearchSubAreasForMatch(string input, List<Area> subAreas)
        {
            List<MPObject> matchedObjects = new List<MPObject>();

            foreach (Area subDestArea in subAreas)
            {
                if (StringMatch(input, subDestArea.NameForMatch))
                {
                    matchedObjects.Add(subDestArea);
                }

                if (subDestArea.SubAreas != null &&
                    subDestArea.SubAreas.Count() > 0)
                {
                    List<MPObject> matchedSubAreas = SearchSubAreasForMatch(input, subDestArea.SubAreas);
                    matchedObjects.AddRange(matchedSubAreas);
                }

                if (subDestArea.Routes != null &&
                    subDestArea.Routes.Count() > 0)
                {
                    List<MPObject> matchedRoutes = SearchRoutes(input, subDestArea.Routes);
                    matchedObjects.AddRange(matchedRoutes);
                }
            }

            return matchedObjects;
        }

        private static List<MPObject> SearchRoutes(string input, List<Route> routes)
        {
            List<MPObject> matchedObjects = new List<MPObject>();

            foreach (Route route in routes)
            {
                if (StringMatch(input, route.NameForMatch))
                {
                    matchedObjects.Add(route);
                }
            }

            return matchedObjects;
        }

        private static bool StringMatch(string inputString, string targetString, bool caseInsensitive = true)
        {
            string input = inputString;
            string target = targetString;

            if (caseInsensitive)
            {
                input = input.ToLower();
                target = target.ToLower();
            }

            return target.Contains(input);
        }

        public static MPObject GetItemWithMatchingUrl(string url, List<MPObject> listToSearch)
        {
            foreach (MPObject item in listToSearch)
            {
                if (item.URL == url)
                    return item;
                else
                {
                    if (item is Area)
                    {
                        MPObject matchingRoute = (item as Area).Routes.Find(p => p.URL == url);
                        if (matchingRoute != null)
                            return matchingRoute;
                        else
                        {
                            MPObject matchingSubArea = GetItemWithMatchingUrl(url, (item as Area).SubAreas.Cast<MPObject>().ToList());
                            if (matchingSubArea != null)
                                return matchingSubArea;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the parent for the child (ie Area that contains the object)
        /// </summary>
        /// <param name="child">The object of which to get the parent</param>
        /// <param name="parentLevel">The parent level. "0" is the highest parent, "1" is the next parent down, etc. "-1" will return the immediate parent</param>
        /// <returns>The parent object to the child. Will return null if the child has no parents or if the parentLevel is invalid</returns>
        public static MPObject GetParent(MPObject child, int parentLevel)
        {
            string url;
            if (parentLevel < 0 && Math.Abs(parentLevel) <= child.ParentUrls.Count) //"Negative indicies"
                url = child.ParentUrls[child.ParentUrls.Count + parentLevel];
            else if (parentLevel <= child.ParentUrls.Count - 1) //Positive indicies (check for "within range")
                url = child.ParentUrls[parentLevel];
            else //Out of range
                return null;

            return GetItemWithMatchingUrl(url, DestAreas.Cast<MPObject>().ToList());
        }
    }
}
