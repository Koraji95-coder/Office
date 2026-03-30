namespace DailyDesk.Models;

public sealed class OperatorActivityRecord
{
    public string EventType { get; set; } = string.Empty;
    public string Agent { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.Now;

    public string DisplayEventLabel =>
        EventType switch
        {
            "suggestion_auto_queued" => "Auto-staged",
            "suggestion_queued" => "Queued",
            "suggestion_completed" => "Completed",
            "suggestion_failed" => "Failed",
            _ => EventType.Replace('_', ' '),
        };

    public string DisplayMeta =>
        $"{OccurredAt:MMM d, h:mm tt} • {Agent}";

    public string DisplaySummary =>
        $"{OccurredAt:yyyy-MM-dd HH:mm} | {EventType} | {Agent} | {Summary}";
}
