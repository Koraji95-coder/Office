namespace DailyDesk.Models;

public sealed class FocusCard
{
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string Accent { get; init; } = "#B87333";
}
