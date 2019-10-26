using System;
using System.Collections.Generic;
using System.Globalization;
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
                //Todo: right now we filter out french grades, but maybe we can support them in the future

                //FOURTH, attempt to match YDS grades with no prefix, but with a subgrade (eg 10b or 9+)
                //For numbers below 10, if it is followed by a letter grade we won't match it (likely a French grade)
                string regexString = @"(?<!\\|\/|\d)\d+[\/\\\-]\d+(?!\\|\/|\d)|" + //First format option: 5/7 (where there isn't a date - other slashes or a partial date)
                                     @"\d+[a-dA-D][\/\\\-][a-dA-D]|" +             //Second format option: 6d/7a
                                     @"\d+(-[\/\\]\+|\+[\/\\]-)|" +                //Third format option: variations on 6-/+ or 6+/-
                                     @"\d{2,}[a-dA-D]|" +                          //Fourth format option: 10a (but not single digits, as YDS uses -/+ below 10 but French uses a-d below 10)
                                     @"\d+[+-]";                                   //Fifth format option: 9- or 9+

                ratingRegex = new Regex(regexString);
                foreach (Match possibleGrade in ratingRegex.Matches(input))
                {
                    string matchedGrade = possibleGrade.Value;
                    Grade parsedGrade = new Grade(GradeSystem.YDS, matchedGrade);

                    if (Regex.IsMatch(matchedGrade, @"[\/\\]"))
                    {
                        string[] rangeParts = Regex.Split(matchedGrade, @"[\/\\]");
                        parsedGrade.RangeStart = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[0]);
                        parsedGrade.RangeEnd = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[1]);

                        //Make sure range end isn't bigger than range start (eg 12/7 - which is a date)
                        if (int.TryParse(rangeParts[0], out _) && int.TryParse(rangeParts[1], out _) &&
                            Convert.ToInt32(rangeParts[0]) > Convert.ToInt32(rangeParts[1]))
                        {
                            continue;
                        }

                        parsedGrade.RectifyRange();
                    }
                    else if (Regex.IsMatch(matchedGrade, @"-(?=.)"))
                    {
                        string[] rangeParts = matchedGrade.Split('-');
                        parsedGrade.RangeStart = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[0]);
                        parsedGrade.RangeEnd = parsedGrade.RectifyGradeValue(parsedGrade.System, rangeParts[1]);

                        //Make sure range end isn't bigger than range start (eg 12/7 - which is a date)
                        if (int.TryParse(rangeParts[0], out _) && int.TryParse(rangeParts[1], out _) &&
                            Convert.ToInt32(rangeParts[0]) > Convert.ToInt32(rangeParts[1]))
                        {
                            continue;
                        }

                        parsedGrade.RectifyRange();
                    }

                    if (parsedGrade != null && !result.Any(g => g.Equals(parsedGrade)))
                        result.Add(parsedGrade);

                    input = input.Replace(matchedGrade, "");
                }

                if (result.Count == 0)
                {
                    //FIFTH, attempt to match any remaining numbers as a grade (eg 10) that don't have certain words around
                    string[] dontMatchBefore = { };                                                //Don't match these REGEX before
                    string[] dontMatchAfter = { "st", "rd", "nd", "th", "m", "ft" };               //Don't match these REGEX after
                    string[] dontMatchBeforeOrAfter = { "pitch", "hour", "day", "month", "year" }; //Don't match these WORDS before or after

                    //Add month names and month abbreviations to the "dontMatchBefore" array
                    string[] monthNames = new CultureInfo("en-US").DateTimeFormat.MonthGenitiveNames.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                    string[] monthAbbr = new CultureInfo("en-US").DateTimeFormat.AbbreviatedMonthNames.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                    dontMatchBefore = dontMatchBefore.Concat(monthNames.Select(m => { return m + @"\s+"; })).Concat(monthAbbr.Select(m => { return m + @"\s+"; })).ToArray();

                    //Expand dontMatchBefore and dontMatchAfter arrays to include dontMatchBeforeOrAfter
                    dontMatchBefore = dontMatchBefore.Concat(dontMatchBeforeOrAfter.Select(w => { return w + @"\s+"; })).ToArray();
                    dontMatchAfter = dontMatchAfter.Concat(dontMatchBeforeOrAfter.Select(w => { return @"\s+" + w; })).ToArray();

                    regexString = $@"(?<!{string.Join(@"|", dontMatchBefore)})" + //Exclude matches with dontMatchWords BEFORE number
                                   @"(?<=\s)\d+" +                                //Match a number
                                   @"(?![a-dA-D\/\\]" +                           //Exclude matches where it is immediately followed by some characters
                                  $@"|{string.Join(@"|", dontMatchAfter)}" +      //Exclude matches with dontMatchWords AFTER number
                                   @"|\d)";                                       //Make sure the match isn't part of an unmatched number (eg "30th" doesn't match "3")

                    foreach (Match possibleGrade in Regex.Matches(input, regexString, RegexOptions.IgnoreCase))
                    {
                        string matchedGrade = possibleGrade.Value; //Todo: need to remove values above 20
                        if (Convert.ToInt32(matchedGrade) > 20) //Don't allow numbers above 20
                            continue;

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

                int rangeStart = Convert.ToInt32(Regex.Match(RangeStart, @"\d+").Value);
                int rangeEnd = Convert.ToInt32(Regex.Match(RangeEnd, @"\d+").Value);
                for (int i = rangeStart; i <= rangeEnd; i++)
                {
                    if (startSub == "" || endSub == "")
                    {
                        if (System == GradeSystem.Hueco)
                            result.Add($"V{i}");
                        else if (System == GradeSystem.YDS)
                            result.Add($"5.{i}");
                    }
                    else if (startSub == "-")
                    {
                        if (System == GradeSystem.Hueco)
                        {
                            result.Add($"V{i}-");
                            result.Add($"V{i}");
                            result.Add($"V{i}+");
                        }
                        else if (System == GradeSystem.YDS)
                        {
                            result.Add($"5.{i}-");
                            result.Add($"5.{i}");
                            result.Add($"5.{i}+");
                        }
                    }
                    else
                    {
                        char startChar = i == rangeStart ? startSub[0] : 'a';
                        char endChar = i == rangeEnd ? endSub[0] : 'd';
                        for (char c = startChar; c <= endChar; c++)
                        {
                            if (System == GradeSystem.Hueco)
                                result.Add($"V{i}{c}");
                            else if (System == GradeSystem.YDS)
                                result.Add($"5.{i}{c}");
                        }
                    }
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
            if (allowRange && this.GetRangeValues().Intersect(otherGrade.GetRangeValues()).Any())
                return true;

            //Do base-only matching (eg 5.10, 5.10a, 5.10+, 5.10b/c should all match "5.10")
            if (allowBaseOnlyMatch && this.BaseValue == otherGrade.BaseValue)
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
                    string rangeStart = RangeStart;
                    string rangeEnd = RangeEnd;
                    if (rangeEnd.Contains(BaseValue))
                        rangeEnd = rangeEnd.Replace(BaseValue, "");

                    if (rangeStart.Contains("-")) //5.9-/+
                        return $"{rangeStart}/{rangeEnd} ({System})";
                    else
                        return $"{rangeStart}-{rangeEnd} ({System})";
                }
                else
                    return $"{Value} ({System})";
            }
            else
            {
                if (IsRange)
                {
                    string rangeStart = RangeStart;
                    string rangeEnd = RangeEnd;
                    if (rangeEnd.Contains(BaseValue))
                        rangeEnd = rangeEnd.Replace(BaseValue, "");

                    if (rangeStart.Contains("-")) //5.9-/+
                        return $"{rangeStart}/{rangeEnd}";
                    else
                        return $"{rangeStart}-{rangeEnd}";
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
