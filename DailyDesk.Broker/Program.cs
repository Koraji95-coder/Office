using System.Net;
using DailyDesk.Broker;
using DailyDesk.Models;
using DailyDesk.Services;
using FluentValidation;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine("State", "logs", "office-broker-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        fileSizeLimitBytes: 10_485_760,
        shared: true)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

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
builder.Services.AddHostedService<OfficeJobWorker>();

var app = builder.Build();
var logger = app.Logger;

app.UseSerilogRequestLogging();

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
    var validation = new ChatRouteRequestValidator().Validate(request);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
    }

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
    var validation = new ChatSendRequestValidator().Validate(request);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
    }

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
    var validation = new StudyScoreDefenseRequestValidator().Validate(request);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
    }

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
    var validation = new StudySaveReflectionRequestValidator().Validate(request);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
    }

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
    var validation = new ResearchRunRequestValidator().Validate(request);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
    }

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
    var validation = new WatchlistRunRequestValidator().Validate(request);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
    }

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
    var validation = new InboxResolveRequestValidator().Validate(request);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
    }

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

app.MapPost("/api/ml/analytics", async (HttpContext httpContext, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    var sync = httpContext.Request.Query["sync"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    if (!sync)
    {
        var job = orchestrator.JobStore.Enqueue(DailyDesk.Models.OfficeJobType.MLAnalytics, "broker");
        return Results.Accepted($"/api/jobs/{job.Id}", new { jobId = job.Id, status = job.Status });
    }

    try
    {
        var analytics = await orchestrator.RunMLAnalyticsAsync(ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { analytics, state });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker ML analytics endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to run ML analytics",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/ml/forecast", async (HttpContext httpContext, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    var sync = httpContext.Request.Query["sync"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    if (!sync)
    {
        var job = orchestrator.JobStore.Enqueue(DailyDesk.Models.OfficeJobType.MLForecast, "broker");
        return Results.Accepted($"/api/jobs/{job.Id}", new { jobId = job.Id, status = job.Status });
    }

    try
    {
        var forecast = await orchestrator.RunMLForecastAsync(ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { forecast, state });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker ML forecast endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to run ML forecast",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/ml/embeddings", async (HttpContext httpContext, MLEmbeddingsRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    var sync = httpContext.Request.Query["sync"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    if (!sync)
    {
        var payload = request.Query is not null ? System.Text.Json.JsonSerializer.Serialize(new { query = request.Query }) : null;
        var job = orchestrator.JobStore.Enqueue(DailyDesk.Models.OfficeJobType.MLEmbeddings, "broker", payload);
        return Results.Accepted($"/api/jobs/{job.Id}", new { jobId = job.Id, status = job.Status });
    }

    try
    {
        var embeddings = await orchestrator.RunMLEmbeddingsAsync(request.Query, ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { embeddings, state });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker ML embeddings endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to run ML embeddings",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/ml/pipeline", async (HttpContext httpContext, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    var sync = httpContext.Request.Query["sync"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    if (!sync)
    {
        var job = orchestrator.JobStore.Enqueue(DailyDesk.Models.OfficeJobType.MLPipeline, "broker");
        return Results.Accepted($"/api/jobs/{job.Id}", new { jobId = job.Id, status = job.Status });
    }

    try
    {
        var result = await orchestrator.RunFullMLPipelineAsync(ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { result, state });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker full ML pipeline endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to run full ML pipeline",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/ml/export-artifacts", async (HttpContext httpContext, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    var sync = httpContext.Request.Query["sync"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    if (!sync)
    {
        var job = orchestrator.JobStore.Enqueue(DailyDesk.Models.OfficeJobType.MLExportArtifacts, "broker");
        return Results.Accepted($"/api/jobs/{job.Id}", new { jobId = job.Id, status = job.Status });
    }

    try
    {
        var artifacts = await orchestrator.ExportSuiteArtifactsAsync(ct);
        var state = await orchestrator.GetStateAsync(ct);
        return Results.Ok(new { artifacts, state });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Office broker ML artifact export endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to export ML artifacts",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

// --- Job Status Endpoints (Phase 3) ---

app.MapGet("/api/jobs", (HttpContext httpContext, OfficeBrokerOrchestrator orchestrator) =>
{
    var statusFilter = httpContext.Request.Query["status"].FirstOrDefault();
    var typeFilter = httpContext.Request.Query["type"].FirstOrDefault();

    IReadOnlyList<DailyDesk.Models.OfficeJob> jobs;
    if (!string.IsNullOrWhiteSpace(statusFilter))
    {
        jobs = orchestrator.JobStore.ListByStatus(statusFilter.ToLowerInvariant(), 50);
    }
    else
    {
        jobs = orchestrator.JobStore.ListRecent(50);
    }

    if (!string.IsNullOrWhiteSpace(typeFilter))
    {
        jobs = jobs.Where(j => j.Type.Equals(typeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    return Results.Ok(new { jobs, total = orchestrator.JobStore.GetTotalCount() });
});

app.MapGet("/api/jobs/{jobId}", (string jobId, OfficeBrokerOrchestrator orchestrator) =>
{
    var job = orchestrator.JobStore.GetById(jobId);
    if (job is null)
    {
        return Results.NotFound(new { error = $"Job '{jobId}' not found." });
    }
    return Results.Ok(new
    {
        job.Id,
        job.Type,
        job.Status,
        job.CreatedAt,
        job.StartedAt,
        job.CompletedAt,
        job.Error,
        job.RequestedBy,
    });
});

app.MapGet("/api/jobs/{jobId}/result", (string jobId, OfficeBrokerOrchestrator orchestrator) =>
{
    var job = orchestrator.JobStore.GetById(jobId);
    if (job is null)
    {
        return Results.NotFound(new { error = $"Job '{jobId}' not found." });
    }
    if (job.Status != DailyDesk.Models.OfficeJobStatus.Succeeded)
    {
        return Results.BadRequest(new { error = $"Job '{jobId}' has status '{job.Status}'. Result is only available for succeeded jobs." });
    }
    if (string.IsNullOrWhiteSpace(job.ResultJson))
    {
        return Results.Ok(new { result = (object?)null });
    }

    try
    {
        var result = System.Text.Json.JsonSerializer.Deserialize<object>(job.ResultJson);
        return Results.Ok(new { result });
    }
    catch
    {
        return Results.Ok(new { result = job.ResultJson });
    }
});

app.MapDelete("/api/jobs/{jobId}", (string jobId, OfficeBrokerOrchestrator orchestrator) =>
{
    var job = orchestrator.JobStore.GetById(jobId);
    if (job is null)
    {
        return Results.NotFound(new { error = $"Job '{jobId}' not found." });
    }
    if (job.Status is not (DailyDesk.Models.OfficeJobStatus.Succeeded or DailyDesk.Models.OfficeJobStatus.Failed))
    {
        return Results.BadRequest(new { error = $"Job '{jobId}' has status '{job.Status}'. Only completed (succeeded/failed) jobs can be deleted." });
    }

    orchestrator.JobStore.DeleteById(jobId);
    return Results.NoContent();
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
internal sealed record MLEmbeddingsRequest(string? Query);
