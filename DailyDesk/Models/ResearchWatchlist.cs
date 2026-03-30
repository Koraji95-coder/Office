namespace DailyDesk.Models;

public sealed class ResearchWatchlist
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Topic { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Weekly";
    public string PreferredPerspective { get; set; } = "EE Mentor";
    public bool SaveToKnowledgeDefault { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset? LastRunAt { get; set; }

    public TimeSpan Interval =>
        Frequency switch
        {
            "Daily" => TimeSpan.FromDays(1),
            "Twice Weekly" => TimeSpan.FromDays(3),
            _ => TimeSpan.FromDays(7),
        };

    public DateTimeOffset NextDueAt => (LastRunAt ?? DateTimeOffset.MinValue).Add(Interval);

    public bool IsDue => IsEnabled && NextDueAt <= DateTimeOffset.Now;

    public string DueSummary =>
        LastRunAt is null
            ? "never run"
            : $"{Frequency} | last {LastRunAt:yyyy-MM-dd HH:mm} | next {NextDueAt:yyyy-MM-dd HH:mm}";
}
