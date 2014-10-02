using System.Net;
using log4net;

namespace Hudl.StackDriver.PerfMon
{
    public sealed class LoggerFailureCallback : CustomMetricsPoster.IFailureCallback
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CustomMetricsPoster));

        public void HandleMetricPostFailure(string metricName, HttpStatusCode statusCode, string body)
        {
            if (metricName == null)
            {
                Logger.ErrorFormat("Send metrics batch failed. StatusCode={0}, Body={1}", statusCode, body);
            }
            else
            {
                Logger.ErrorFormat("Send metrics batch failed. StatusCode={0}, Body={1}, MetricName={2}", statusCode, body, metricName);
            }
        }
    }
}