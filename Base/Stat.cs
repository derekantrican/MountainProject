using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Base
{
    public class Stat
    {
        private Stopwatch stopwatch;
        private TimeSpan elapsed = TimeSpan.Zero;

        public string Name;
        public Dictionary<string, TimeSpan> Snapshots = new Dictionary<string, TimeSpan>();

        public static Stat Start(string name)
        {
            return new Stat
            {
                Name = name,
                stopwatch = Stopwatch.StartNew()
            };
        }

        public void Record(string key)
        {
            TimeSpan currentElapsed = stopwatch.Elapsed;

            if (!string.IsNullOrEmpty(key))
            {
                Snapshots[key] = currentElapsed - elapsed;
            }

            elapsed = currentElapsed;
        }
    }
}
