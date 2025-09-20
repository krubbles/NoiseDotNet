using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpNoise
{
    public class Profiler
    {
        public static string ProfileRelative(string name, int profilingTimeMS, Action baseline, Action toProfile)
        {
            for (int i = 0; i < 20; ++i)
            {
                baseline();
                toProfile();
            }
            long baselineTicks = 0;
            long toProfileTicks = 0;
            System.Diagnostics.Stopwatch totalTime = new();
            totalTime.Start();
            System.Diagnostics.Stopwatch sampler = new();
            while (totalTime.ElapsedMilliseconds < profilingTimeMS)
            {
                sampler.Restart();
                baseline();
                baselineTicks += sampler.ElapsedTicks;
                sampler.Restart();
                toProfile();
                toProfileTicks += sampler.ElapsedTicks;
            }
            return $"{name}: {toProfileTicks / (float)baselineTicks}";
        }
    }
}
