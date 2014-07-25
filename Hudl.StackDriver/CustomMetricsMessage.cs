using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hudl.StackDriver
{
    public class CustomMetricsMessage
    {
        public static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public const int ProtocolVersion = 1;

        private readonly List<DataPoint> _dataPoints;
        private readonly long _timestamp;

        public CustomMetricsMessage(DataPoint dataPoint)
        {
            if (dataPoint == null)
            {
                throw new ArgumentNullException("dataPoint");
            }

            _dataPoints = new List<DataPoint> { dataPoint };
            _timestamp = (long)DateTime.UtcNow.Subtract(EpochTime).TotalSeconds;
        }

        public CustomMetricsMessage(IEnumerable<DataPoint> dataPoints)
        {
            if (dataPoints == null)
            {
                throw new ArgumentNullException("dataPoints");
            }

            _dataPoints = dataPoints.ToList();
            if (_dataPoints.Count == 0)
            {
                throw new ArgumentException("no dataPoints were found");
            }

            _timestamp = (long)DateTime.UtcNow.Subtract(EpochTime).TotalSeconds;
        }

        public string ToJson()
        {
            var sb = new StringBuilder("{\"timestamp\":");
            sb.Append(_timestamp);
            sb.Append(", \"proto_version\":");
            sb.Append(ProtocolVersion);
            sb.Append(", \"data\":[");
            var isFirst = true;
            foreach (var dp in _dataPoints)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    sb.Append(',');
                }
                sb.Append(dp.ToJson());
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }
}
