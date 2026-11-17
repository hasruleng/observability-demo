using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Threading.Channels;
using Demo.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args).AddDemoTelemetry("order-worker");
builder.Services.AddHttpClient();
builder.Services.AddSingleton<OrderStore>();
builder.Services.AddSingleton<WorkerMetrics>();
builder.Services.AddSingleton(_ => Channel.CreateBounded<QueuedOrder>(new BoundedChannelOptions(64)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true,
    SingleWriter = false
}));
builder.Services.AddHostedService<OrderProcessor>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "order-worker" }));

app.MapPost("/work", async Task<IResult> (
    WorkRequest request,
    Channel<QueuedOrder> queue,
    OrderStore orders,
    WorkerMetrics metrics,
    CancellationToken cancellationToken) =>
{
    if (request.ItemCount is < 1 or > 50)
    {
        return Results.BadRequest("ItemCount must be between 1 and 50.");
    }

    var submittedAt = DateTimeOffset.UtcNow;
    orders.Add(request.OrderId, request.ItemCount, submittedAt);
    var queuedOrder = new QueuedOrder(
        request.OrderId,
        request.ItemCount,
        submittedAt,
        Activity.Current?.Context ?? default);

    metrics.OnEnqueued();
    if (!queue.Writer.TryWrite(queuedOrder))
    {
        metrics.OnDequeued();
        orders.SetStatus(request.OrderId, "failed", "The worker queue is closed.");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Accepted($"/work/{request.OrderId}", new
    {
        request.OrderId,
        status = "pending"
    });
});

app.MapGet("/work/{orderId:guid}", (Guid orderId, OrderStore orders) =>
    orders.TryGet(orderId, out var order) ? Results.Ok(order) : Results.NotFound());

app.Run();

sealed record WorkRequest(Guid OrderId, int ItemCount);
sealed record QueuedOrder(Guid OrderId, int ItemCount, DateTimeOffset SubmittedAt, ActivityContext ParentContext);

sealed record OrderStatus(
    Guid OrderId,
    int ItemCount,
    string Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? FinishedAt,
    string? Error)
{
    public double ElapsedMilliseconds =>
        Math.Round(((FinishedAt ?? DateTimeOffset.UtcNow) - SubmittedAt).TotalMilliseconds, 1);
}

sealed class OrderStore
{
    private readonly ConcurrentDictionary<Guid, OrderStatus> _orders = new();

    public void Add(Guid orderId, int itemCount, DateTimeOffset submittedAt) =>
        _orders[orderId] = new OrderStatus(
            orderId,
            itemCount,
            "pending",
            submittedAt,
            null,
            null);

    public bool TryGet(Guid orderId, out OrderStatus order) =>
        _orders.TryGetValue(orderId, out order!);

    public void SetStatus(Guid orderId, string status, string? error = null)
    {
        if (_orders.TryGetValue(orderId, out var order))
        {
            _orders[orderId] = order with
            {
                Status = status,
                FinishedAt = DateTimeOffset.UtcNow,
                Error = error
            };
        }
    }
}

sealed class WorkerMetrics : IDisposable
{
    private readonly Meter _meter = new("Demo.Worker");
    private long _queueDepth;
    private long _activeWorkers;

    public WorkerMetrics()
    {
        _meter.CreateObservableGauge<long>(
            "demo.worker.queue.depth",
            ObserveQueueDepth,
            "orders",
            "Orders waiting in the worker queue.");
        _meter.CreateObservableGauge<long>(
            "demo.worker.active",
            ObserveActiveWorkers,
            "workers",
            "Orders currently being processed.");
    }

    public void OnEnqueued() => Interlocked.Increment(ref _queueDepth);

    public void OnDequeued() => Interlocked.Decrement(ref _queueDepth);

    public void OnStarted() => Interlocked.Increment(ref _activeWorkers);

    public void OnFinished() => Interlocked.Decrement(ref _activeWorkers);

    public long ObserveQueueDepth() => Interlocked.Read(ref _queueDepth);

    private long ObserveActiveWorkers() => Interlocked.Read(ref _activeWorkers);

    public void Dispose() => _meter.Dispose();
}

sealed class OrderProcessor(
    Channel<QueuedOrder> queue,
    OrderStore orders,
    WorkerMetrics metrics,
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<OrderProcessor> logger) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("Demo.Worker");
    private readonly string _fulfillmentBaseUrl =
        configuration["FULFILLMENT_URL"] ?? "http://localhost:8082";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var order in queue.Reader.ReadAllAsync(stoppingToken))
        {
            metrics.OnDequeued();
            metrics.OnStarted();
            using var activity = ActivitySource.StartActivity(
                "order.process",
                ActivityKind.Consumer,
                order.ParentContext);
            activity?.SetTag("order.id", order.OrderId);
            activity?.SetTag("item.count", order.ItemCount);

            try
            {
                using var response = await clients.CreateClient().PostAsJsonAsync(
                    $"{_fulfillmentBaseUrl.TrimEnd('/')}/process",
                    new FulfillmentRequest(order.ItemCount),
                    stoppingToken);

                if (!response.IsSuccessStatusCode)
                {
                    var details = await response.Content.ReadAsStringAsync(stoppingToken);
                    throw new HttpRequestException(
                        $"Fulfillment returned {(int)response.StatusCode}: {details}",
                        null,
                        response.StatusCode);
                }

                orders.SetStatus(order.OrderId, "completed");
                OrderTelemetry.RecordCompletion(
                    (DateTimeOffset.UtcNow - order.SubmittedAt).TotalMilliseconds,
                    "completed");
                logger.LogInformation(
                    "Order {OrderId} completed with {ItemCount} items",
                    order.OrderId,
                    order.ItemCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                orders.SetStatus(order.OrderId, "failed", exception.Message);
                OrderTelemetry.RecordCompletion(
                    (DateTimeOffset.UtcNow - order.SubmittedAt).TotalMilliseconds,
                    "failed");
                logger.LogError(
                    exception,
                    "Order {OrderId} failed while processing {ItemCount} items",
                    order.OrderId,
                    order.ItemCount);
            }
            finally
            {
                metrics.OnFinished();
            }
        }
    }
}

sealed record FulfillmentRequest(int ItemCount);
