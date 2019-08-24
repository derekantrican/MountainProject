﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using static MountainProjectAPI.Route;

namespace MountainProjectAPI
{
    public static class MountainProjectDataSearch
    {
        public static List<Area> DestAreas = new List<Area>();

        #region Public Methods
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
            SearchResult searchResult;

            List<SearchResult> possibleResults = new List<SearchResult>();
            List<Tuple<string, string>> possibleQueryAndLocationGroups = GetPossibleQueryAndLocationGroups(queryText, searchParameters);
            foreach (Tuple<string, string> group in possibleQueryAndLocationGroups)
            {
                string query = Utilities.FilterStringForMatch(group.Item1);
                string location = Utilities.FilterStringForMatch(group.Item2);

                List<MPObject> possibleMatches = DeepSearch(query, DestAreas);
                possibleMatches = FilterBySearchParameters(possibleMatches, searchParameters);

                MPObject filteredResult = null;
                SearchResult possibleResult = new SearchResult();
                if (!string.IsNullOrEmpty(location))
                {
                    Dictionary<MPObject, Area> resultsWithLocations = GetMatchingResultLocationPairs(possibleMatches, location);
                    filteredResult = DetermineBestMatch(resultsWithLocations.Keys.ToList(), group.Item1, searchParameters);

                    if (filteredResult == null)
                        continue;

                    possibleResult = new SearchResult()
                    {
                        AllResults = resultsWithLocations.Keys.ToList(),
                        FilteredResult = filteredResult,
                        RelatedLocation = resultsWithLocations[filteredResult]
                    };
                }
                else
                {
                    filteredResult = DetermineBestMatch(possibleMatches, group.Item1, searchParameters);

                    if (filteredResult == null)
                        continue;

                    possibleResult = new SearchResult()
                    {
                        AllResults = possibleMatches,
                        FilteredResult = filteredResult
                    };
                }

                if (!possibleResult.IsEmpty())
                    possibleResults.Add(possibleResult);
            }

            if (possibleResults.Count > 0)
                searchResult = DetermineBestMatch(possibleResults, queryText, searchParameters);
            else if (searchParameters != null && !string.IsNullOrEmpty(searchParameters.SpecificLocation))
            {
                //If we used searchParameters to find a specific location, but couldn't find a match at that location,
                //we should return an empty result
                searchResult = new SearchResult();
            }
            else
            {
                string filteredQuery = Utilities.FilterStringForMatch(queryText);
                List<MPObject> results = FilterBySearchParameters(DeepSearch(filteredQuery, DestAreas), searchParameters);
                MPObject filteredResult = DetermineBestMatch(results, queryText, searchParameters);
                searchResult = new SearchResult()
                {
                    AllResults = results,
                    FilteredResult = filteredResult
                };
            }

            Console.WriteLine($"    Found {searchResult.AllResults.Count} matching results from MountainProject in {searchStopwatch.ElapsedMilliseconds} ms");

            searchResult.TimeTaken = searchStopwatch.Elapsed;

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
                result.Add(new Tuple<string, string>(queryText, "")); //Add the full query as a possible match

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

        public static MPObject GetItemWithMatchingUrl(string url)
        {
            return GetItemWithMatchingUrl(url, DestAreas);
        }

        public static MPObject GetItemWithMatchingUrl(string url, List<Area> listToSearch)
        {
            return GetItemWithMatchingUrl(url, listToSearch.Cast<MPObject>().ToList());
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
                            MPObject matchingSubArea = GetItemWithMatchingUrl(url, (item as Area).SubAreas);
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

            if (child.Parents.Count == 0)
                return GetItemWithMatchingUrl(url);
            else
                return child.Parents.Find(p => p.URL == url);
        }

