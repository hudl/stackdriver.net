using System;
using Amazon.EC2.Util;
using Hudl.StackDriver.PerfMon.Config;
using log4net;

namespace Hudl.StackDriver.PerfMon
{
    public class PerfMonAgentFactory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (PerfMonAgentFactory));

        public static PerfMonReporter CreateAgentWithConfiguration(CountersConfig config)
        {
            var instanceId = config.InstanceId;
            if (string.IsNullOrWhiteSpace(config.InstanceId))
            {
                // get from meta-data service
                try
                {
                    var awsInstanceId = EC2Metadata.InstanceId;
                    if (!string.IsNullOrWhiteSpace(awsInstanceId))
                    {
                        instanceId = awsInstanceId;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("No Instance Id supplied and unable to retrieve one from AWS.", ex);
                }
            }

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                Log.Error("No Instance Id supplied, nothing to send metrics to.");
                return null;
            }

            var stackdriverApiKey = config.ApiKey;

            if (string.IsNullOrWhiteSpace(config.ConfigS3Bucket) || string.IsNullOrWhiteSpace(config.ConfigS3Key))
            {
                // Not using s3 configuration, lets see if they've actually got counters;
                if (config.Counters == null || config.Counters.Count == 0)
                {
                    throw new Exception("Configuration is empty.");
                }
                return new PerfMonReporter(config.Counters, instanceId, stackdriverApiKey);
            }

            var s3ConfigReader = new S3CounterConfigReader(15, config.ConfigS3Bucket, config.ConfigS3Key,
                config.AwsAccessKey, config.AwsSecretKey);

            return new PerfMonReporter(config.Counters, instanceId, stackdriverApiKey, s3ConfigReader);
        }
    }
}