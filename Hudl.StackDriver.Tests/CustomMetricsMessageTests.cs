using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Xunit;
using Newtonsoft.Json.Linq;

namespace Hudl.StackDriver.Tests
{
    public class CustomMetricsMessageTests
    {
        [Fact]
        public void MissingDataPoint_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => new CustomMetricsMessage((DataPoint)null));
            Assert.Throws<ArgumentNullException>(() => new CustomMetricsMessage((IEnumerable<DataPoint>)null));
            Assert.Throws<ArgumentException>(() => new CustomMetricsMessage(new List<DataPoint>()));
        }

        [Fact]
        public void ValidMessage_ProducesValidJson()
        {
            var name = "foo";
            var value = 1923;
            var instance = "i-349da92";
            var collectedAt = new DateTime(2012, 1, 2);
            var collectedAtEpochSeconds = (long) collectedAt.Subtract(CustomMetricsMessage.EpochTime).TotalSeconds;
            var dp = new DataPoint(name, value, collectedAt, instance);

            var now = DateTime.UtcNow;
            var msg = new CustomMetricsMessage(dp);

            var json = msg.ToJson();
            Assert.NotNull(json);

            var deserialized = (JObject)JsonConvert.DeserializeObject(json);
            Assert.Equal(CustomMetricsMessage.ProtocolVersion, deserialized["proto_version"].Value<int>());
            Assert.True(deserialized["timestamp"].Value<long>() >= CustomMetricsMessage.EpochTime.Subtract(now).TotalSeconds);

            var pointsArray = deserialized["data"].Value<JArray>();
            Assert.NotNull(pointsArray);
            Assert.Equal(1, pointsArray.Count);

            var data0 = (JObject)pointsArray[0];
            Assert.Equal(name, data0["name"].Value<string>());
            Assert.Equal(value, data0["value"].Value<int>());
            Assert.Equal(instance, data0["instance"].Value<string>());
            Assert.Equal(collectedAtEpochSeconds, data0["collected_at"].Value<long>());
        }
    }
}
