using System;
using System.IO;
using System.Text;

namespace Base
{
    public class ConsoleHelper
    {
        public static object ConsoleLock = new object();

        public static void Write(string text, ConsoleColor? color = null)
        {
            lock (ConsoleLock)
            {
                if (color.HasValue)
                {
                    Console.ForegroundColor = color.Value;
                }

                Console.WriteLine(text);
                Console.ResetColor();
            }
        }

        public static void RecordProgress(double progress, TimeSpan estTimeRemaining)
        {
            string text = $"{progress * 100:0.00}% complete. Estimated time remaining: {Math.Floor(estTimeRemaining.TotalHours)} hours, {estTimeRemaining.Minutes} min";

            Write(text, ConsoleColor.Green);

            if (Console.Title != $"MountainProjectDBBuilder - {text}")
            {
                Console.Title = $"MountainProjectDBBuilder - {text}";
            }
        }

        public static void WriteToAdditionalTarget(TextWriter additionalTarget)
        {
            Console.SetOut(new OutputCapture(additionalTarget));
        }

        private class OutputCapture : TextWriter
        {
            private readonly TextWriter stdOutWriter;
            private readonly TextWriter captured;
            public override Encoding Encoding { get { return Encoding.ASCII; } }

            public OutputCapture(TextWriter additionalTarget)
            {
                this.stdOutWriter = Console.Out;
                captured = additionalTarget;
            }

            public override void Write(string output)
            {
                captured.Write(output);
                stdOutWriter.Write(output);
            }

            public override void WriteLine(string output)
            {
                captured.WriteLine(output);
                stdOutWriter.WriteLine(output);
            }
        }
    }
}
