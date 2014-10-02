using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Timers;
using Hudl.StackDriver.PerfMon.Config;
using log4net;

namespace Hudl.StackDriver.PerfMon
{

    public class PerfMonReporter
    {
        private string ServerName { get; set; }
        private IList<CounterConfig> Counters { get; set; }
        private ICounterConfigReader ConfigReader { get; set; }
        private ManagementScope Scope { get; set; }

        private static readonly ILog Log = LogManager.GetLogger(typeof(PerfMonReporter));
        private readonly CustomMetricsPoster _stackDriverPoster;
        private readonly string _instanceId;
        private readonly Timer _timer;

        private const int OneMinuteMilliseconds = 60000; //One minute is the minimum time between StackDriver metrics.

        public PerfMonReporter(IList<CounterConfig> counters, string instanceId, string stackDriverApiKey, ICounterConfigReader configReader)
            : this(counters, instanceId, stackDriverApiKey, true)
        {
            ConfigReader = configReader;
            if (configReader != null)
            {
                configReader.ConfigUpdated += OnConfigurationUpdated;
            }
            GetMetrics();
        }

        public void OnConfigurationUpdated(object sender, CounterConfigEventArgs e)
        {
            if (e == null) return;

            Log.InfoFormat("Updated Counters. Previous={0} New={1}", Counters == null ? 0 : Counters.Count, e.Counters == null ? 0 : e.Counters.Count);

            Counters = e.Counters;
        }

        // init is just used to make the signature of this private constructor different.
        private PerfMonReporter(IList<CounterConfig> counters, string instanceId, string stackDriverApiKey, bool init)
        {
            var serverName = Environment.MachineName;

            Counters = counters;
            Scope = new ManagementScope(string.Format("\\\\{0}\\root\\cimv2", serverName));

            _stackDriverPoster = new CustomMetricsPoster(stackDriverApiKey, instanceId, new LoggerFailureCallback());

            _instanceId = instanceId;

            _timer = new Timer { Interval = OneMinuteMilliseconds };
            _timer.Elapsed += GetMetrics;
        }

        public PerfMonReporter(IList<CounterConfig> counters, string instanceId, string stackDriverApiKey)
            : this(counters, instanceId, stackDriverApiKey, true)
        {
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

                if (Counters == null) return; //no work to do.

                Parallel.ForEach(Counters, counter =>
                {
                    var datapoint = GetDataPoint(counter);
                    if (datapoint != null)
                    {
                        datapoints.Add(datapoint);
                    }
                });

                if (datapoints.Count > 0)
                {
                    await _stackDriverPoster.SendBatchMetricsAsync(datapoints);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception in metric timer ", ex);
            }
        }

        private DataPoint GetDataPoint(CounterConfig counter)
        {

            var friendlyName = counter.Name;
            var providerName = counter.Provider;
            var categoryName = counter.Category;

            var counterName = counter.Counter;

            if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(categoryName) || string.IsNullOrWhiteSpace(counterName))
            {
                Log.ErrorFormat("Invalid Counter. Provider={0}, Category={1}, Name={2}", providerName, categoryName, counterName);
                return null;
            }

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
                        Log.DebugFormat("{0}/{1}: {2}", ServerName, friendlyName, value);
                        return new DataPoint(friendlyName, value, DateTime.UtcNow, _instanceId);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorFormat(string.Format("Exception while retrieving metric results. Query: {0}", queryString), ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorFormat(string.Format("Exception while polling metrics. Query: {0}", queryString), ex);
            }
            return null;
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