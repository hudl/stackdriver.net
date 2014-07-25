stackdriver.net
===============

A C# wrapper for sending custom metrics to StackDriver

## Usage

You can use this one of two ways. The easiest way to do collect basic counting metrics is to use the ```MetricAggregator``` class. The simplest way to use it is to somewhere keep a single instance of it and then call ```Increment("foo.bar");```. Once you instantiate the aggregator, it will publish all collected metrics up to StackDriver every 60 seconds. It will publish the metrics using a single batched request.

```csharp
public const string ApiKey = "CO32DCH55D0KVHL2R6MO434CR46PFSDF";
public static readonly MetricAggregator Aggregator = new MetricAggregator(ApiKey);

// elsewhere in your code...
Aggregator.Increment("logins");
...
Aggregator.Increment("purchase.books");
// etc.
```


Another way of using this is to simply use the ```CustomMetricsPoster``` class. It exposes a synchronous and an async version of both single and batch metric sending.

```csharp
var poster = new CustomMetricsPoster(ApiKey);
poster.SendMetric("purchase.amount", 105.25);
```
