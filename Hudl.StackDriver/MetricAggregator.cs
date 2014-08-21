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
    /// </summary>
    public class MetricAggregator
    {
        private readonly ConcurrentDictionary<string, MetricCounter> _counters = new ConcurrentDictionary<string, MetricCounter>();
        private readonly CustomMetricsPoster _customMetricsPoster;
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable - this is important so the Timer does not get garbage collected
        private readonly Timer _timer;

        public string InstanceId
        {
            get { return _customMetricsPoster.InstanceId; }
            set { _customMetricsPoster.InstanceId = value; }
        }

        public MetricAggregator(string apiKey, string instanceId = null, CustomMetricsPoster.IFailureCallback failureCallback = null)
            : this(new CustomMetricsPoster(apiKey, instanceId, failureCallback), 60)
        {
        }

        public MetricAggregator(CustomMetricsPoster poster, int seconds)
        {
            if (poster == null)
            {
                throw new ArgumentNullException("poster");
            }

            _customMetricsPoster = poster;

            _timer = new Timer
                {
                    Interval = TimeSpan.FromSeconds(seconds).TotalMilliseconds,
                };
            _timer.Elapsed += Tick;
            _timer.Start();
        }

        public void Increment(string metric)
        {
            var counter = _counters.GetOrAdd(metric, m => new MetricCounter(m, MetricType.Total));
            counter.Increment(1);
        }

        public void Increment(string metric, int incrementBy)
        {
            if (incrementBy < 0)
            {
                throw new ArgumentException("incrementBy cannot be negative (" + incrementBy + ")");
            }

            var counter = _counters.GetOrAdd(metric, m => new MetricCounter(m, MetricType.Total));
            counter.Increment(incrementBy);
        }

        public void Add(string metric, long amount)
        {
            var counter = _counters.GetOrAdd(metric, m => new MetricCounter(m, MetricType.Average));
            counter.Add(amount);
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

                return new DataPoint(mc.Name, value, snapshot, InstanceId);
            }).ToList();

            _customMetricsPoster.SendBatchMetricsAsync(dataPoints).Wait();
        }

        private sealed class MetricCounter
        {
            private readonly object _lock = new object();
            private readonly string _name;
            private readonly MetricType _metricType;
            private DateTime _startedAt;
            private int _count;
            private long _sum;

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
                _sum = 0;
            }

            public void Increment(int incrementBy)
            {
                lock (_lock)
                {
                    _sum += incrementBy;
                    _count++;
                }
            }

            public void Add(long amount)
            {
                lock (_lock)
                {
                    _sum += amount;
                    _count++;
                }
            }

            public long FreezeAndReset(out DateTime startedAt)
            {
                lock (_lock)
                {
                    startedAt = _startedAt;
                    var snapshotCount = _count;
                    var snapshotSum = _sum;

                    _count = 0;
                    _sum = 0;
                    _startedAt = DateTime.UtcNow;

                    if (_metricType == MetricType.Average)
                    {
                        return snapshotCount > 0
                            ? (long) Math.Round((double) snapshotSum/snapshotCount, 0)
                            : 0L;
                    }
                    return snapshotSum;
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
