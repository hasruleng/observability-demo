using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Threading.Channels;
using Demo.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args).AddDemoTelemetry("worker");
builder.Services.AddHttpClient();
builder.Services.AddSingleton<JourneyStore>();
builder.Services.AddSingleton<WorkerMetrics>();
builder.Services.AddSingleton(_ => Channel.CreateBounded<QueuedJourney>(new BoundedChannelOptions(64)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true,
    SingleWriter = false
}));
builder.Services.AddHostedService<JourneyProcessor>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "worker" }));

app.MapPost("/work", async Task<IResult> (
    WorkRequest request,
    Channel<QueuedJourney> queue,
    JourneyStore journeys,
    WorkerMetrics metrics,
    CancellationToken cancellationToken) =>
{
    if (request.ItemCount is < 1 or > 50)
    {
        return Results.BadRequest("ItemCount must be between 1 and 50.");
    }

    var submittedAt = DateTimeOffset.UtcNow;
    journeys.Add(request.JourneyId, request.ItemCount, submittedAt);
    var queuedJourney = new QueuedJourney(
        request.JourneyId,
        request.ItemCount,
        submittedAt,
        Activity.Current?.Context ?? default);

    metrics.OnEnqueued();
    if (!queue.Writer.TryWrite(queuedJourney))
    {
        metrics.OnDequeued();
        journeys.SetStatus(request.JourneyId, "failed", "The worker queue is closed.");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Accepted($"/work/{request.JourneyId}", new
    {
        request.JourneyId,
        status = "pending"
    });
});

app.MapGet("/work/{journeyId:guid}", (Guid journeyId, JourneyStore journeys) =>
    journeys.TryGet(journeyId, out var journey) ? Results.Ok(journey) : Results.NotFound());

app.Run();

sealed record WorkRequest(Guid JourneyId, int ItemCount);
sealed record QueuedJourney(Guid JourneyId, int ItemCount, DateTimeOffset SubmittedAt, ActivityContext ParentContext);

sealed record JourneyStatus(
    Guid JourneyId,
    int ItemCount,
    string Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? FinishedAt,
    string? Error)
{
    public double ElapsedMilliseconds =>
        Math.Round(((FinishedAt ?? DateTimeOffset.UtcNow) - SubmittedAt).TotalMilliseconds, 1);
}

sealed class JourneyStore
{
    private readonly ConcurrentDictionary<Guid, JourneyStatus> _journeys = new();

    public void Add(Guid journeyId, int itemCount, DateTimeOffset submittedAt) =>
        _journeys[journeyId] = new JourneyStatus(
            journeyId,
            itemCount,
            "pending",
            submittedAt,
            null,
            null);

    public bool TryGet(Guid journeyId, out JourneyStatus journey) =>
        _journeys.TryGetValue(journeyId, out journey!);

    public void SetStatus(Guid journeyId, string status, string? error = null)
    {
        if (_journeys.TryGetValue(journeyId, out var journey))
        {
            _journeys[journeyId] = journey with
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
            "journeys",
            "Journeys waiting in the worker queue.");
        _meter.CreateObservableGauge<long>(
            "demo.worker.active",
            ObserveActiveWorkers,
            "workers",
            "Journeys currently being processed.");
    }

    public void OnEnqueued() => Interlocked.Increment(ref _queueDepth);

    public void OnDequeued() => Interlocked.Decrement(ref _queueDepth);

    public void OnStarted() => Interlocked.Increment(ref _activeWorkers);

    public void OnFinished() => Interlocked.Decrement(ref _activeWorkers);

    public long ObserveQueueDepth() => Interlocked.Read(ref _queueDepth);

    private long ObserveActiveWorkers() => Interlocked.Read(ref _activeWorkers);

    public void Dispose() => _meter.Dispose();
}

sealed class JourneyProcessor(
    Channel<QueuedJourney> queue,
    JourneyStore journeys,
    WorkerMetrics metrics,
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<JourneyProcessor> logger) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("Demo.Worker");
    private readonly string _dependencyBaseUrl =
        configuration["DEPENDENCY_URL"] ?? "http://localhost:8082";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var journey in queue.Reader.ReadAllAsync(stoppingToken))
        {
            metrics.OnDequeued();
            metrics.OnStarted();
            using var activity = ActivitySource.StartActivity(
                "journey.process",
                ActivityKind.Consumer,
                journey.ParentContext);
            activity?.SetTag("journey.id", journey.JourneyId);
            activity?.SetTag("item.count", journey.ItemCount);

            try
            {
                using var response = await clients.CreateClient().PostAsJsonAsync(
                    $"{_dependencyBaseUrl.TrimEnd('/')}/process",
                    new DependencyRequest(journey.ItemCount),
                    stoppingToken);

                if (!response.IsSuccessStatusCode)
                {
                    var details = await response.Content.ReadAsStringAsync(stoppingToken);
                    throw new HttpRequestException(
                        $"Dependency returned {(int)response.StatusCode}: {details}",
                        null,
                        response.StatusCode);
                }

                journeys.SetStatus(journey.JourneyId, "completed");
                JourneyTelemetry.RecordCompletion(
                    (DateTimeOffset.UtcNow - journey.SubmittedAt).TotalMilliseconds,
                    "completed");
                logger.LogInformation(
                    "Journey {JourneyId} completed with {ItemCount} items",
                    journey.JourneyId,
                    journey.ItemCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                journeys.SetStatus(journey.JourneyId, "failed", exception.Message);
                JourneyTelemetry.RecordCompletion(
                    (DateTimeOffset.UtcNow - journey.SubmittedAt).TotalMilliseconds,
                    "failed");
                logger.LogError(
                    exception,
                    "Journey {JourneyId} failed while processing {ItemCount} items",
                    journey.JourneyId,
                    journey.ItemCount);
            }
            finally
            {
                metrics.OnFinished();
            }
        }
    }
}

sealed record DependencyRequest(int ItemCount);
