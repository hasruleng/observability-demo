using Demo.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args).AddDemoTelemetry("dependency");
builder.Services.AddSingleton<ScenarioState>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "dependency" }));

app.MapPost("/scenario", (ScenarioRequest request, ScenarioState scenario) =>
{
    if (!scenario.TrySet(request.Name))
    {
        return Results.BadRequest("Scenario must be healthy, slow, or failing.");
    }

    return Results.Ok(new { scenario = scenario.Current });
});

app.MapPost("/process", async Task<IResult> (
    ProcessRequest request,
    ScenarioState scenario,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (request.ItemCount is < 1 or > 50)
    {
        return Results.BadRequest("ItemCount must be between 1 and 50.");
    }

    var currentScenario = scenario.Current;
    var delayMilliseconds = currentScenario switch
    {
        "healthy" => 75,
        "slow" => 350 + (request.ItemCount * 100),
        _ => 0
    };

    if (delayMilliseconds > 0)
    {
        await Task.Delay(delayMilliseconds, cancellationToken);
    }

    logger.LogInformation(
        "Dependency processed {ItemCount} items using the {Scenario} scenario",
        request.ItemCount,
        currentScenario);

    if (currentScenario == "failing")
    {
        logger.LogWarning("Dependency rejected the synthetic journey with {ItemCount} items", request.ItemCount);
        return Results.Problem(
            detail: "Synthetic dependency failure.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new ProcessResponse(
        request.ItemCount,
        currentScenario,
        DateTimeOffset.UtcNow));
});

app.Run();

sealed record ScenarioRequest(string Name);
sealed record ProcessRequest(int ItemCount);
sealed record ProcessResponse(int ItemCount, string Scenario, DateTimeOffset ProcessedAt);

sealed class ScenarioState
{
    private string _current = "healthy";

    public string Current => Volatile.Read(ref _current);

    public bool TrySet(string name)
    {
        if (name is not ("healthy" or "slow" or "failing"))
        {
            return false;
        }

        Interlocked.Exchange(ref _current, name);
        return true;
    }
}
