using System.Collections.Generic;

namespace Hudl.StackDriver.PerfMon
{
    class CountersConfig
    {
        public IList<CounterConfig> Counters { get; set; }
        public string InstanceId { get; set; }
        public string ApiKey { get; set; }
    }
}