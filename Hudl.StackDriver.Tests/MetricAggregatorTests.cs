using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Hudl.StackDriver.Tests
{
    public class MetricAggregatorTests
    {
        [Fact]
        public void Increment_OneStat_TwoTicks()
        {
            const string metricName = "foo";
            const int count1 = 100;
            const int count2 = 25;

            IEnumerable<DataPoint> points = null;
            var mockPoster = new Mock<MockCustomMetricsPoster>();
            mockPoster
                .Setup(p => p.SendBatchMetricsAsync(It.IsAny<IEnumerable<DataPoint>>()))
                .Callback<IEnumerable<DataPoint>>(c => points = c)
                .Returns(new Task(NoOp));

            var aggregator = new MetricAggregator(mockPoster.Object, 3);
            for (int i = 0; i < count1; i++)
            {
                aggregator.Increment(metricName);
            }

            Thread.Sleep(4000);

            Assert.NotNull(points);
            var dp = points.First();
            Assert.NotNull(dp);
            Assert.Equal(count1, (int)(double)dp.Value);


            for (int i = 0; i < count2; i++)
            {
                aggregator.Increment(metricName);
            }

            Thread.Sleep(4000);

            Assert.NotNull(points);
            dp = points.First();
            Assert.NotNull(dp);
            Assert.Equal(count2, (int)(double)dp.Value);
        }

        [Fact]
        public void IncrementByTwo_OneStat_TwoTicks()
        {
            const string metricName = "foo";
            const int count1 = 100;
            const int count2 = 25;

            IEnumerable<DataPoint> points = null;
            var mockPoster = new Mock<MockCustomMetricsPoster>();
            mockPoster
                .Setup(p => p.SendBatchMetricsAsync(It.IsAny<IEnumerable<DataPoint>>()))
                .Callback<IEnumerable<DataPoint>>(c => points = c)
                .Returns(new Task(NoOp));

            var aggregator = new MetricAggregator(mockPoster.Object, 3);
            for (int i = 0; i < count1; i++)
            {
                aggregator.Increment(metricName, 2);
            }

            Thread.Sleep(4000);

            Assert.NotNull(points);
            var dp = points.First();
            Assert.NotNull(dp);
            Assert.Equal(count1 * 2, (int)(double)dp.Value);

            for (int i = 0; i < count2; i++)
            {
                aggregator.Increment(metricName, 2);
            }

            Thread.Sleep(4000);

            Assert.NotNull(points);
            dp = points.First();
            Assert.NotNull(dp);
            Assert.Equal(count2 * 2, (int)(double)dp.Value);
        }

        [Fact]
        public void IncrementByTwo_TwoStats_TwoTicks()
        {
            const string metricName1 = "foo";
            const string metricName2 = "bar";
            const string metricName3 = "zap";
            const int count1 = 100;
            const long count2 = 2500L;
            const long count3 = 10;

            IEnumerable<DataPoint> points = null;
            var mockPoster = new Mock<MockCustomMetricsPoster>();
            mockPoster
                .Setup(p => p.SendBatchMetricsAsync(It.IsAny<IEnumerable<DataPoint>>()))
                .Callback<IEnumerable<DataPoint>>(c => points = c)
                .Returns(new Task(NoOp));

            var aggregator = new MetricAggregator(mockPoster.Object, 3);
            var sw = Stopwatch.StartNew();
            aggregator.SetupMetric(metricName3, MetricType.Total);
            for (int i = 0; i < count1; i++)
            {
                aggregator.Increment(metricName1, 2);
                aggregator.Add(metricName2, count2);
                aggregator.Add(metricName3, count3);
            }
            sw.Stop();
            Thread.Sleep(3100 - (int)sw.ElapsedMilliseconds);

            Assert.NotNull(points);
            Assert.Equal(count1 * 2, (int)(double)points.Single(dp => dp.Name == metricName1).Value);
            Assert.Equal(count2, (double)points.Single(dp => dp.Name == metricName2).Value);
            var val = points.Single(dp => dp.Name == metricName3);
            Assert.Equal(count3 * count1, (double)points.Single(dp => dp.Name == metricName3).Value);
        }

        private void NoOp()
        {
            
        }
    }

    public class MockCustomMetricsPoster : CustomMetricsPoster
    {
        public MockCustomMetricsPoster() : base("ABC123")
        {
            
        }
    }
}
