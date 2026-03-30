namespace DailyDesk.Models;

public sealed class PracticeTest
{
    public string Title { get; init; } = string.Empty;
    public string Overview { get; init; } = string.Empty;
    public string Focus { get; init; } = string.Empty;
    public string Difficulty { get; init; } = string.Empty;
    public string GenerationSource { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
    public IReadOnlyList<TrainingQuestion> Questions { get; init; } = Array.Empty<TrainingQuestion>();
}
