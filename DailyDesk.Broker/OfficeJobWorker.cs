using System.Text.Json;
using DailyDesk.Models;
using DailyDesk.Services;

namespace DailyDesk.Broker;

/// <summary>
/// Background worker that polls for queued jobs and executes them.
/// Runs one job at a time to match the existing _mlGate concurrency model.
/// </summary>
public sealed class OfficeJobWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly OfficeBrokerOrchestrator _orchestrator;
    private readonly ILogger<OfficeJobWorker> _logger;
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true, WriteIndented = false };

    public OfficeJobWorker(
        OfficeBrokerOrchestrator orchestrator,
        ILogger<OfficeJobWorker> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OfficeJobWorker started. Polling for queued jobs every {Interval}s.", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = _orchestrator.JobStore.DequeueNext();
                if (job is not null)
                {
                    await ExecuteJobAsync(job, stoppingToken);
                }
                else
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OfficeJobWorker encountered an unexpected error.");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        _logger.LogInformation("OfficeJobWorker stopped.");
    }

    private async Task ExecuteJobAsync(OfficeJob job, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Executing job {JobId} of type {JobType}.", job.Id, job.Type);

        try
        {
            var resultJson = job.Type switch
            {
                OfficeJobType.MLAnalytics => await ExecuteMLAnalyticsAsync(stoppingToken),
                OfficeJobType.MLForecast => await ExecuteMLForecastAsync(stoppingToken),
                OfficeJobType.MLEmbeddings => await ExecuteMLEmbeddingsAsync(job.RequestPayload, stoppingToken),
                OfficeJobType.MLPipeline => await ExecuteMLPipelineAsync(stoppingToken),
                _ => throw new InvalidOperationException($"Unknown job type: {job.Type}"),
            };

            _orchestrator.JobStore.MarkSucceeded(job.Id, resultJson);
            _logger.LogInformation("Job {JobId} succeeded.", job.Id);
        }
        catch (Exception ex)
        {
            _orchestrator.JobStore.MarkFailed(job.Id, ex.Message);
            _logger.LogWarning(ex, "Job {JobId} failed.", job.Id);
        }
    }

    private async Task<string> ExecuteMLAnalyticsAsync(CancellationToken ct)
    {
        var result = await _orchestrator.RunMLAnalyticsAsync(ct);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    private async Task<string> ExecuteMLForecastAsync(CancellationToken ct)
    {
        var result = await _orchestrator.RunMLForecastAsync(ct);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    private async Task<string> ExecuteMLEmbeddingsAsync(string? requestPayload, CancellationToken ct)
    {
        string? query = null;
        if (!string.IsNullOrWhiteSpace(requestPayload))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<MLEmbeddingsJobPayload>(requestPayload, _jsonOptions);
                query = payload?.Query;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Malformed embeddings job payload, proceeding with default query.");
            }
        }

        var result = await _orchestrator.RunMLEmbeddingsAsync(query, ct);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    private async Task<string> ExecuteMLPipelineAsync(CancellationToken ct)
    {
        var result = await _orchestrator.RunFullMLPipelineAsync(ct);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    private sealed class MLEmbeddingsJobPayload
    {
        public string? Query { get; set; }
    }
}