        public static bool IsRouteGradeEqual(GradeSystem requestedSystem, string requestedGrade, Route route, bool allowRange = false, bool allowBaseOnlyMatch = false)
        {
            if (!route.Grades.ContainsKey(requestedSystem))
                return false;

            //On MountainProject, boulders take the form of "V7-8" and others take the form "5.11c/d"
            if (requestedSystem == GradeSystem.Hueco)
            {
                requestedGrade = Regex.Replace(requestedGrade, @"\\|\/", "-"); //Replace slashes with hyphens
                requestedGrade = requestedGrade.ToUpper();

                if (!requestedGrade.Contains("V"))
                    requestedGrade = "V" + requestedGrade;
            }
            else if (requestedSystem == GradeSystem.YDS)
            {
                requestedGrade = Regex.Replace(requestedGrade, @"((\-)|(\\))(?=[a-dA-D])", "/"); //Replace hyphens and backslashes with forward slashes (but not instances like 5.9-)
                requestedGrade = requestedGrade.ToLower();

                if (!requestedGrade.Contains("5."))
                    requestedGrade = "5." + requestedGrade;
            }

            if (route.Grades[requestedSystem] == requestedGrade)
                return true;

            //Do range matching (eg 5.11a/b or 5.11a-c should both match 5.11b)
            if (Regex.IsMatch(requestedGrade, @"[a-d][\/\\\-][a-d]|\d+[\/\\\-]\d+") && allowRange) //Match hyphens and slashes (but not instances like 5.9-)
            {
                string baseGrade = Regex.Replace(requestedGrade, @"[a-d][\/\\\-][a-d]|\d+[\/\\\-]\d+", "");
                char firstSubGrade = Convert.ToChar(Regex.Match(requestedGrade, @"[a-d\d+](?=[\/\\\-])").Value);
                char lastSubGrade = Convert.ToChar(Regex.Match(requestedGrade, @"(?<=[\/\\\-])[a-d\d+]").Value);
                for (char c = firstSubGrade; c <= lastSubGrade; c++)
                {
                    if (route.Grades[requestedSystem] == baseGrade + c)
                        return true;
                }

                //Todo: another valid range should be "V6-/+" meaning "V6-, V6, V6+". Only when the "-/+" or "-\+" string is present
            }

            //Do base-only matching (eg 5.10, 5.10a, 5.10+, 5.10b/c should all match "5.10")
            if (allowBaseOnlyMatch)
            {
                string baseGrade = Regex.Replace(requestedGrade, @"[a-d][\/\\\-][a-d]|\d+[\/\\\-]\d+", "");
                if (route.Grades[requestedSystem].Contains(baseGrade))
                    return true;
            }

            return false;
        }
        #endregion Public Methods

        #region Filter Methods
        private static SearchResult DetermineBestMatch(List<SearchResult> searchResults, string searchQuery, SearchParameters searchParameters = null)
        {
            MPObject bestMatch = DetermineBestMatch(searchResults.Select(p => p.FilteredResult).ToList(), searchQuery, searchParameters);
            return searchResults.Find(p => p.FilteredResult == bestMatch);
        }

