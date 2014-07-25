using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Hudl.StackDriver
{
    /// <summary>
    /// StackDriver only wants to receive custom metrics once per minute. This is a helper class for accumulating counts and then reporting them automatically.
    /// 
    /// Exposing a singleton instance of this somewhere in your app makes sense. Then call Increment(name) anywhere to start tracking metrics. Every 60 seconds
    /// the metrics will automatically be sent to StackDriver in a single batch call. You can customize whether you want to just sent the metric count or a 
    /// per-second value by calling SetupMetric(name, MetricType.PerSecond) once (typically on app startup).
    /// 
    /// Verbose=true will log the StackDriver API calls. It's helpful when metrics don't seem to be making it up to StackDriver. It will log the Http status code
    /// returned by StackDriver as well as the respone body.
    /// </summary>
    public class MetricAggregator
    {
        private readonly ConcurrentDictionary<string, MetricCounter> _counters = new ConcurrentDictionary<string, MetricCounter>();
        private readonly CustomMetricsPoster _customMetricsPoster;

        public MetricAggregator(string apiKey, string instanceId)
        {
            _customMetricsPoster = new CustomMetricsPoster(apiKey, instanceId);

            var timer = new Timer
            {
                Interval = TimeSpan.FromSeconds(60).TotalMilliseconds,
            };
            timer.Elapsed += Tick;
            timer.Start();
        }

        public void Increment(string metric)
        {
            var counter = _counters.GetOrAdd(metric, m => new MetricCounter(m, MetricType.Total));
            counter.Increment();
        }

        public void SetupMetric(string metricName, MetricType type)
        {
            _counters.AddOrUpdate(metricName,
                name => new MetricCounter(name, type),
                (name, existingCounter) =>
                {
                    if (existingCounter.MetricType == type)
                    {
                        return existingCounter;
                    }
                    return new MetricCounter(name, type);
                });
        }

        private void Tick(object sender, ElapsedEventArgs e)
        {
            // freeze and reset each stat
            var dataPoints = _counters.Values.Select(mc =>
            {
                DateTime snapshot;
                var count = (double)mc.FreezeAndReset(out snapshot);

                object value;
                switch (mc.MetricType)
                {
                    case MetricType.PerSecond:
                        value = Math.Round(count/(DateTime.UtcNow - snapshot).TotalSeconds, 0);
                        break;

                    default:
                        value = count;
                        break;
                }

                return new DataPoint(mc.Name, value, snapshot);
            }).ToList();

            _customMetricsPoster.SendBatchMetricsAsync(dataPoints).RunSynchronously();
        }

        private sealed class MetricCounter
        {
            private readonly object _lock = new object();
            private readonly string _name;
            private readonly MetricType _metricType;
            private DateTime _startedAt;
            private int _count;

            public string Name
            {
                get { return _name; }
            }

            public MetricType MetricType
            {
                get { return _metricType; }
            }

            public MetricCounter(string name, MetricType type)
            {
                _name = name;
                _metricType = type;
                _startedAt = DateTime.UtcNow;
                _count = 0;
            }

            public void Increment()
            {
                lock (_lock)
                {
                    _count++;
                }
            }

            public int FreezeAndReset(out DateTime startedAt)
            {
                lock (_lock)
                {
                    startedAt = _startedAt;
                    var snapshotCount = _count;

                    _count = 0;
                    _startedAt = DateTime.UtcNow;

                    return snapshotCount;
                }
            }

            public override bool Equals(object obj)
            {
                var mc = obj as MetricCounter;
                if (mc == null)
                {
                    return false;
                }
                return _name.Equals(mc._name);
            }

            public override int GetHashCode()
            {
                return _name.GetHashCode();
            }

            public override string ToString()
            {
                return String.Format("[{0}, {1}]", _name, _count);
            }
        }
    }
}
