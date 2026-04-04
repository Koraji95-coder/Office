using System.Net;
using DailyDesk.Models;
using DailyDesk.Services;

var builder = WebApplication.CreateBuilder(args);

var configuredHost = builder.Configuration["Broker:Host"] ?? OfficeBrokerDefaults.Host;
var configuredPort =
    builder.Configuration.GetValue<int?>("Broker:Port") is { } parsedPort && parsedPort > 0
        ? parsedPort
        : OfficeBrokerDefaults.Port;
if (!IPAddress.TryParse(configuredHost, out var ipAddress))
{
    ipAddress = IPAddress.Loopback;
    configuredHost = OfficeBrokerDefaults.Host;
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(ipAddress, configuredPort);
});

var baseUrl = OfficeBrokerDefaults.BuildBaseUrl(configuredHost, configuredPort);
builder.Services.AddSingleton(
    new OfficeBrokerRuntimeMetadata
    {
        Host = configuredHost,
        Port = configuredPort,
        BaseUrl = baseUrl,
        StartedAt = DateTimeOffset.Now,
        LoopbackOnly = true,
    }
);
builder.Services.AddSingleton<OfficeBrokerOrchestrator>();

var app = builder.Build();
var logger = app.Logger;

app.MapGet("/health", async (OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await orchestrator.GetHealthAsync(ct));
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker health endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Office broker health check failed",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapGet("/api/state", async (OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await orchestrator.GetStateAsync(ct));
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker state endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to build office state",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapGet("/api/chat/threads", async (OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var threads = await orchestrator.GetChatThreadsAsync(ct);
        return Results.Ok(new { threads });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker chat threads endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to load chat threads",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/chat/route", async (ChatRouteRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var route = await orchestrator.SetChatRouteAsync(request.Route, ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { route, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker chat route endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to set chat route",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/chat/send", async (ChatSendRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var message = await orchestrator.SendChatAsync(request.Prompt, request.RouteOverride, ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { message, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker chat send endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to send chat message",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/study/start", async (StudyStartRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var session = await orchestrator.StartStudyAsync(request.Focus, request.Difficulty, request.QuestionCount, ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { session, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker study start endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to start study session",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/study/generate-practice", async (StudyStartRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var practice = await orchestrator.GeneratePracticeAsync(request.Focus, request.Difficulty, request.QuestionCount, ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { practice, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker practice generation endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to generate practice",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/study/score-practice", async (StudyScorePracticeRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var attempt = await orchestrator.ScorePracticeAsync(request.Answers ?? Array.Empty<OfficePracticeAnswerInput>(), ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { attempt, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker practice scoring endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to score practice",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/study/generate-defense", async (StudyGenerateDefenseRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var scenario = await orchestrator.GenerateDefenseAsync(request.Topic, ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { scenario, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker defense generation endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to generate defense",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/study/score-defense", async (StudyScoreDefenseRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var attempt = await orchestrator.ScoreDefenseAsync(request.Answer, ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { attempt, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker defense scoring endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to score defense",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/study/save-reflection", async (StudySaveReflectionRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var reflection = await orchestrator.SaveReflectionAsync(request.Reflection, ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { reflection, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker reflection save endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to save reflection",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/research/run", async (ResearchRunRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var report = await orchestrator.RunResearchAsync(request.Query, request.Perspective, request.SaveToLibrary ?? false, ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { report, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker research run endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to run research",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/research/save", async (ResearchSaveRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var filePath = await orchestrator.SaveLatestResearchAsync(request.Notes, ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { filePath, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker research save endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to save research",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/watchlists/run", async (WatchlistRunRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var report = await orchestrator.RunWatchlistAsync(
            request.WatchlistId,
            request.SaveToLibrary,
            ct
        );
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { report, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker watchlist run endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to run watchlist",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapGet("/api/inbox", async (OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await orchestrator.GetInboxAsync(ct));
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker inbox endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to load inbox",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/inbox/resolve", async (InboxResolveRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var suggestion = await orchestrator.ResolveSuggestionAsync(
            request.SuggestionId,
            request.Status,
            request.Reason,
            request.Note,
            ct
        );
        var inbox = await orchestrator.GetInboxAsync(ct);
        return Results.Ok(new { suggestion, inbox });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker inbox resolve endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to resolve inbox suggestion",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/inbox/queue", async (InboxQueueRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var suggestion = await orchestrator.QueueSuggestionAsync(
            request.SuggestionId,
            request.ApproveFirst ?? false,
            ct
        );
        var inbox = await orchestrator.GetInboxAsync(ct);
        return Results.Ok(new { suggestion, inbox });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker inbox queue endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to queue inbox suggestion",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/library/import", async (LibraryImportRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var result = await orchestrator.ImportLibraryFilesAsync(request.Paths ?? Array.Empty<string>(), ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { result, state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker library import endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to import library files",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/history/reset", async (OfficeHistoryResetRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var state = await orchestrator.ResetLocalHistoryAsync(
            request.ClearTrainingHistory ?? true,
            ct
        );
        return Results.Ok(new { state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker history reset endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to reset Office local history",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/workspace/reset", async (OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var state = await orchestrator.ResetWorkspaceAsync(ct);
        return Results.Ok(new { state });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker workspace reset endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to reset Office workspace",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.Run();

internal sealed record ChatRouteRequest(string Route);
internal sealed record ChatSendRequest(string Prompt, string? RouteOverride);
internal sealed record StudyStartRequest(string? Focus, string? Difficulty, int? QuestionCount);
internal sealed record StudyScorePracticeRequest(IReadOnlyList<OfficePracticeAnswerInput>? Answers);
internal sealed record StudyGenerateDefenseRequest(string? Topic);
internal sealed record StudyScoreDefenseRequest(string Answer);
internal sealed record StudySaveReflectionRequest(string Reflection);
internal sealed record ResearchRunRequest(string Query, string? Perspective, bool? SaveToLibrary);
internal sealed record ResearchSaveRequest(string? Notes);
internal sealed record WatchlistRunRequest(string WatchlistId, bool? SaveToLibrary);
internal sealed record InboxResolveRequest(
    string SuggestionId,
    string Status,
    string? Reason,
    string? Note
);
internal sealed record InboxQueueRequest(string SuggestionId, bool? ApproveFirst);
internal sealed record LibraryImportRequest(IReadOnlyList<string>? Paths);
internal sealed record OfficeHistoryResetRequest(bool? ClearTrainingHistory);
