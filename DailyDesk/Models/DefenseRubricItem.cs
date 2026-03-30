namespace DailyDesk.Models;

public sealed class DefenseRubricItem
{
    public string Name { get; init; } = string.Empty;
    public int Score { get; init; }
    public int MaxScore { get; init; } = 4;
    public string Feedback { get; init; } = string.Empty;

    public string DisplaySummary => $"{Name}: {Score}/{MaxScore} - {Feedback}";
}
