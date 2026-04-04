namespace DailyDesk.Models;

public sealed class OperatorMemoryState
{
    public List<AgentPolicy> Policies { get; set; } = [];
    public List<SuggestedAction> Suggestions { get; set; } = [];
    public List<ResearchWatchlist> Watchlists { get; set; } = [];
    public List<DailyRunTemplate> DailyRuns { get; set; } = [];
    public List<OperatorActivityRecord> Activities { get; set; } = [];
    public List<DeskThreadState> DeskThreads { get; set; } = [];
    public OfficeWorkflowState Workflow { get; set; } = new();

    public IReadOnlyList<SuggestedAction> PendingApprovalSuggestions =>
        Suggestions
            .Where(item => item.RequiresApproval && item.IsPending)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();

    public IReadOnlyList<SuggestedAction> OpenSuggestions =>
        Suggestions
            .Where(item => !item.RequiresApproval && item.IsPending && !item.HasExecution)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();

    public IReadOnlyList<SuggestedAction> ApprovedSuggestions =>
        Suggestions
            .Where(item => item.NeedsFollowThrough)
            .OrderByDescending(item => item.ExecutionUpdatedAt ?? item.CreatedAt)
            .ToList();

    public IReadOnlyList<SuggestedAction> QueuedWorkSuggestions =>
        Suggestions
            .Where(item => item.IsQueued || item.IsRunning || item.IsFailed)
            .OrderByDescending(item => item.ExecutionUpdatedAt ?? item.CreatedAt)
            .ToList();

    public IReadOnlyList<SuggestedAction> RecentSuggestions =>
        Suggestions
            .OrderByDescending(item => item.CreatedAt)
            .Take(12)
            .ToList();

    public IReadOnlyList<ResearchWatchlist> DueWatchlists =>
        Watchlists
            .Where(item => item.IsDue)
            .OrderBy(item => item.NextDueAt)
            .ToList();

    public DailyRunTemplate? LatestDailyRun =>
        DailyRuns
            .OrderByDescending(item => item.DateKey)
            .ThenByDescending(item => item.GeneratedAt)
            .FirstOrDefault();

    public IReadOnlyList<OperatorActivityRecord> RecentActivities =>
        Activities
            .OrderByDescending(item => item.OccurredAt)
            .Take(12)
            .ToList();

    public IReadOnlyList<OperatorActivityRecord> SuggestionExecutionActivities =>
        Activities
            .Where(item =>
                item.EventType is "suggestion_auto_queued"
                or "suggestion_queued"
                or "suggestion_completed"
                or "suggestion_failed")
            .OrderByDescending(item => item.OccurredAt)
            .Take(12)
            .ToList();

    public DeskThreadState? FindDeskThread(string deskId) =>
        DeskThreads.FirstOrDefault(item =>
            item.DeskId.Equals(deskId, StringComparison.OrdinalIgnoreCase)
        );
}
