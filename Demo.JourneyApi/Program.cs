using System.Diagnostics;
using System.Net.Http.Json;
using Demo.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args).AddDemoTelemetry("journey-api");
builder.Services.AddHttpClient();

var app = builder.Build();
var workerBaseUrl = builder.Configuration["WORKER_URL"] ?? "http://localhost:8081";

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "journey-api" }));

app.MapPost("/journeys", async Task<IResult> (
    JourneyRequest request,
    IHttpClientFactory clients,
    CancellationToken cancellationToken) =>
{
    if (request.ItemCount is < 1 or > 50)
    {
        return Results.BadRequest("ItemCount must be between 1 and 50.");
    }

    var journeyId = Guid.NewGuid();
    using var response = await clients.CreateClient().PostAsJsonAsync(
        $"{workerBaseUrl.TrimEnd('/')}/work",
        new WorkRequest(journeyId, request.ItemCount),
        cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem(
            detail: "The worker could not accept the journey.",
            statusCode: StatusCodes.Status502BadGateway);
    }

    return Results.Accepted(
        $"/journeys/{journeyId}",
        new JourneyAccepted(journeyId, Activity.Current?.TraceId.ToString()));
});

app.MapGet("/journeys/{journeyId:guid}", async Task<IResult> (
    Guid journeyId,
    IHttpClientFactory clients,
    CancellationToken cancellationToken) =>
{
    using var response = await clients.CreateClient().GetAsync(
        $"{workerBaseUrl.TrimEnd('/')}/work/{journeyId}",
        cancellationToken);

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.NotFound();
    }

    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem(
            detail: "The worker could not return journey status.",
            statusCode: StatusCodes.Status502BadGateway);
    }

    var status = await response.Content.ReadFromJsonAsync<JourneyStatus>(
        cancellationToken: cancellationToken);
    return status is null ? Results.Problem("The worker returned an empty status.") : Results.Ok(status);
});

app.Run();

sealed record JourneyRequest(int ItemCount);
sealed record WorkRequest(Guid JourneyId, int ItemCount);
sealed record JourneyAccepted(Guid JourneyId, string? TraceId);
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
