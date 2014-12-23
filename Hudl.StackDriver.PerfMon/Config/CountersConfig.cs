using System.Collections.Generic;

namespace Hudl.StackDriver.PerfMon.Config
{
    public class CountersConfig
    {
        public IList<CounterConfig> Counters { get; set; }
        public string InstanceId { get; set; }
        public string ApiKey { get; set; }
        public string AwsSecretKey { get; set; }
        public string AwsAccessKey { get; set; }
        public string ConfigS3Bucket { get; set; }
        public string ConfigS3Key { get; set; }
    }
}