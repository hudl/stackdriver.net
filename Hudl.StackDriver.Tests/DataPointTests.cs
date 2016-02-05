using System;
using Newtonsoft.Json;
using Xunit;
using Newtonsoft.Json.Linq;

namespace Hudl.StackDriver.Tests
{
    public class DataPointTests
    {
        [Fact]
        public static void MissingMetricName_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => new DataPoint(null, 123, DateTime.Now));
            Assert.Throws<ArgumentNullException>(() => new DataPoint("", 123, DateTime.Now));
        }

        [Fact]
        public void ValidDataPoint_ProducesValidJson()
        {
            var name = "foo";
            var value = 1923;
            var instance = "i-349da92";
            var collectedAt = new DateTime(2012, 1, 2);
            var collectedAtEpochSeconds = (long) collectedAt.Subtract(CustomMetricsMessage.EpochTime).TotalSeconds;
            var dp = new DataPoint(name, value, collectedAt, instance);

            var json = dp.ToJson();
            Assert.NotNull(json);

            var deserialized = (JObject)JsonConvert.DeserializeObject(json);
            Assert.Equal(name, deserialized["name"].Value<string>());
            Assert.Equal(value, deserialized["value"].Value<int>());
            Assert.Equal(instance, deserialized["instance"].Value<string>());
            Assert.Equal(collectedAtEpochSeconds, deserialized["collected_at"].Value<long>());
        }
    }
}
