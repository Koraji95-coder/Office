namespace DailyDesk.Models;

public sealed class SessionReflectionRecord
{
    public string Mode { get; init; } = string.Empty;
    public string Focus { get; init; } = string.Empty;
    public string Reflection { get; init; } = string.Empty;
    public DateTimeOffset CompletedAt { get; init; }

    public string DisplaySummary
    {
        get
        {
            var condensed = Reflection.Length <= 140
                ? Reflection
                : $"{Reflection[..137]}...";
            return $"{CompletedAt:yyyy-MM-dd HH:mm} | {Mode} | {Focus} | {condensed}";
        }
    }
}
