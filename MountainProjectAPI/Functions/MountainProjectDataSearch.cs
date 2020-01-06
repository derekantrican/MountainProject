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
            Console.WriteLine($"\tGetting info from MountainProject for \"{queryText}\"");
            Stopwatch searchStopwatch = Stopwatch.StartNew();
            SearchResult searchResult;

            List<SearchResult> possibleResults = new List<SearchResult>();
            List<Tuple<string, string>> possibleQueryAndLocationGroups = GetPossibleQueryAndLocationGroups(queryText, searchParameters);
            foreach (Tuple<string, string> group in possibleQueryAndLocationGroups)
            {
                string query = Utilities.FilterStringForMatch(Utilities.EnforceWordConsistency(group.Item1));
                string location = Utilities.FilterStringForMatch(Utilities.EnforceWordConsistency(group.Item2));

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

            if (possibleResults.Any())
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

            Console.WriteLine($"\tFound {searchResult.AllResults.Count} matching results from MountainProject in {searchStopwatch.ElapsedMilliseconds} ms");

            searchResult.TimeTaken = searchStopwatch.Elapsed;

            return searchResult;
        }

        #region Post Title Parsing
        public static SearchResult ParseRouteFromString(string inputString)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            SearchResult finalResult = new SearchResult(); //Todo: in the future support returning multiple routes (but only if there are multiple grades in the title? Maybe only if all of the routes' full names are in the title?)
            List<Tuple<Route, Area, string>> possibleResults = new List<Tuple<Route, Area, string>>();

            List<Grade> postGrades = Grade.ParseString(inputString);
            Console.WriteLine($"\tRecognized grade(s): {string.Join(" | ", postGrades)}");

            List<string> possibleRouteNames = GetPossibleRouteNames(inputString);
            Console.WriteLine($"\tRecognized name(s): {string.Join(" | ", possibleRouteNames)}");
            
            foreach (string possibleRouteName in possibleRouteNames)
            {
                string inputWithoutName = inputString.Replace(possibleRouteName, "");
                SearchResult searchResult = Search(possibleRouteName, new SearchParameters() { OnlyRoutes = true });
                if (!searchResult.IsEmpty() && searchResult.AllResults.Count < 75) //If the number of matching results is greater than 75, it was probably a very generic word for a search (eg "There")
                {
                    if (searchResult.AllResults.Count == 1 && ParentsInString(searchResult.AllResults.First(), inputWithoutName, false, true).Any())
                        possibleResults.Add(new Tuple<Route, Area, string>(searchResult.AllResults.First() as Route, searchResult.RelatedLocation, inputWithoutName));
                    else if (searchResult.AllResults.Count(r => ParentsInString(r as Route, inputWithoutName, false, true).Any() && Utilities.StringContainsWithFilters(inputString, r.Name, true)) == 1)
                    {
                        Route route = searchResult.AllResults.First(r => ParentsInString(r as Route, inputWithoutName, false, true).Any() &&
                                                                         Utilities.StringContainsWithFilters(inputString, r.Name, true)) as Route;
                        possibleResults.Add(new Tuple<Route, Area, string>(route, searchResult.RelatedLocation, inputWithoutName));
                    }
                    else if (postGrades.Any())
                    {
                        if (searchResult.AllResults.Any(r => ParentsInString(r, inputWithoutName).Any())) //If some routes have a location in the inputString, work with those
                        {
                            foreach (Route route in searchResult.AllResults.Where(r => ParentsInString(r, inputWithoutName).Any()).Cast<Route>())
                            {
                                if (route.Grades.Any(g => postGrades.Any(p => g.Equals(p, true, true))))
                                    possibleResults.Add(new Tuple<Route, Area, string>(route, searchResult.RelatedLocation, inputWithoutName));
                            }
                        }
                        else
                        {
                            foreach (Route route in searchResult.AllResults.Cast<Route>())
                            {
                                if (route.Grades.Any(g => postGrades.Any(p => g.Equals(p, true, true))))
                                    possibleResults.Add(new Tuple<Route, Area, string>(route, searchResult.RelatedLocation, inputWithoutName));
                            }
                        }
                    }
                    else if (searchResult.AllResults.Any(r => ParentsInString(r as Route, inputWithoutName, false, true).Any() && Utilities.StringContainsWithFilters(inputString, r.Name, true)))
                    {
                        List<MPObject> routesWhereParentIsInString = searchResult.AllResults.Where(r => ParentsInString(r as Route, inputWithoutName, false, true).Any() &&
                                                                                                        Utilities.StringContainsWithFilters(inputString, r.Name, true)).ToList();
                        routesWhereParentIsInString.ForEach(r => possibleResults.Add(new Tuple<Route, Area, string>(r as Route, searchResult.RelatedLocation, inputWithoutName)));
                    }
                }
            }

            possibleResults = possibleResults.GroupBy(x => x.Item1).Select(g => g.First()).ToList(); //Distinct routes

            if (possibleResults.Any())
            {
                //Todo: prioritize routes where the grade matches exactly (eg 5.11a matches 5.11a rather than matching 5.11a-b)
                //Todo: for matching parents in string, maybe give higher priority to results that match MORE parents. EG "Once Upon a Time - Black Mountain, California" gives
                //  2 routes named "Once upon a time" in California. But only one is also at "Black Mountain"

                //Prioritize routes where the full name is in the input string
                //(Additionally, we could also prioritize how close - within the input string - the name is to the rating)
                List<Tuple<Route, Area, string>> filteredResults = possibleResults.Where(p => Utilities.StringContainsWithFilters(inputString, p.Item1.Name, true)).ToList();

                int highConfidence = 1;
                int medConfidence = 2;
                int lowConfidence = 3;
                int confidence = lowConfidence;
                string unconfidentReason = null;
                if (filteredResults.Count == 1)
                {
                    if (ParentsInString(filteredResults.First().Item1, inputString, true).Any() ||
                        Grade.ParseString(inputString, false).Any(g => filteredResults.First().Item1.Grades.Any(p => g.Equals(p))))
                        confidence = highConfidence; //Highest confidence when we also match a location in the string or if we match a full grade
                    else
                    {
                        confidence = medConfidence; //Medium confidence when we have only found one match with that exact name but can't match a location in the string
                        unconfidentReason = "Single result found, but no parents or grades matched";
                    }
                }
                else if (filteredResults.Count > 1)
                {
                    //Prioritize routes where one of the parents (locations) is also in the input string
                    List<Tuple<Route, Area, string>> routesWithMatchingLocations = filteredResults.Where(r => ParentsInString(r.Item1, r.Item3).Any()).ToList();
                    if (routesWithMatchingLocations.Any())
                    {
                        filteredResults = routesWithMatchingLocations;

                        if (postGrades.Any())
                            confidence = highConfidence; //Highest confidence when we have found the location in the string
                        else
                        {
                            confidence = medConfidence;
                            unconfidentReason = $"{filteredResults.Count} EXACTLY matching routes (name & location w/o abbrev). No grades matched";
                        }
                    }
                    else
                    {
                        routesWithMatchingLocations = filteredResults.Where(r => ParentsInString(r.Item1, r.Item3, true).Any()).ToList();
                        if (routesWithMatchingLocations.Any())
                        {
                            filteredResults = routesWithMatchingLocations;

                            if (postGrades.Any())
                                confidence = highConfidence; //Highest confidence when we have found the location in the string
                            else
                            {
                                confidence = medConfidence;
                                unconfidentReason = $"{filteredResults.Count} EXACTLY matching routes (name & location w/ abbrev). No grades matched";
                            }
                        }
                        else
                        {
                            routesWithMatchingLocations = filteredResults.Where(r => ParentsInString(r.Item1, r.Item3, true, true).Any()).ToList();
                            if (routesWithMatchingLocations.Any())
                            {
                                filteredResults = routesWithMatchingLocations;
                                confidence = medConfidence; //Medium confidence when we have matched only part of a parent's name
                                unconfidentReason = $"{filteredResults.Count} EXACTLY matching routes (name & PARTIAL location w/ abbrev)";
                            }
                        }
                    }
                }
                else
                {
                    //Prioritize routes where one of the parents (locations) is also in the input string
                    List<Tuple<Route, Area, string>> routesWithMatchingLocations = possibleResults.Where(r => ParentsInString(r.Item1, r.Item3).Any()).ToList();
                    if (routesWithMatchingLocations.Any())
                    {
                        filteredResults = routesWithMatchingLocations;
                        confidence = medConfidence; //Medium confidence when we didn't match a full route name, but have found a parent location in the string
                        unconfidentReason = $"{filteredResults.Count} PARTIALLY matching routes (name & location w/o abbrev)";
                    }
                    else
                    {
                        routesWithMatchingLocations = possibleResults.Where(r => ParentsInString(r.Item1, r.Item3, true).Any()).ToList();
                        if (routesWithMatchingLocations.Any())
                        {
                            filteredResults = routesWithMatchingLocations;
                            confidence = medConfidence; //Medium confidence when we didn't match a full route name, but have found a parent location in the string (including the possibility of the state abbrv)
                            unconfidentReason = $"{filteredResults.Count} PARTIALLY matching routes (name & location w/ abbrev)";
                        }
                        else
                        {
                            routesWithMatchingLocations = possibleResults.Where(r => ParentsInString(r.Item1, r.Item3, true, true).Any()).ToList();
                            if (routesWithMatchingLocations.Any())
                            {
                                filteredResults = routesWithMatchingLocations;
                                confidence = medConfidence; //Medium confidence when we didn't match a full route name and have matched only part of a parent's name in the string
                                unconfidentReason = $"{filteredResults.Count} PARTIALLY matching routes (name & PARTIAL location w/ abbrev)";
                            }
                        }
                    }
                }

                Tuple<Route, Area, string> chosenRoute;
                Area location;
                List<MPObject> allResults = new List<MPObject>();
                if (filteredResults.Count == 1)
                {
                    chosenRoute = filteredResults.First();
                    allResults.Add(chosenRoute.Item1);
                }
                else if (filteredResults.Count > 1)
                {
                    chosenRoute = filteredResults.OrderByDescending(p => p.Item1.Popularity).First();
                    allResults.AddRange(filteredResults.Select(p => p.Item1));
                    confidence = medConfidence; //Medium confidence when we have matched the string exactly, but there are multiple results
                    unconfidentReason ??= $"Too many filtered results ({filteredResults.Count})";
                }
                else
                {
                    chosenRoute = possibleResults.OrderByDescending(p => p.Item1.Popularity).First();
                    allResults.AddRange(possibleResults.Select(p => p.Item1));
                    confidence = lowConfidence; //Low confidence when we can't match the string exactly, haven't matched any locations, and there are multiple results
                    unconfidentReason ??= "No filtered results. Chose most popular partial match instead";
                }

                location = chosenRoute.Item2;
                if (location == null)
                    location = ParentsInString(chosenRoute.Item1, chosenRoute.Item3, allowPartialParents: true).FirstOrDefault(p => p.ID != GetOuterParent(chosenRoute.Item1).ID) as Area;

                finalResult = new SearchResult(chosenRoute.Item1, location)
                {
                    AllResults = allResults,
                    Confidence = confidence,
                    UnconfidentReason = unconfidentReason
                };
            }

            finalResult.TimeTaken = stopwatch.Elapsed;

            return finalResult;
        }

        private static List<MPObject> ParentsInString(MPObject child, string inputString, bool allowStateAbbr = false, bool allowPartialParents = false, bool caseSensitive = false)
        {
            List<MPObject> matchedParents = new List<MPObject>();
            List<string> possibleNames = GetPossibleRouteNames(inputString);

            if (caseSensitive)
            {
                if (child.Parents.Any(p => possibleNames.Any(n => Utilities.StringContainsWithFilters(p.Name, n))))
                    matchedParents.AddRange(child.Parents.Where(p => possibleNames.Any(n => Utilities.StringContainsWithFilters(p.Name, n, enforceConsistentWords: false))));
            }
            else
            {
                if (child.Parents.Any(p => possibleNames.Any(n => Utilities.StringContainsWithFilters(p.Name, n, true))))
                    matchedParents.AddRange(child.Parents.Where(p => possibleNames.Any(n => Utilities.StringContainsWithFilters(p.Name, n, true, false))));
            }

            if (allowStateAbbr)
            {
                foreach (MPObject parent in child.Parents)
                {
                    if (Utilities.StatesWithAbbr.ContainsKey(parent.Name) && inputString.Contains(Utilities.StatesWithAbbr[parent.Name]))
                        matchedParents.Add(parent);
                }
            }

            if (allowPartialParents)
            {
                foreach (MPObject parent in child.Parents)
                {
                    if (caseSensitive)
                    {
                        List<string> parentWords = Utilities.GetWordGroups(Utilities.FilterStringForMatch(parent.Name, false), false);
                        if (parentWords.Any(p => Utilities.FilterStringForMatch(inputString, false).Contains(p)))
                            matchedParents.Add(parent);
                    }
                    else
                    {
                        List<string> parentWords = Utilities.GetWordGroups(Utilities.FilterStringForMatch(parent.Name.ToLower(), false), false);
                        if (parentWords.Any(p => Utilities.FilterStringForMatch(inputString, false).ToLower().Contains(p)))
                            matchedParents.Add(parent);
                    }
                }
            }

            matchedParents = matchedParents.Distinct().ToList();
            matchedParents = matchedParents.OrderByDescending(p => child.ParentIDs.IndexOf(p.ID)).ToList(); //Sort by closest to child

            return matchedParents;
        }

        private static List<string> GetPossibleRouteNames(string postTitle)
        {
            //Todo: this method should also filter out locations as possible route names (of the form ", POSSIBLENAME"). 
            //For instance, in the postTitle "Evening session on Dasani 6-. Morrison, Colorado" Colorado should not be 
            //a possible name. This may be difficult because there could also be "My first 12, Jade" (maybe that's ok, though).
            //Maybe in a series of "POSSIBLE_NAME, POSSIBLE_NAME, POSSIBLE_NAME" (possible names separated by commas and optional
            //spaces) we only take the first one in the list. For instance, in this:
            //"Sex Cave Sector of the Daliwood Boulders, in Dali, Yunnan Province, China" we should only consider
            //"Sex Cave Sector of the Daliwood Boulders" (and that even below will get broken into "Sex Cave Sector" and
            //"Daliwood Boulders" for alternate matches)

            List<string> result = new List<string>();

            postTitle = Regex.Replace(postTitle, @"\d+[\\/]\d+[\\/]\d+", ""); //Remove dates from post titles

            Regex routeGradeRegex = new Regex(@"((5\.)\d+[+-]?[a-dA-D]?([\/\\\-][a-dA-D])?)|([vV]\d+([\/\\\-]\d+)?)");
            postTitle = routeGradeRegex.Replace(postTitle, "");

            string[] connectingWords = new string[] { "and", "the", "to", "or", "a", "an", "was", "but" };
            string[] locationWords = new string[] { "of", "on", "at", "in" };
            connectingWords = connectingWords.Concat(locationWords).ToArray();

            string possibleRouteName = "";
            foreach (string word in Regex.Split(postTitle, @"[^\p{L}0-9'’]"))
            {
                if ((!string.IsNullOrWhiteSpace(word) && char.IsUpper(word[0]) && word.Length > 1) ||
                    (connectingWords.Contains(word.ToLower()) && !string.IsNullOrWhiteSpace(possibleRouteName)) || Utilities.IsNumber(word))
                    possibleRouteName += word + " ";
                else if (!string.IsNullOrWhiteSpace(possibleRouteName))
                {
                    possibleRouteName = possibleRouteName.Trim();
                    result.Add(possibleRouteName);

                    //If there is a "location word" in the possibleRouteName, add the separate parts to the result as well
                    Regex locationWordsRegex = new Regex(@"(\s+" + string.Join(@"\s+)|(\s+", locationWords) + @"\s+)");
                    if (locationWordsRegex.IsMatch(possibleRouteName))
                        locationWordsRegex.Split(possibleRouteName).ToList().ForEach(p => result.Add(Utilities.TrimWords(p, connectingWords).Trim()));

                    possibleRouteName = "";
                }
            }

            //Add any "remaining" names
            if (!string.IsNullOrWhiteSpace(possibleRouteName) && possibleRouteName != "V")
            {
                possibleRouteName = possibleRouteName.Trim();
                result.Add(possibleRouteName);

                //If there is a "location word" in the possibleRouteName, add the separate parts to the result as well
                Regex locationWordsRegex = new Regex(@"(\s+" + string.Join(@"\s+)|(\s+", locationWords) + @"\s+)");
                if (locationWordsRegex.IsMatch(possibleRouteName))
                    locationWordsRegex.Split(possibleRouteName).ToList().ForEach(p => result.Add(Utilities.TrimWords(p, connectingWords).Trim()));
            }

            result = result.Distinct().ToList();
            result.RemoveAll(p => p.Length < 3); //Remove any short "names" (eg "My")
            result.RemoveAll(p => connectingWords.Contains(p.ToLower())); //Remove any "possible names" that are only a connecting word (eg "the")
            result.RemoveAll(p => Utilities.IsNumber(p));

            //Todo: experiment with using all "word combinations". IE once we have parsed our list of "possibleRouteNames", expand the list to
            //be all combinations of those words (respecting order and not "cross-contaminating groups"). EG "Climbed Matthes Crest"
            //possibleRouteName should be expanded to "Climbed Matthes Crest", "Climbed", "Matthes", "Crest", "Climbed Matthes", "Matthes Crest".
            //See if this actually improves results or hurts it. (After this, we should probably run the "RemoveAll" lines again and remove the 
            //"locationWordsRegex.Split" section above)

            return result;
        }
        #endregion Post Title Parsing

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

        public static MPObject GetItemWithMatchingID(string id)
        {
            return GetItemWithMatchingID(id, DestAreas);
        }

        public static MPObject GetItemWithMatchingID(string id, List<Area> listToSearch)
        {
            return GetItemWithMatchingID(id, listToSearch.Cast<MPObject>().ToList());
        }

        public static MPObject GetItemWithMatchingID(string id, List<MPObject> listToSearch)
        {
            foreach (MPObject item in listToSearch)
            {
                if (item.ID == id)
                    return item;
                else
                {
                    if (item is Area)
                    {
                        MPObject matchingRoute = (item as Area).Routes.Find(p => p.ID == id);
                        if (matchingRoute != null)
                            return matchingRoute;
                        else
                        {
                            MPObject matchingSubArea = GetItemWithMatchingID(id, (item as Area).SubAreas);
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
            string id;
            if (parentLevel < 0 && Math.Abs(parentLevel) <= child.ParentIDs.Count) //"Negative indicies"
                id = child.ParentIDs[child.ParentIDs.Count + parentLevel];
            else if (parentLevel <= child.ParentIDs.Count - 1) //Positive indicies (check for "within range")
                id = child.ParentIDs[parentLevel];
            else //Out of range
                return null;

            if (child.Parents.Count == 0)
                return GetItemWithMatchingID(id);
            else
                return child.Parents.Find(p => p.ID == id);
        }

        /// <summary>
        /// Gets the outer parent (state for US, country for international) for a child
        /// </summary>
        /// <param name="child">The area or route of the parent to return</param>
        /// <returns></returns>
        public static MPObject GetOuterParent(MPObject child)
        {
            MPObject parent = GetParent(child, 0); //Get state that route/area is in
            if (parent.URL == Utilities.GetSimpleURL(Utilities.INTERNATIONALURL)) //If this is international, get the country instead of the state (eg "China")
            {
                if (child.ParentIDs.Count > 3)
                {
                    if (child.ParentIDs.Contains(Utilities.GetID(Utilities.AUSTRALIAURL))) //Australia is both a continent and a country so it is an exception
                        parent = GetParent(child, 1);
                    else
                        parent = GetParent(child, 2);
                }
                else
                    parent = null; //Return null if "child" is an area like "China" (so we don't return a string like "China is located in Asia")
            }

            return parent;
        }

        /// <summary>
        /// Gets the inner parent for a child
        /// </summary>
        /// <param name="child">The area or route of the parent to return</param>
        /// <returns></returns>
        public static MPObject GetInnerParent(MPObject child)
        {
            MPObject parent = null;
            if (child is Route)
            {
                parent = GetParent(child, -2); //Get the "second to last" parent https://github.com/derekantrican/MountainProject/issues/12

                if (parent.URL == GetParent(child, 0).URL)
                    parent = GetParent(child, -1);
            }
            else if (child is Area)
                parent = GetParent(child, -1); //Get immediate parent

            if (parent != null && parent.URL == Utilities.GetSimpleURL(Utilities.INTERNATIONALURL)) //If "child" is an area like "Europe"
                parent = null;

            return parent;
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

            if (allMatches.Count == 0)
                return null;

            //First priority: items where the name matches the search query exactly CASE SENSITIVE
            List<MPObject> matchingItems = allMatches.Where(p => Utilities.StringsEqual(searchQuery, p.Name, false)).ToList();
            if (matchingItems.Any())
                return FilterByPopularity(matchingItems);

            //Second priority: items where the name matches the search query exactly (but case-insensitive)
            matchingItems = allMatches.Where(p => Utilities.StringsEqual(searchQuery, p.Name)).ToList();
            if (matchingItems.Any())
                return FilterByPopularity(matchingItems);

            //Third priority: items where the name matches the FILTERED (no symbols or spaces, case insensitive) search query exactly
            matchingItems = allMatches.Where(p => Utilities.StringsEqual(Utilities.FilterStringForMatch(searchQuery), p.NameForMatch)).ToList();
            if (matchingItems.Any())
                return FilterByPopularity(matchingItems);

            //[IN THE FUTURE]Fourth priority: items with a levenshtein distance less than 3

            //Fifth priority: items where the name contains the search query CASE SENSITIVE
            matchingItems = allMatches.Where(p => Utilities.StringContains(p.Name, searchQuery, false)).ToList();
            if (matchingItems.Any())
                return FilterByPopularity(matchingItems);

            //Sixth priority: items where the name contains the search query (case-insensitive)
            matchingItems = allMatches.Where(p => Utilities.StringContains(p.Name, searchQuery)).ToList();
            if (matchingItems.Any())
                return FilterByPopularity(matchingItems);

            //Seventh priority: items where the name contains the FITLERED search query (case-insensitive)
            matchingItems = allMatches.Where(p => Utilities.StringContains(p.NameForMatch, Utilities.FilterStringForMatch(searchQuery))).ToList();
            if (matchingItems.Any())
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

                Console.WriteLine($"\tFiltering based on popularity (result has popularity {popularityPercentDiff}% higher than next closest)");

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

                Console.WriteLine($"\tFiltering based on popularity (result has popularity {popularityPercentDiff}% higher than next closest)");

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
                if (allowDestAreaMatch && Utilities.StringContains(destArea.NameForMatch, input))
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
                if (Utilities.StringContains(subDestArea.NameForMatch, input))
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
                if (Utilities.StringContains(route.NameForMatch, input))
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
                Area matchingParent = result.Parents.ToList().FirstOrDefault(p => Utilities.StringContains(p.NameForMatch, location)) as Area;
                if (matchingParent != null)
                    results.Add(result, matchingParent);
            }

            return results;
        }
        #endregion Helper Methods
    }
}
