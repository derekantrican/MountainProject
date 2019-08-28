using System;
using System.Collections.Generic;
using System.Linq;
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
            this.Value = value;

            if (rectify)
                this.Value = RectifyGradeValue(system, value);
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
        public string Value { get;set; }
        [XmlIgnore]
        public string BaseValue //Todo: note that in some cases there are multiple base values (eg 5.10-11)
        {
            get
            {
                if (!string.IsNullOrEmpty(RangeStart))
                    return Regex.Replace(RangeStart, @"[a-dA-D\/\\\-+]", "");
                else
                    return Regex.Replace(Value, @"[a-dA-D\/\\\-+]", "");
            }
        }
        public string RangeStart { get; set; }
        public string RangeEnd { get; set; }
        [XmlIgnore]
        public bool IsRange { get { return !string.IsNullOrEmpty(RangeStart) || !string.IsNullOrEmpty(RangeEnd); } }

        /// <summary>
        /// Extracts climbing grades from a string. Only supports YDS and Hueco grades
        /// </summary>
        /// <param name="input">The string to parse</param>
        /// <param name="allowHeadlessMatch">Whether or not to match standalone number (eg "10") as YDS (eg "5.10")</param>
        /// <returns>A list of grades extracted</returns>
        public static List<Grade> ParseString(string input, bool allowHeadlessMatch = true)
        {
            //https://regex101.com/r/5ZS6EE/4
            List<Grade> result = new List<Grade>();

            //FIRST, attempt to match grade ranges with "5." or "V" on both ends of range (eg 5.10-5.11 or V2-V3)
            Regex ratingRegex = new Regex(@"5\.\d+[a-dA-D]?[\/\\\-]5\.\d+[a-dA-D]?|[vV]\d+[\/\\\-][vV]\d+");
            foreach (Match possibleGradeRange in ratingRegex.Matches(input))
            {
                string matchedGradeRange = possibleGradeRange.Value;
                Grade parsedGrade = null;
                if (matchedGradeRange.ToLower().Contains("v"))
                    parsedGrade = new Grade(GradeSystem.Hueco, matchedGradeRange);
                else
                    parsedGrade = new Grade(GradeSystem.YDS, matchedGradeRange);

                if (Regex.IsMatch(matchedGradeRange, @"[\/\\]"))
                {
                    string[] rangeParts = Regex.Split(matchedGradeRange, @"[\/\\]");
                    parsedGrade.RangeStart = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[0]);
                    parsedGrade.RangeEnd = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[1]);

                    parsedGrade.RectifyRange();
                }
                else if (Regex.IsMatch(matchedGradeRange, @"-(?=.)"))
                {
                    string[] rangeParts = matchedGradeRange.Split('-');
                    parsedGrade.RangeStart = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[0]);
                    parsedGrade.RangeEnd = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[1]);

                    parsedGrade.RectifyRange();
                }

                if (parsedGrade != null && !result.Any(g => g.Equals(parsedGrade)))
                    result.Add(parsedGrade);

                input = input.Replace(matchedGradeRange, "");
            }

            //SECOND, attempt to match other grade ranges (eg 5.10-11, 5.11a/b, V7-/+, etc)
            ratingRegex = new Regex(@"5\.\d+[a-dA-D]?[\/\\\-](\d+|[a-dA-D])|5\.\d+(-[\/\\]\+|\+[\/\\]-)|[vV]\d+[\/\\\-]\d+|[vV]\d+(-[\/\\]\+|\+[\/\\]-)");
            foreach (Match possibleGradeRange in ratingRegex.Matches(input))
            {
                string matchedGradeRange = possibleGradeRange.Value;
                Grade parsedGrade = null;
                if (matchedGradeRange.ToLower().Contains("v"))
                    parsedGrade = new Grade(GradeSystem.Hueco, matchedGradeRange);
                else
                    parsedGrade = new Grade(GradeSystem.YDS, matchedGradeRange);

                if (Regex.IsMatch(matchedGradeRange, @"[\/\\]"))
                {
                    string[] rangeParts = Regex.Split(matchedGradeRange, @"[\/\\]");
                    parsedGrade.RangeStart = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[0]);
                    parsedGrade.RangeEnd = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[1]);

                    parsedGrade.RectifyRange();
                }
                else if (Regex.IsMatch(matchedGradeRange, @"-(?=.)"))
                {
                    string[] rangeParts = matchedGradeRange.Split('-');
                    parsedGrade.RangeStart = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[0]);
                    parsedGrade.RangeEnd = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[1]);

                    parsedGrade.RectifyRange();
                }

                if (parsedGrade != null && !result.Any(g => g.Equals(parsedGrade)))
                    result.Add(parsedGrade);

                input = input.Replace(matchedGradeRange, "");
            }


            //THIRD, attempt to match remaining grades with a "5." or "V" prefix
            ratingRegex = new Regex(@"5\.\d+[+-]?[a-dA-D]?|[vV]\d+[+-]?");
            foreach (Match possibleGrade in ratingRegex.Matches(input))
            {
                string matchedGrade = possibleGrade.Value;
                Grade parsedGrade = null;
                if (matchedGrade.ToLower().Contains("v"))
                    parsedGrade = new Grade(GradeSystem.Hueco, matchedGrade);
                else
                    parsedGrade = new Grade(GradeSystem.YDS, matchedGrade);

                if (parsedGrade != null && !result.Any(g => g.Equals(parsedGrade)))
                    result.Add(parsedGrade);

                input = input.Replace(matchedGrade, "");
            }

            if (allowHeadlessMatch)
            {
                //FOURTH, attempt to match YDS grades with no prefix, but with a subgrade (eg 10b or 9+)
                //For numbers below 10, if it is followed by a letter grade we won't match it (likely a French grade)
                ratingRegex = new Regex(@"\d+[\/\\\-]\d+|\d+[a-dA-D][\/\\\-][a-dA-D]|\d+(-[\/\\]\+|\+[\/\\]-)|\d{2,}[a-dA-D]|\d+[+-]");
                foreach (Match possibleGrade in ratingRegex.Matches(input))
                {
                    string matchedGrade = possibleGrade.Value;
                    Grade parsedGrade = new Grade(GradeSystem.YDS, matchedGrade);

                    if (Regex.IsMatch(matchedGrade, @"[\/\\]"))
                    {
                        string[] rangeParts = Regex.Split(matchedGrade, @"[\/\\]");
                        parsedGrade.RangeStart = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[0]);
                        parsedGrade.RangeEnd = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[1]);

                        parsedGrade.RectifyRange();
                    }
                    else if (Regex.IsMatch(matchedGrade, @"-(?=.)"))
                    {
                        string[] rangeParts = matchedGrade.Split('-');
                        parsedGrade.RangeStart = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[0]);
                        parsedGrade.RangeEnd = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[1]);

                        parsedGrade.RectifyRange();
                    }

                    if (parsedGrade != null && !result.Any(g => g.Equals(parsedGrade)))
                        result.Add(parsedGrade);

                    input = input.Replace(matchedGrade, "");
                }

                if (result.Count == 0)
                {
                    //FIFTH, attempt to match any remaining numbers as a grade (eg 10) that don't have "pitch" around
                    ratingRegex = new Regex(@"(?<!pitch\s+)\d+(?!\s+pitch)");
                    foreach (Match possibleGrade in ratingRegex.Matches(input))
                    {
                        string matchedGrade = possibleGrade.Value;
                        Grade parsedGrade = new Grade(GradeSystem.YDS, matchedGrade);

                        if (parsedGrade != null && !result.Any(g => g.Equals(parsedGrade)))
                            result.Add(parsedGrade);

                        input = input.Replace(matchedGrade, "");
                    }
                }
            }

            return result;
        }

        public string RectifyGradeValue(GradeSystem system, string gradeValue)
        {
            //If gradeValue only consists of a subgrade (eg "a" or "+")
            if (Regex.Match(gradeValue, @"[a-dA-D+\-]").Value == gradeValue)
                gradeValue = BaseValue + gradeValue;

            if (system == GradeSystem.Hueco)
            {
                gradeValue = gradeValue.ToUpper();

                if (!gradeValue.Contains("V"))
                    gradeValue = "V" + gradeValue;
            }
            else if (system == GradeSystem.YDS)
            {
                gradeValue = gradeValue.ToLower();

                if (!gradeValue.Contains("5."))
                    gradeValue = "5." + gradeValue;
            }

            return gradeValue;
        }

        public void RectifyRange()
        {
            string startSub = RangeStart.Replace(BaseValue, "");
            string endSub = RangeEnd.Replace(BaseValue, "");

            if (startSub == "" || endSub == "") //In the cases where one end IS the base value (eg 5.10-5.11)
                return;
            else if (startSub == "-" && endSub == "+")
                return;
            else if (endSub == "-" && startSub == "+")
            {
                RangeStart = BaseValue + "-";
                RangeEnd = BaseValue + "+";
            }
            else if (endSub[0] < startSub[0]) //Compare letters by char value
            {
                RangeStart = BaseValue + endSub;
                RangeEnd = BaseValue + startSub;
            }
        }

        public List<string> GetRangeValues()
        {
            if (!IsRange)
                return new List<string> { Value };
            else
            {
                List<string> result = new List<string>();

                string startSub = RangeStart.Replace(BaseValue, "");
                string endSub = RangeEnd.Replace(BaseValue, "");
                if (startSub == "-")
                {
                    result.Add(RangeStart);
                    result.Add(BaseValue);
                    result.Add(RangeEnd);
                }
                else
                {
                    for (char c = Convert.ToChar(startSub); c <= Convert.ToChar(endSub); c++)
                        result.Add(BaseValue + c);
                }

                return result;
            }
        }

        public bool Equals(Grade otherGrade, bool allowRange = false, bool allowBaseOnlyMatch = false)
        {
            if (this.System != otherGrade.System)
                return false;

            if (this.Value == otherGrade.Value)
                return true;

            //Do range matching (eg "5.11a/b" or "5.11a-c" should both match "5.11b")
            if (allowRange && this.GetRangeValues().Contains(otherGrade.Value))
                return true;

            //Do base-only matching (eg 5.10, 5.10a, 5.10+, 5.10b/c should all match "5.10")
            if (allowBaseOnlyMatch && this.BaseValue == Regex.Replace(otherGrade.Value, @"[a-d][\/\\\-][a-d]|\d+[\/\\\-]\d+", ""))
                return true;
            //{
            //    //string baseGrade = Regex.Match(otherGrade.Value, @"5\.\d+|[vV]\d+").Value; //Todo: this is correct, but fails for fontainbleau types
            //    string baseGrade = Regex.Replace(otherGrade.Value, @"[a-d][\/\\\-][a-d]|\d+[\/\\\-]\d+", ""); //Todo: this is wrong
            //    if (this.Value.Contains(baseGrade))
            //        return true;
            //}

            return false;
        }

        public string ToString(bool withSystem = true)
        {
            if (withSystem)
            {
                if (IsRange)
                {
                    if (RangeStart.Contains("-")) //5.9-/+
                        return $"{RangeStart}/{RangeEnd} ({System})";
                    else
                        return $"{RangeStart}-{RangeEnd} ({System})";
                }
                else
                    return $"{Value} ({System})";
            }
            else
            {
                if (IsRange)
                {
                    if (RangeStart.Contains("-")) //5.9-/+
                        return $"{RangeStart}/{RangeEnd}";
                    else
                        return $"{RangeStart}-{RangeEnd}";
                }
                else
                    return Value;
            }
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
