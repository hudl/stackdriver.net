using System;

namespace Hudl.StackDriver.PerfMon.Config
{
    public interface ICounterConfigReader
    {
        CountersConfig Config { get; }
        event EventHandler<CounterConfigEventArgs> ConfigUpdated;
        void TriggerUpdate();
    }
}