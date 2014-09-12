using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Timers;
using log4net;

namespace Hudl.StackDriver.PerfMon
{
    internal class PerfMonReporter
    {
        private string ServerName { get; set; }
        private IList<CounterConfig> Counters { get; set; }
        private ManagementScope Scope { get; set; }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(PerfMonReporter));
        private readonly CustomMetricsPoster _stackDriverPoster;
        private readonly string _instanceId;
        private readonly Timer _timer;

        private const int OneMinuteMilliseconds = 60000; //One minute is the minimum time between StackDriver metrics.

        public PerfMonReporter(IList<CounterConfig> counters, string instanceId, string stackDriverApiKey)
        {
            var serverName = Environment.MachineName;

            Counters = counters;
            Scope = new ManagementScope(string.Format("\\\\{0}\\root\\cimv2", serverName));

            _stackDriverPoster = new CustomMetricsPoster(stackDriverApiKey, instanceId, new LoggerFailureCallback());
            _instanceId = instanceId;

            _timer = new Timer { Interval = OneMinuteMilliseconds };
            _timer.Elapsed += GetMetrics;

            GetMetrics();
        }

        public void GetMetrics(object sender, ElapsedEventArgs args)
        {
            GetMetrics();
        }

        public async void GetMetrics()
        {
            try
            {
                Scope.Connect();
                var datapoints = new ConcurrentBag<DataPoint>();

                Parallel.ForEach(Counters, counter =>
                {
                    var friendlyName = counter.Name;
                    var providerName = counter.Provider;
                    var categoryName = counter.Category;

                    var counterName = counter.Counter;

                    var whereClause = string.Empty;
                    if (!string.IsNullOrWhiteSpace(counter.Instance))
                    {
                        whereClause = string.Format(" Where Name Like '{0}'", counter.Instance);
                    }

                    var queryString = string.Format("Select Name, {2} from Win32_PerfFormattedData_{0}_{1}{3}",
                        providerName, categoryName, counterName, whereClause);
                    var search = new ManagementObjectSearcher(Scope, new ObjectQuery(queryString));

                    try
                    {
                        var queryResults = search.Get();

                        foreach (var result in queryResults.Cast<ManagementObject>())
                        {
                            try
                            {
                                var value = Convert.ToSingle(result[counterName]);
                                Logger.DebugFormat("{0}/{1}: {2}", ServerName, friendlyName, value);
                                datapoints.Add(new DataPoint(friendlyName, value, DateTime.UtcNow, _instanceId));
                            }
                            catch (Exception ex)
                            {
                                Logger.ErrorFormat(string.Format("Exception while retrieving metric results. Query: {0}", queryString), ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorFormat(string.Format("Exception while polling metrics. Query: {0}", queryString), ex);
                    }
                });
                await _stackDriverPoster.SendBatchMetricsAsync(datapoints);
            }
            catch (Exception ex)
            {
                Logger.Error("Exception in metric timer ", ex);
            }
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }
    }
}