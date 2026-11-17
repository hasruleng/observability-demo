using System.Diagnostics;
using System.Net.Http.Json;
using Demo.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args).AddDemoTelemetry("order-api");
builder.Services.AddHttpClient();

var app = builder.Build();
var workerBaseUrl = builder.Configuration["WORKER_URL"] ?? "http://localhost:8081";

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "order-api" }));

app.MapPost("/orders", async Task<IResult> (
    OrderRequest request,
    IHttpClientFactory clients,
    CancellationToken cancellationToken) =>
{
    if (request.ItemCount is < 1 or > 50)
    {
        return Results.BadRequest("ItemCount must be between 1 and 50.");
    }

    var orderId = Guid.NewGuid();
    using var response = await clients.CreateClient().PostAsJsonAsync(
        $"{workerBaseUrl.TrimEnd('/')}/work",
        new WorkRequest(orderId, request.ItemCount),
        cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem(
            detail: "The worker could not accept the order.",
            statusCode: StatusCodes.Status502BadGateway);
    }

    return Results.Accepted(
        $"/orders/{orderId}",
        new OrderAccepted(orderId, Activity.Current?.TraceId.ToString()));
});

app.MapGet("/orders/{orderId:guid}", async Task<IResult> (
    Guid orderId,
    IHttpClientFactory clients,
    CancellationToken cancellationToken) =>
{
    using var response = await clients.CreateClient().GetAsync(
        $"{workerBaseUrl.TrimEnd('/')}/work/{orderId}",
        cancellationToken);

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.NotFound();
    }

    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem(
            detail: "The worker could not return order status.",
            statusCode: StatusCodes.Status502BadGateway);
    }

    var status = await response.Content.ReadFromJsonAsync<OrderStatus>(
        cancellationToken: cancellationToken);
    return status is null ? Results.Problem("The worker returned an empty status.") : Results.Ok(status);
});

app.Run();

sealed record OrderRequest(int ItemCount);
sealed record WorkRequest(Guid OrderId, int ItemCount);
sealed record OrderAccepted(Guid OrderId, string? TraceId);
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
