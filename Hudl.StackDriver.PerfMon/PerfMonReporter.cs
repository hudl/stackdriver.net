using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Timers;
using Hudl.StackDriver.PerfMon.Config;
using log4net;
using Microsoft.Web.Administration;

namespace Hudl.StackDriver.PerfMon
{
    public class PerfMonReporter
    {
        private IList<CounterConfig> Counters { get; set; }
        private ICounterConfigReader ConfigReader { get; set; }
        private ManagementScope Scope { get; set; }

        private static readonly ILog Log = LogManager.GetLogger(typeof (PerfMonReporter));
        private readonly CustomMetricsPoster _stackDriverPoster;
        private readonly string _instanceId;
        private readonly Timer _timer;

#if DEBUG
        private const int OneMinuteMilliseconds = 500; // make reporting quicker when testing.
#else
        private const int OneMinuteMilliseconds = 60000; //One minute is the minimum time between StackDriver metrics.
#endif

        public PerfMonReporter(IList<CounterConfig> counters, string instanceId, string stackDriverApiKey,
            ICounterConfigReader configReader)
            : this(counters, instanceId, stackDriverApiKey, true)
        {
            ConfigReader = configReader;
            if (configReader != null)
            {
                configReader.ConfigUpdated += OnConfigurationUpdated;
                configReader.TriggerUpdate();
            }

            GetMetrics();
        }

        public void OnConfigurationUpdated(object sender, CounterConfigEventArgs e)
        {
            if (e == null || e.Counters == null) return;
            Log.InfoFormat("Updated Counters. Previous={0} New={1}", Counters == null ? 0 : Counters.Count,
                e.Counters.Count);

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

            _timer = new Timer {Interval = OneMinuteMilliseconds};
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
                    var datapoint = GetDataPoints(counter);
                    if (datapoint != null)
                    {
                        datapoint.ForEach(datapoints.Add);
                    }
                });


                if (datapoints.Any())
                {
                    await _stackDriverPoster.SendBatchMetricsAsync(datapoints);
                }
                else
                {
                    Log.Debug("No metrics were reported during this cycle.");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception in metric timer ", ex);
            }
        }

        private string GetPropertyString(ManagementObject collection, string propertyName)
        {
            // This dumb collection doesn't have any way to check if a property exists. Commence looping 

            // check if it has the property at all, if not return null.
            if (collection.Properties.Cast<PropertyData>().All(property => property.Name != propertyName)) return null;

            // if it does, get the value of the property.
            var value = collection[propertyName];
            if (value is string)
            {
                return value.ToString();
            }
            return null;
        }

        private int? GetPropertyInt(ManagementObject collection, string propertyName)
        {
            // This dumb collection doesn't have any way to check if a property exists. Commence looping 

            // check if it has the property at all, if not return null.
            if (collection.Properties.Cast<PropertyData>().All(property => property.Name != propertyName)) return null;

            // if it does, get the value of the property.
            int intValue;
            var value = collection[propertyName].ToString();
            if (int.TryParse(value, out intValue))
            {
                return intValue;
            }
            return null;
        }

        private List<DataPoint> GetDataPoints(CounterConfig counter)
        {
            if (counter == null) return null;


            var providerName = counter.Provider;
            var categoryName = counter.Category;
            var instance = counter.Instance;
            var counterName = counter.Counter;

            if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(categoryName) ||
                string.IsNullOrWhiteSpace(counterName))
            {
                Log.ErrorFormat("Invalid Counter. Provider={0}, Category={1}, Name={2}", providerName, categoryName,
                    counterName);
                return null;
            }

            var whereClause = string.Empty;
            if (!string.IsNullOrWhiteSpace(counter.Instance))
            {
                whereClause = string.Format(" Where Name Like '{0}'", instance);
            }

            var queryString = string.Format("Select * from Win32_PerfFormattedData_{0}_{1}{2}",
                providerName, categoryName, whereClause);

            var search = new ManagementObjectSearcher(Scope, new ObjectQuery(queryString));
            var dataPoints = new List<DataPoint>();
            try
            {
                var queryResults = search.Get();
                var applicationPoolName = "";
                var results = queryResults.Cast<ManagementObject>();

                try
                {
                    Log.DebugFormat("Retrieved {0} results from '{1}'", results.Count(), queryString);
                }
                catch (ManagementException ex)
                {
                    Log.WarnFormat("Unable to read results of '{0}'", queryString, ex);
                    return null;
                }

                foreach (var result in results)
                {
                    try
                    {
                        var friendlyName = counter.Name;
                        var resultName = GetPropertyString(result, "Name");

                        var value = Convert.ToSingle(result[counterName]);

                        var processId = GetPropertyInt(result, "IDProcess");
                        if (processId.HasValue)
                        {
                            applicationPoolName = GetAppPoolByProcessId(processId);
                        }

                        // Prefer in order of ApplicationName, ResultName, Instance or just use empty string.
                        var instanceName =
                            !string.IsNullOrWhiteSpace(applicationPoolName)
                                ? applicationPoolName
                                : !string.IsNullOrWhiteSpace(resultName)
                                    ? resultName
                                    : !string.IsNullOrWhiteSpace(instance)
                                        ? instance
                                        : string.Empty;

                        Log.DebugFormat("{0}/{1} ({2}): {3}", friendlyName, instanceName,
                            applicationPoolName, value);

                        if (!string.IsNullOrWhiteSpace(instanceName))
                        {
                            friendlyName = string.Concat(friendlyName, " - ", instanceName);
                        }
                        dataPoints.Add(new DataPoint(friendlyName, value, DateTime.UtcNow, _instanceId));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(string.Format("Exception while retrieving metric results. Query: {0}", queryString),
                            ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Exception while polling metrics. Query: {0}", queryString), ex);
            }

            return dataPoints;
        }

        private static string GetAppPoolByProcessId(int? processId)
        {
            // Get the actual process name.
            var serverManager = new ServerManager();
            var applicationPoolCollection = serverManager.ApplicationPools;
            foreach (
                var applicationPool in
                    applicationPoolCollection.Where(
                        applicationPool =>
                            applicationPool.WorkerProcesses.Any(workerProcess => workerProcess.ProcessId == processId)))
            {
                return applicationPool.Name;
            }
            return "";
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