        private static MPObject DetermineBestMatch(List<MPObject> allMatches, string searchQuery, SearchParameters searchParameters = null)
        {
            allMatches = FilterBySearchParameters(allMatches, searchParameters);

            //First priority: items where the name matches the search query exactly (but still case-insensitive)
            List<MPObject> matchingItems = allMatches.Where(p => Utilities.StringsEqual(p.Name, searchQuery)).ToList();
            if (matchingItems.Count > 0)
                return FilterByPopularity(matchingItems);

            //Second priority: items where the name matches the FILTERED (no symbols or spaces, case insensitive) search query exactly
            matchingItems = allMatches.Where(p => Utilities.StringsEqual(p.NameForMatch, Utilities.FilterStringForMatch(searchQuery))).ToList();
            if (matchingItems.Count > 0)
                return FilterByPopularity(matchingItems);

            //[IN THE FUTURE]Third priority: items with a levenshtein distance less than 3

            //Fourth priority: items where the name contains the search query (still case-insensitive)
            matchingItems = allMatches.Where(p => Utilities.StringsMatch(p.Name, searchQuery)).ToList();
            if (matchingItems.Count > 0)
                return FilterByPopularity(matchingItems);

            //Fifth priority: items where the name contains the FITLERED search query (still case-insensitive)
            matchingItems = allMatches.Where(p => Utilities.StringsMatch(p.NameForMatch, Utilities.FilterStringForMatch(searchQuery))).ToList();
            if (matchingItems.Count > 0)
                return FilterByPopularity(matchingItems);

            //Finally, if we haven't matched anything above, just filter by priority
            return FilterByPopularity(allMatches);
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

            //Note: searchParameters.SpecificLocation is handled by the GetPossibleQueryAndLocationGroups function

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

        private static SearchResult FilterByPopularity(List<SearchResult> listToFilter)
        {
            if (listToFilter.Count == 1)
                return listToFilter.First();
            if (listToFilter.Count >= 2)
            {
                List<SearchResult> resultsByPopularity = listToFilter.OrderByDescending(p => p.FilteredResult.Popularity).ToList();

                int pop1 = resultsByPopularity[0].FilteredResult.Popularity;
                int pop2 = resultsByPopularity[1].FilteredResult.Popularity;
                double popularityPercentDiff = Math.Round((double)(pop1 - pop2) / pop2 * 100, 2);

                Console.WriteLine($"    Filtering based on popularity (result has popularity {popularityPercentDiff}% higher than next closest)");

                return resultsByPopularity.First(); //Return the most popular matched object (this may prioritize areas over routes. May want to check that later)
            }
            else
                return new SearchResult(); //Return empty SearchResult as default
        }
        #endregion Filter Methods

        #region Private Search (Recursive)

        private static List<MPObject> DeepSearch(string input, List<Area> destAreas, bool allowDestAreaMatch = false)
        {
            List<MPObject> matchedObjects = new List<MPObject>();

            if (string.IsNullOrWhiteSpace(input)) //Don't allow a blank string search
                return matchedObjects;

            foreach (Area destArea in destAreas)
            {
                //Controls whether dest area names should be matched (should the keyword "Alabama" match the state or a route named "Sweet Home Alabama")
                if (allowDestAreaMatch && Utilities.StringsMatch(input, destArea.NameForMatch))
                    matchedObjects.Add(destArea);

                List<MPObject> matchedSubAreas = SearchSubAreasForMatch(input, destArea.SubAreas);
                matchedSubAreas.ForEach(a =>
                {
                    if (!a.Parents.Contains(destArea))
                        a.Parents.Add(destArea);
                });
                matchedObjects.AddRange(matchedSubAreas);
            }

            return matchedObjects;
        }

        private static List<MPObject> SearchSubAreasForMatch(string input, List<Area> subAreas)
        {
            List<MPObject> matchedObjects = new List<MPObject>();

            foreach (Area subDestArea in subAreas)
            {
                if (Utilities.StringsMatch(input, subDestArea.NameForMatch))
                    matchedObjects.Add(subDestArea);

                if (subDestArea.SubAreas != null &&
                    subDestArea.SubAreas.Count() > 0)
                {
                    List<MPObject> matchedSubAreas = SearchSubAreasForMatch(input, subDestArea.SubAreas);
                    matchedSubAreas.ForEach(a =>
                    {
                        if (!a.Parents.Contains(subDestArea))
                            a.Parents.Add(subDestArea);
                    });
                    matchedObjects.AddRange(matchedSubAreas);
                }

                if (subDestArea.Routes != null &&
                    subDestArea.Routes.Count() > 0)
                {
                    List<MPObject> matchedRoutes = SearchRoutes(input, subDestArea.Routes);
                    matchedRoutes.ForEach(r => 
                    {
                        if (!r.Parents.Contains(subDestArea))
                            r.Parents.Add(subDestArea);
                    });
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
                if (Utilities.StringsMatch(input, route.NameForMatch))
                    matchedObjects.Add(route);
            }

            return matchedObjects;
        }
        #endregion Private Search (Recursive)

        #region Helper Methods
        private static Dictionary<MPObject, Area> GetMatchingResultLocationPairs(List<MPObject> listToFilter, string location)
        {
            Dictionary<MPObject, Area> results = new Dictionary<MPObject, Area>();
            foreach (MPObject result in listToFilter)
            {
                Area matchingParent = result.Parents.ToList().FirstOrDefault(p => Utilities.StringsMatch(location, p.NameForMatch)) as Area;
                if (matchingParent != null)
                    results.Add(result, matchingParent);
            }

            return results;
        }
        #endregion Helper Methods
    }
}
