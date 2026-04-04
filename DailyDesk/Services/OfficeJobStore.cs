using DailyDesk.Models;

namespace DailyDesk.Services;

/// <summary>
/// Manages job lifecycle persistence via LiteDB.
/// Thread-safe for concurrent reads; LiteDB serializes writes.
/// </summary>
public sealed class OfficeJobStore
{
    private readonly OfficeDatabase _db;

    public OfficeJobStore(OfficeDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Creates a new job record in queued state.
    /// </summary>
    public OfficeJob Enqueue(string type, string? requestedBy = null, string? requestPayload = null)
    {
        var job = new OfficeJob
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Status = OfficeJobStatus.Queued,
            CreatedAt = DateTimeOffset.Now,
            RequestedBy = requestedBy,
            RequestPayload = requestPayload,
        };

        _db.Jobs.Insert(job);
        return job;
    }

    /// <summary>
    /// Retrieves a job by its ID.
    /// </summary>
    public OfficeJob? GetById(string jobId)
    {
        return _db.Jobs.FindOne(j => j.Id == jobId);
    }

    /// <summary>
    /// Lists recent jobs, most recent first.
    /// </summary>
    public IReadOnlyList<OfficeJob> ListRecent(int count = 50)
    {
        return _db.Jobs.Query()
            .OrderByDescending(j => j.CreatedAt)
            .Limit(count)
            .ToList();
    }

    /// <summary>
    /// Dequeues the next queued job (oldest first).
    /// Returns null if no queued jobs exist.
    /// </summary>
    public OfficeJob? DequeueNext()
    {
        var job = _db.Jobs.Query()
            .Where(j => j.Status == OfficeJobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
            .Limit(1)
            .FirstOrDefault();

        if (job is null) return null;

        job.Status = OfficeJobStatus.Running;
        job.StartedAt = DateTimeOffset.Now;
        _db.Jobs.Update(job);
        return job;
    }

    /// <summary>
    /// Marks a job as succeeded with a JSON result.
    /// </summary>
    public void MarkSucceeded(string jobId, string? resultJson)
    {
        var job = _db.Jobs.FindOne(j => j.Id == jobId);
        if (job is null) return;

        job.Status = OfficeJobStatus.Succeeded;
        job.CompletedAt = DateTimeOffset.Now;
        job.ResultJson = resultJson;
        _db.Jobs.Update(job);
    }

    /// <summary>
    /// Marks a job as failed with an error message.
    /// </summary>
    public void MarkFailed(string jobId, string error)
    {
        var job = _db.Jobs.FindOne(j => j.Id == jobId);
        if (job is null) return;

        job.Status = OfficeJobStatus.Failed;
        job.CompletedAt = DateTimeOffset.Now;
        job.Error = error;
        _db.Jobs.Update(job);
    }
}
