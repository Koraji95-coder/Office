using System.Text.Json.Serialization;

namespace DailyDesk.Models;

public sealed class SuggestedAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string SourceAgent { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public string Rationale { get; set; } = string.Empty;
    public string ExpectedBenefit { get; set; } = string.Empty;
    public string LinkedArea { get; set; } = string.Empty;
    public string WhatYouLearn { get; set; } = string.Empty;
    public string ProductImpact { get; set; } = string.Empty;
    public string CareerValue { get; set; } = string.Empty;
    public bool RequiresApproval { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public SuggestionOutcome? Outcome { get; set; }
    public string ExecutionStatus { get; set; } = "not_queued";
    public string ExecutionSummary { get; set; } = string.Empty;
    public DateTimeOffset? ExecutionUpdatedAt { get; set; }
    public string LatestResultSummary { get; set; } = string.Empty;
    public string LatestResultDetail { get; set; } = string.Empty;
    public List<string> LatestResultSources { get; set; } = [];

    [JsonIgnore]
    public string OutcomeReasonInput { get; set; } = string.Empty;

    [JsonIgnore]
    public string OutcomeNoteInput { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsPending => Outcome is null || Outcome.Status.Equals("pending", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string Status => Outcome?.Status ?? "pending";

    [JsonIgnore]
    public string StatusSummary => Outcome?.DisplaySummary ?? "pending review";

    [JsonIgnore]
    public string DisplaySummary =>
        $"{SourceAgent} | {Priority} | {Title}";

    [JsonIgnore]
    public bool IsAccepted =>
        Outcome?.Status.Equals("accepted", StringComparison.OrdinalIgnoreCase) == true;

    [JsonIgnore]
    public bool IsQueued => ExecutionStatus.Equals("queued", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsRunning => ExecutionStatus.Equals("running", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsCompleted => ExecutionStatus.Equals("completed", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsFailed => ExecutionStatus.Equals("failed", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool HasExecution => IsQueued || IsRunning || IsCompleted || IsFailed;

    [JsonIgnore]
    public bool HasLatestResult =>
        !string.IsNullOrWhiteSpace(LatestResultSummary)
        || !string.IsNullOrWhiteSpace(LatestResultDetail)
        || LatestResultSources.Count > 0;

    [JsonIgnore]
    public bool NeedsFollowThrough => IsAccepted && !HasExecution;

    [JsonIgnore]
    public string ExecutionStatusSummary =>
        ExecutionStatus switch
        {
            "queued" => "queued",
            "running" => "running now",
            "completed" => "completed",
            "failed" => "needs retry",
            _ => "not queued",
        };

    [JsonIgnore]
    public string InboxBadgeText =>
        HasExecution
            ? ExecutionStatusSummary
            : NeedsFollowThrough
                ? "approved next"
                : (IsPending ? "pending" : Status);

    [JsonIgnore]
    public string InboxSummary =>
        HasExecution && !string.IsNullOrWhiteSpace(ExecutionSummary)
            ? ExecutionSummary
            : NeedsFollowThrough
                ? $"Approved. Queue or Run now to start. {StatusSummary}"
                : StatusSummary;

    [JsonIgnore]
    public bool WasAutoStaged =>
        (Outcome?.Reason?.Contains("auto-staged", StringComparison.OrdinalIgnoreCase) == true)
        || (ExecutionSummary?.Contains("auto-queued", StringComparison.OrdinalIgnoreCase) == true);
}
