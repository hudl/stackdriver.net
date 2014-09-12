using System;

namespace Hudl.StackDriver.PerfMon
{
    class PerfMonAgentFactory
    {
        public PerfMonReporter CreateAgentWithConfiguration(CountersConfig config)
        {
            if (config.Counters == null || config.Counters.Count == 0)
            {
                throw new Exception("Configuration is empty.");
            }
            // TODO - Get Instance Id from aws meta-data
            var instanceId = config.InstanceId;

            var stackdriverApiKey = config.ApiKey;

            return new PerfMonReporter(config.Counters, instanceId, stackdriverApiKey);
        }
    }
}