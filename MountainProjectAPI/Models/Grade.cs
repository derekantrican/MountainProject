using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace MountainProjectAPI
{
    public class Grade
    {
        public Grade()
        {

        }

        public Grade(GradeSystem system, string value, bool rectify = true)
        {
            this.System = system;
            this.OriginalValue = value;

            if (rectify)
                this.OriginalValue = RectifyGradeValue(system, value);
        }

        public enum GradeSystem
        {
            YDS,
            French,
            Ewbanks,
            UIAA,
            SouthAfrica,
            Britsh,
            Hueco,
            Fontainebleau,
            Unlabled
        }

        public GradeSystem System { get; set; }
        public string OriginalValue { get; set; }
        public string BaseValue { get; set; }
        public string RangeStart { get; set; }
        public string RangeEnd { get; set; }
        [XmlIgnore]
        public bool IsRange { get { return !string.IsNullOrEmpty(RangeStart) || !string.IsNullOrEmpty(RangeEnd); } }

        public static List<Grade> ParseString(string input)
        {
            List<Grade> result = new List<Grade>();

            //FIRST, attempt to match grades with a "5." or "V" prefix (regex101 for testing: https://regex101.com/r/5ZS6EE/3)
            Regex ratingRegex = new Regex(@"((5\.)\d+[+-]?[a-dA-D]?([\/\\\-][a-dA-D])?)|([vV]\d+[+-]?([\/\\\-]\d+)?)");
            foreach (Match possibleGrade in ratingRegex.Matches(input))
            {
                string matchedGrade = possibleGrade.Value;

                if (matchedGrade.ToLower().Contains("v"))
                {
                    if (result.Find(p => p.System == GradeSystem.Hueco && p.OriginalValue == matchedGrade) == null)
                        result.Add(new Grade(GradeSystem.Hueco, matchedGrade));
                }
                else
                {
                    if (result.Find(p => p.System == GradeSystem.YDS && p.OriginalValue == matchedGrade) == null)
                        result.Add(new Grade(GradeSystem.YDS, matchedGrade));
                }
            }

            //SECOND, attempt to match YDS grades with no prefix, but with a subgrade (eg 10b or 9+)
            ratingRegex = new Regex(@"\d+([+-]|[a-dA-D])([\/\\\-][a-dA-D])?(?!\d)");
            foreach (Match possibleGrade in ratingRegex.Matches(input))
            {
                string matchedGrade = possibleGrade.Value;
                matchedGrade = Grade.RectifyGradeValue(GradeSystem.YDS, matchedGrade);

                if (result.Find(p => p.System == GradeSystem.YDS && p.OriginalValue == matchedGrade) == null)
                    result.Add(new Grade(GradeSystem.YDS, matchedGrade));
            }

            //THIRD, attempt to match any remaining numbers as a grade (eg 10)
            if (result.Count == 0)
            {
                ratingRegex = new Regex(@"(?<=\s|^)\d+(?=\s|$)");
                foreach (Match possibleGrade in ratingRegex.Matches(input))
                {
                    string matchedGrade = possibleGrade.Value;
                    matchedGrade = Grade.RectifyGradeValue(GradeSystem.YDS, matchedGrade);

                    if (result.Find(p => p.System == GradeSystem.YDS && p.OriginalValue == matchedGrade) == null)
                        result.Add(new Grade(GradeSystem.YDS, matchedGrade));
                }
            }

            return result;
        }

        public static string RectifyGradeValue(GradeSystem system, string gradeValue)
        {
            //On MountainProject, Hueco grades are of the form V6-7 and YDS grades are
            //of the form 5.10a/b

            if (system == GradeSystem.Hueco)
            {
                gradeValue = Regex.Replace(gradeValue, @"\\|\/", "-"); //Replace slashes with hyphens
                gradeValue = gradeValue.ToUpper();

                if (!gradeValue.Contains("V"))
                    gradeValue = "V" + gradeValue;
            }
            else if (system == GradeSystem.YDS)
            {
                gradeValue = Regex.Replace(gradeValue, @"((\-)|(\\))(?=[a-dA-D])", "/"); //Replace hyphens and backslashes with forward slashes (but not instances like 5.9-)
                gradeValue = gradeValue.ToLower();

                if (!gradeValue.Contains("5."))
                    gradeValue = "5." + gradeValue;
            }

            return gradeValue;
        }

        public bool Equals(Grade otherGrade, bool allowRange = false, bool allowBaseOnlyMatch = false)
        {
            if (this.System != otherGrade.System)
                return false;

            if (this.OriginalValue == otherGrade.OriginalValue)
                return true;

            //Do range matching (eg "5.11a/b" or "5.11a-c" should both match "5.11b")
            if (allowRange)
            {
                if (Regex.IsMatch(otherGrade.OriginalValue, @"[a-d][\/\\\-][a-d]|\d+[\/\\\-]\d+")) //Range of "5.10a-c" is valid for "5.10a, 5.10b, 5.10c". (But don't match instances like 5.9-)
                {
                    string baseGrade = Regex.Replace(otherGrade.OriginalValue, @"[a-d][\/\\\-][a-d]|\d+[\/\\\-]\d+", "");
                    char firstSubGrade = Convert.ToChar(Regex.Match(otherGrade.OriginalValue, @"[a-d\d+](?=[\/\\\-])").Value);
                    char lastSubGrade = Convert.ToChar(Regex.Match(otherGrade.OriginalValue, @"(?<=[\/\\\-])[a-d\d+]").Value);
                    for (char c = firstSubGrade; c <= lastSubGrade; c++)
                    {
                        if (this.OriginalValue == baseGrade + c)
                            return true;
                    }
                }
                else if (Regex.IsMatch(otherGrade.OriginalValue, @"(-\/\+)|(-\\\+)")) //Range of "V6-/+" is valid for "V6-, V6, V6+"
                {
                    string baseGrade = Regex.Replace(otherGrade.OriginalValue, @"(-\/\+)|(-\\\+)", "");
                    string minusGrade = baseGrade + "-";
                    string plusGrade = baseGrade + "+";
                    return this.OriginalValue == baseGrade || this.OriginalValue == minusGrade || this.OriginalValue == plusGrade;
                }
            }

            //Do base-only matching (eg 5.10, 5.10a, 5.10+, 5.10b/c should all match "5.10")
            if (allowBaseOnlyMatch)
            {
                //string baseGrade = Regex.Match(otherGrade.Value, @"5\.\d+|[vV]\d+").Value; //Todo: this is correct, but fails for fontainbleau types
                string baseGrade = Regex.Replace(otherGrade.OriginalValue, @"[a-d][\/\\\-][a-d]|\d+[\/\\\-]\d+", ""); //Todo: this is wrong
                if (this.OriginalValue.Contains(baseGrade))
                    return true;
            }

            return false;
        }

        public string ToString(bool withSystem = true)
        {
            if (withSystem)
                return $"{this.OriginalValue} ({this.System})";
            else
                return this.OriginalValue;
        }

        #region Overrides
        public override bool Equals(object obj)
        {
            if (obj is Grade)
                return this.Equals(obj as Grade);
            else
                return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return this.ToString();
        }
        #endregion Overrides
    }
}
