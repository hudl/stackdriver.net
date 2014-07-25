using System;
using System.Text;

namespace Hudl.StackDriver
{
    public sealed class DataPoint
    {
        private readonly string _name;
        private readonly object _value;
        private readonly long _collectedAt;
        private readonly string _instanceId;

        public DataPoint(string name, object value, DateTime collectedAt, string instanceId = null)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            _name = name;
            _value = value;
            _collectedAt = (long)collectedAt.Subtract(CustomMetricsMessage.EpochTime).TotalSeconds;
            _instanceId = instanceId;
        }

        public string ToJson()
        {
            var sb = new StringBuilder("{\"name\":\"");
            sb.Append(_name);
            sb.Append("\", \"value\":");
            sb.Append(_value);
            sb.Append(", \"collected_at\":");
            sb.Append(_collectedAt);
            if (String.IsNullOrWhiteSpace(_instanceId))
            {
                sb.Append("}");
            }
            else
            {
                sb.Append(", \"instance\":\"");
                sb.Append(_instanceId);
                sb.Append("\"}");
            }

            return sb.ToString();
        }
    }
}
