using System;
using System.Text.RegularExpressions;

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
        public string Value { get; set; }

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

            if (this.Value == otherGrade.Value)
                return true;

            //Do range matching (eg "5.11a/b" or "5.11a-c" should both match "5.11b")
            if (allowRange)
            {
                if (Regex.IsMatch(otherGrade.Value, @"[a-d][\/\\\-][a-d]|\d+[\/\\\-]\d+")) //Range of "5.10a-c" is valid for "5.10a, 5.10b, 5.10c". (But don't match instances like 5.9-)
                {
                    string baseGrade = Regex.Replace(otherGrade.Value, @"[a-d][\/\\\-][a-d]|\d+[\/\\\-]\d+", "");
                    char firstSubGrade = Convert.ToChar(Regex.Match(otherGrade.Value, @"[a-d\d+](?=[\/\\\-])").Value);
                    char lastSubGrade = Convert.ToChar(Regex.Match(otherGrade.Value, @"(?<=[\/\\\-])[a-d\d+]").Value);
                    for (char c = firstSubGrade; c <= lastSubGrade; c++)
                    {
                        if (this.Value == baseGrade + c)
                            return true;
                    }
                }
                else if (Regex.IsMatch(otherGrade.Value, @"(-\/\+)|(-\\\+)")) //Range of "V6-/+" is valid for "V6-, V6, V6+"
                {
                    string baseGrade = Regex.Replace(otherGrade.Value, @"(-\/\+)|(-\\\+)", "");
                    string minusGrade = baseGrade + "-";
                    string plusGrade = baseGrade + "+";
                    return this.Value == baseGrade || this.Value == minusGrade || this.Value == plusGrade;
                }
            }

            //Do base-only matching (eg 5.10, 5.10a, 5.10+, 5.10b/c should all match "5.10")
            if (allowBaseOnlyMatch)
            {
                string baseGrade = Regex.Match(otherGrade.Value, @"5\.\d+|[vV]\d+").Value;
                if (this.Value.Contains(baseGrade))
                    return true;
            }

            return false;
        }

        public string ToString(bool withSystem = true)
        {
            if (withSystem)
                return $"{this.Value} ({this.System})";
            else
                return this.Value;
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
