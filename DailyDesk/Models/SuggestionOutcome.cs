namespace DailyDesk.Models;

public sealed class SuggestionOutcome
{
    public string Status { get; set; } = "pending";
    public string Reason { get; set; } = string.Empty;
    public string OutcomeNote { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.Now;

    public string DisplaySummary =>
        $"{Status} | {Reason}{(string.IsNullOrWhiteSpace(OutcomeNote) ? string.Empty : $" | {OutcomeNote}")}";
}
