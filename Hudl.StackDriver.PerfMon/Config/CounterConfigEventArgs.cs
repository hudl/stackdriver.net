using System;
using System.Collections.Generic;

namespace Hudl.StackDriver.PerfMon.Config
{
    public class CounterConfigEventArgs : EventArgs
    {
        public CounterConfigEventArgs(IList<CounterConfig> counters)
        {
            Counters = counters;
        }

        public IList<CounterConfig> Counters { get; private set; }
    }
}