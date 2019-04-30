using MountainProjectModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MountainProjectBot
{
    public static class MountainProjectDataSearch
    {
        public static List<Area> DestAreas = new List<Area>();

        public static void InitMountainProjectData(string xmlPath)
        {
            Console.WriteLine("Deserializing info from MountainProject");

            FileStream fileStream = new FileStream(xmlPath, FileMode.Open);
            XmlSerializer xmlDeserializer = new XmlSerializer(typeof(List<Area>));
            DestAreas = (List<Area>)xmlDeserializer.Deserialize(fileStream);

            if (DestAreas.Count == 0)
            {
                Console.WriteLine("Problem deserializing MountainProject info");
                Environment.Exit(13); //Invalid data
            }

            Console.WriteLine("MountainProject Info deserialized successfully");
        }

        public static string SearchMountainProject(string searchText)
        {
            Console.WriteLine("Getting info from MountainProject");
            Stopwatch searchStopwatch = Stopwatch.StartNew();

            MPObject result = DeepSearch(searchText, DestAreas);
            if (result == null)
                return null;

            Console.WriteLine($"Info retrieved from MountainProject (found in {searchStopwatch.ElapsedMilliseconds} ms)");

            return GetFormattedString(result);
        }

        private static string GetFormattedString(MPObject inputMountainProjectObject)
        {
            string result = "I found the following info:\n\n";

            if (inputMountainProjectObject is Area)
            {
                Area inputArea = inputMountainProjectObject as Area;
                result += $"{inputArea.Name} [{inputArea.Statistics}]\n" +
                         inputArea.URL;

                //Todo: additional info to add
                // - located in {destArea}
                // - popular routes
            }
            else if (inputMountainProjectObject is Route)
            {
                Route inputRoute = inputMountainProjectObject as Route;
                result += $"{inputRoute.Name} [{inputRoute.Type} {inputRoute.Grade},";

                if (!string.IsNullOrEmpty(inputRoute.AdditionalInfo))
                    result += " " + inputRoute.AdditionalInfo;

                result += "]\n";
                result += inputRoute.URL;

                //Todo: additional info to add
                // - located in {destArea}
                // - # of bolts (if sport)
            }

            return result;
        }

        private static MPObject DeepSearch(string input, List<Area> destAreas)
        {
            MPObject matchedObject = null;
            foreach (Area destArea in destAreas)
            {
                if (input.ToLower().Contains(destArea.Name.ToLower()))
                {
                    //If we're matching the name of a destArea (eg a State), we'll assume that the route/area is within that state
                    //(eg routes named "Sweet Home Alabama"). So instead of returning the destArea, we'll return a search on the
                    //state's subareas
                    matchedObject = SearchSubAreasForMatch(input, destArea.SubAreas);
                    if (matchedObject != null)
                        return matchedObject;
                }

                if (destArea.SubAreas != null &&
                    destArea.SubAreas.Count() > 0)
                {
                    matchedObject = SearchSubAreasForMatch(input, destArea.SubAreas);
                    if (matchedObject != null)
                        return matchedObject;
                }
            }

            return matchedObject;
        }

        private static MPObject SearchSubAreasForMatch(string input, List<Area> subAreas)
        {
            MPObject matchedObject = null;

            foreach (Area subDestArea in subAreas)
            {
                if (input.Equals(subDestArea.Name, StringComparison.InvariantCultureIgnoreCase))
                    return subDestArea;

                if (subDestArea.SubAreas != null &&
                    subDestArea.SubAreas.Count() > 0)
                {
                    matchedObject = SearchSubAreasForMatch(input, subDestArea.SubAreas);
                    if (matchedObject != null)
                        return matchedObject;
                }

                if (subDestArea.Routes != null &&
                    subDestArea.Routes.Count() > 0)
                {
                    matchedObject = SearchRoutes(input, subDestArea.Routes);
                    if (matchedObject != null)
                        return matchedObject;
                }
            }

            return matchedObject;
        }

        private static MPObject SearchRoutes(string input, List<Route> routes)
        {
            MPObject matchedObject = null;

            foreach (Route route in routes)
            {
                if (input.Equals(route.Name, StringComparison.InvariantCultureIgnoreCase))
                    return route;
            }

            return matchedObject;
        }
    }
}
