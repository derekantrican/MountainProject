
namespace MountainProjectAPI
{
    public class Dimension
    {
        public enum Units
        {
            Feet,
            Meters
        }

        public Dimension()
        {

        }

        public Dimension(double value, Units units)
        {
            this.Value = value;
            this.CurrentUnits = units;
        }

        public double Value { get; set; }
        public Units CurrentUnits { get; set; }

        public double GetValue(Units requestedUnits)
        {
            if (requestedUnits == CurrentUnits)
                return Value;
            else if (requestedUnits == Units.Feet && CurrentUnits == Units.Meters)
                return Value * 3.28084;
            else if (requestedUnits == Units.Meters && CurrentUnits == Units.Feet)
                return Value * 0.3048;

            return 0;
        }
    }
}
