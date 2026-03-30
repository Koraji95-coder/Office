namespace DailyDesk.Models;

public sealed class TrainingOption
{
    public string Key { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;

    public string DisplayLabel => $"{Key}. {Text}";
}
