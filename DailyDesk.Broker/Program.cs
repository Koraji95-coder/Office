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
builder.Services.AddHostedService<JobRetentionWorker>();
builder.Services.AddHostedService<JobSchedulerWorker>();

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

// --- Detailed Health Check (Phase 4) ---

app.MapGet("/api/health", async (OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await orchestrator.GetDetailedHealthAsync(ct));
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Detailed health check failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Detailed health check failed",
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

// --- Knowledge Indexing Endpoints (Phase 5) ---

app.MapPost("/api/ml/index-knowledge", async (HttpContext httpContext, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    var sync = httpContext.Request.Query["sync"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    if (!sync)
    {
        var job = orchestrator.JobStore.Enqueue(DailyDesk.Models.OfficeJobType.KnowledgeIndex, "broker");
        return Results.Accepted($"/api/jobs/{job.Id}", new { jobId = job.Id, status = job.Status });
    }

    try
    {
        var result = await orchestrator.RunKnowledgeIndexAsync(ct);
        return Results.Ok(result);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Knowledge indexing endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to index knowledge documents",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapGet("/api/knowledge/index-status", async (OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var status = await orchestrator.GetKnowledgeIndexStatusAsync(ct);
        return Results.Ok(status);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Knowledge index status endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Failed to get index status",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

// --- Knowledge Search Endpoint (Phase 9) ---

app.MapPost("/api/knowledge/search", async (KnowledgeSearchRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
{
    try
    {
        var searchService = new DailyDesk.Services.KnowledgeSearchService(
            orchestrator.EmbeddingService,
            orchestrator.VectorStoreService);
        var response = await searchService.SearchAsync(
            request.Query,
            topK: request.TopK > 0 ? request.TopK : 5,
            cancellationToken: ct);
        return Results.Ok(response);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Knowledge search endpoint failed.");
        return Results.Problem(
            detail: exception.Message,
            title: "Knowledge search failed",
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

// --- Job Metrics Endpoint (Phase 4) ---

app.MapGet("/api/jobs/metrics", (OfficeBrokerOrchestrator orchestrator) =>
{
    return Results.Ok(orchestrator.JobStore.GetMetrics());
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

// --- Schedule Endpoints (Phase 8) ---

app.MapGet("/api/schedules", (OfficeBrokerOrchestrator orchestrator) =>
{
    var schedules = orchestrator.SchedulerStore.ListAll();
    return Results.Ok(new { schedules });
});

app.MapPost("/api/schedules", (CreateScheduleRequest request, OfficeBrokerOrchestrator orchestrator) =>
{
    var validator = new CreateScheduleRequestValidator();
    var validation = validator.Validate(request);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
    }

    var schedule = new DailyDesk.Models.JobSchedule
    {
        Name = request.Name,
        JobType = request.JobType,
        CronExpression = request.CronExpression,
        Enabled = request.Enabled ?? true,
        RequestPayload = request.RequestPayload,
    };

    var created = orchestrator.SchedulerStore.Create(schedule);
    return Results.Created($"/api/schedules/{created.Id}", created);
});

app.MapPut("/api/schedules/{id}", (string id, UpdateScheduleRequest request, OfficeBrokerOrchestrator orchestrator) =>
{
    var validator = new UpdateScheduleRequestValidator();
    var validation = validator.Validate(request);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
    }

    var updated = orchestrator.SchedulerStore.Update(id, schedule =>
    {
        if (request.Name is not null) schedule.Name = request.Name;
        if (request.CronExpression is not null) schedule.CronExpression = request.CronExpression;
        if (request.Enabled.HasValue) schedule.Enabled = request.Enabled.Value;
        if (request.RequestPayload is not null) schedule.RequestPayload = request.RequestPayload;
    });

    if (updated is null)
    {
        return Results.NotFound(new { error = $"Schedule '{id}' not found." });
    }

    return Results.Ok(updated);
});

app.MapDelete("/api/schedules/{id}", (string id, OfficeBrokerOrchestrator orchestrator) =>
{
    var deleted = orchestrator.SchedulerStore.Delete(id);
    if (!deleted)
    {
        return Results.NotFound(new { error = $"Schedule '{id}' not found." });
    }
    return Results.NoContent();
});

// --- Daily Run Endpoint (Phase 8) ---

app.MapGet("/api/daily-run/latest", (OfficeBrokerOrchestrator orchestrator) =>
{
    var summary = orchestrator.GetLatestDailyRunSummary();
    if (summary is null)
    {
        return Results.Ok(new { message = "No daily run has been executed yet." });
    }
    return Results.Ok(summary);
});

// --- Workflow Endpoints (Phase 8) ---

app.MapGet("/api/workflows", (OfficeBrokerOrchestrator orchestrator) =>
{
    var workflows = orchestrator.WorkflowStore.ListAll();
    return Results.Ok(new { workflows });
});

app.MapPost("/api/workflows", (CreateWorkflowRequest request, OfficeBrokerOrchestrator orchestrator) =>
{
    var validator = new CreateWorkflowRequestValidator();
    var validation = validator.Validate(request);
    if (!validation.IsValid)
    {
        return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
    }

    var template = new DailyDesk.Models.WorkflowTemplate
    {
        Name = request.Name,
        Description = request.Description ?? string.Empty,
        FailurePolicy = request.FailurePolicy ?? DailyDesk.Models.WorkflowFailurePolicy.Abort,
        Steps = request.Steps.Select(s => new DailyDesk.Models.WorkflowStep
        {
            JobType = s.JobType,
            Label = s.Label ?? string.Empty,
            RequestPayload = s.RequestPayload,
        }).ToList(),
    };

    var created = orchestrator.WorkflowStore.Create(template);
    return Results.Created($"/api/workflows/{created.Id}", created);
});

app.MapPost("/api/workflows/{id}/run", (string id, OfficeBrokerOrchestrator orchestrator) =>
{
    var template = orchestrator.WorkflowStore.GetById(id);
    if (template is null)
    {
        return Results.NotFound(new { error = $"Workflow '{id}' not found." });
    }

    var jobIds = new List<string>();
    foreach (var step in template.Steps)
    {
        var job = orchestrator.JobStore.Enqueue(
            step.JobType,
            requestedBy: $"workflow:{template.Name}",
            requestPayload: step.RequestPayload);
        jobIds.Add(job.Id);
    }

    return Results.Accepted(value: new
    {
        workflowId = id,
        workflowName = template.Name,
        jobIds,
        totalSteps = template.Steps.Count,
    });
});

app.MapDelete("/api/workflows/{id}", (string id, OfficeBrokerOrchestrator orchestrator) =>
{
    var deleted = orchestrator.WorkflowStore.Delete(id);
    if (!deleted)
    {
        return Results.NotFound(new { error = $"Workflow '{id}' not found or is a built-in template." });
    }
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

// Phase 8: Schedule request records
internal sealed record CreateScheduleRequest(
    string Name,
    string JobType,
    string CronExpression,
    bool? Enabled,
    string? RequestPayload
);
internal sealed record UpdateScheduleRequest(
    string? Name,
    string? CronExpression,
    bool? Enabled,
    string? RequestPayload
);

// Phase 8: Workflow request records
internal sealed record CreateWorkflowRequest(
    string Name,
    string? Description,
    string? FailurePolicy,
    IReadOnlyList<CreateWorkflowStepRequest> Steps
);
internal sealed record CreateWorkflowStepRequest(
    string JobType,
    string? Label,
    string? RequestPayload
);

// Phase 9: Knowledge search request record
internal sealed record KnowledgeSearchRequest(string Query, int TopK = 5);
