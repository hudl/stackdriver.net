using System.Net;
using log4net;
using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace Hudl.StackDriver
{
    public class CustomMetricsPoster
    {
        private const string DefaultEndpointUrl = "https://custom-gateway.stackdriver.com/v1/custom";

        private static readonly ILog Log = LogManager.GetLogger(typeof(CustomMetricsPoster));

        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler
            {
                UseProxy = false,
            });

        private readonly string _apiKey;
        private readonly string _instanceId;

        public CustomMetricsPoster(string apiKey, string instanceId = null)
        {
            if (String.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentNullException("apiKey");
            }

            _apiKey = apiKey;
            _instanceId = instanceId;
        }

        public void SendMetric(string name, object value, DateTime? collectedAt = null, string instanceId = null)
        {
            SendMetricAsync(name, value, collectedAt, instanceId).RunSynchronously();
        }

        public async Task SendMetricAsync(string name, object value, DateTime? collectedAt = null, string instanceId = null)
        {
            var sendCollectedAt = collectedAt.HasValue ? collectedAt.Value : DateTime.UtcNow;
            var sendInstanceId = instanceId ?? _instanceId;

            var msg = new CustomMetricsMessage(new DataPoint(name, value, sendCollectedAt, sendInstanceId));
            var result = await Client.PostAsync(DefaultEndpointUrl, PrepareContent(msg.ToJson())).ConfigureAwait(false);

            if (result.StatusCode != HttpStatusCode.Created)
            {
                // the normal response is a 201. For any other response code, log the code and the response body (which will hopefully say 
                // what StackDriver didn't like with the request.
                var body = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.WarnFormat("Sent metric. Name={0}, StatusCode={1}, Body={2}", name, result.StatusCode, body);
            }
        }

        public void SendBatchMetrics(IEnumerable<DataPoint> dataPoints)
        {
            SendBatchMetricsAsync(dataPoints).RunSynchronously();
        }

        public async Task SendBatchMetricsAsync(IEnumerable<DataPoint> dataPoints)
        {
            var msg = new CustomMetricsMessage(dataPoints);
            var result = await Client.PostAsync(DefaultEndpointUrl, PrepareContent(msg.ToJson())).ConfigureAwait(false);

            if (result.StatusCode != HttpStatusCode.Created)
            {
                // the normal response is a 201. For any other response code, log the code and the response body (which will hopefully say 
                // what StackDriver didn't like with the request.
                var body = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.WarnFormat("Sent metrics batch. StatusCode={0}, Body={1}", result.StatusCode, body);
            }
        }

        private HttpContent PrepareContent(string json)
        {
            var content = new StringContent(json, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.Add("x-stackdriver-apikey", _apiKey);
            return content;
        }
    }
}
