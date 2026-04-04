namespace DailyDesk.Models;

/// <summary>
/// Represents an async job record with lifecycle tracking.
/// Persisted in the LiteDB jobs collection.
/// </summary>
public sealed class OfficeJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = OfficeJobStatus.Queued;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
    public string? ResultJson { get; set; }
    public string? RequestedBy { get; set; }
    public string? RequestPayload { get; set; }
}

/// <summary>
/// Constants for job status values.
/// </summary>
public static class OfficeJobStatus
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}

/// <summary>
/// Constants for job type values.
/// </summary>
public static class OfficeJobType
{
    public const string MLAnalytics = "ml-analytics";
    public const string MLForecast = "ml-forecast";
    public const string MLEmbeddings = "ml-embeddings";
    public const string MLPipeline = "ml-pipeline";
}
