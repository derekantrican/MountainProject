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

        public static List<MPObject> SearchMountainProject(string searchText, SearchParameters parameters = null)
        {
            Console.WriteLine("Getting info from MountainProject");
            Stopwatch searchStopwatch = Stopwatch.StartNew();

            searchText = Utilities.FilterStringForMatch(searchText);

            List<MPObject> results = new List<MPObject>();
            if (parameters != null && !string.IsNullOrEmpty(parameters.SpecificLocation))
            {
                List<MPObject> locationMatches = DeepSearch(Utilities.FilterStringForMatch(parameters.SpecificLocation), DestAreas, parameters);
                locationMatches.RemoveAll(p => p is Route);
                Area location = FilterByPopularity(locationMatches) as Area;

                results = SearchSubAreasForMatch(searchText, location.SubAreas, parameters);
            }
            else
                results = DeepSearch(searchText, DestAreas, parameters);


            Console.WriteLine($"Found {results.Count} matching results from MountainProject in {searchStopwatch.ElapsedMilliseconds} ms");

            return results;
        }

        public static SearchParameters ParseParameters(ref string input)
        {
            SearchParameters parameters = new SearchParameters();
            if (Regex.IsMatch(input, "-area", RegexOptions.IgnoreCase))
            {
                parameters.OnlyAreas = true;
                input = Regex.Replace(input, "-area", "", RegexOptions.IgnoreCase);
            }

            if (Regex.IsMatch(input, "-route", RegexOptions.IgnoreCase))
            {
                parameters.OnlyRoutes = true;
                input = Regex.Replace(input, "-route", "", RegexOptions.IgnoreCase);
            }

            if (Regex.IsMatch(input, "-location", RegexOptions.IgnoreCase))
            {
                parameters.SpecificLocation = Regex.Match(input, @"-location:([^-\n]*)", RegexOptions.IgnoreCase).Groups[1].Value;
                input = Regex.Replace(input, @"-location:[^-\n]*", "", RegexOptions.IgnoreCase);
            }

            return parameters;
        }

        public static MPObject FilterByPopularity(List<MPObject> listToFilter)
        {
            if (listToFilter.Count == 1)
                return listToFilter.First();
            if (listToFilter.Count >= 2)
            {
                List<MPObject> matchedObjectsByPopularity = listToFilter.OrderByDescending(p => p.Popularity).ToList();

                int pop1 = matchedObjectsByPopularity[0].Popularity;
                int pop2 = matchedObjectsByPopularity[1].Popularity;
                double popularityPercentDiff = Math.Round((double)(pop1 - pop2) / pop2 * 100, 2);

                Console.WriteLine($"Filtering based on priority (result has priority {popularityPercentDiff}% higher than next closest)");

                return matchedObjectsByPopularity.First(); //Return the most popular matched object (this may prioritize areas over routes. May want to check that later)
            }
            else
                return null;
        }

        public static List<MPObject> DeepSearch(string input, List<Area> destAreas, SearchParameters parameters = null)
        {
            List<MPObject> matchedObjects = new List<MPObject>();
            foreach (Area destArea in destAreas)
            {
                //If we're matching the name of a destArea (eg a State), we'll assume that the route/area is within that state
                //(eg routes named "Sweet Home Alabama") instead of considering a match on the destArea ie: Utilities.StringMatch(input, destArea.Name)

                List<MPObject> matchedSubAreas = SearchSubAreasForMatch(input, destArea.SubAreas, parameters);
                matchedObjects.AddRange(matchedSubAreas);
            }

            return matchedObjects;
        }

        private static List<MPObject> SearchSubAreasForMatch(string input, List<Area> subAreas, SearchParameters parameters = null)
        {
            List<MPObject> matchedObjects = new List<MPObject>();

            foreach (Area subDestArea in subAreas)
            {
                if (StringMatch(input, subDestArea.NameForMatch) &&
                    (parameters != null && !parameters.OnlyRoutes))
                {
                    matchedObjects.Add(subDestArea);
                }

                if (subDestArea.SubAreas != null &&
                    subDestArea.SubAreas.Count() > 0)
                {
                    List<MPObject> matchedSubAreas = SearchSubAreasForMatch(input, subDestArea.SubAreas, parameters);
                    matchedObjects.AddRange(matchedSubAreas);
                }

                if (subDestArea.Routes != null &&
                    subDestArea.Routes.Count() > 0)
                {
                    List<MPObject> matchedRoutes = SearchRoutes(input, subDestArea.Routes, parameters);
                    matchedObjects.AddRange(matchedRoutes);
                }
            }

            return matchedObjects;
        }

        private static List<MPObject> SearchRoutes(string input, List<Route> routes, SearchParameters parameters = null)
        {
            List<MPObject> matchedObjects = new List<MPObject>();

            foreach (Route route in routes)
            {
                if (StringMatch(input, route.NameForMatch) &&
                    (parameters != null && !parameters.OnlyAreas))
                {
                    matchedObjects.Add(route);
                }
            }

            return matchedObjects;
        }

        public static bool StringMatch(string inputString, string targetString, bool caseInsensitive = true)
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
