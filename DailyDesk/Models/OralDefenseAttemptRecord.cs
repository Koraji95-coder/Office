namespace DailyDesk.Models;

public sealed class OralDefenseAttemptRecord
{
    public string Title { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public string GenerationSource { get; init; } = string.Empty;
    public DateTimeOffset CompletedAt { get; init; }
    public int TotalScore { get; init; }
    public int MaxScore { get; init; } = 20;
    public string Summary { get; init; } = string.Empty;
    public string NextReviewRecommendation { get; init; } = string.Empty;
    public IReadOnlyList<DefenseRubricItem> RubricItems { get; init; } = Array.Empty<DefenseRubricItem>();
    public IReadOnlyList<string> FollowUpQuestions { get; init; } = Array.Empty<string>();

    public double ScoreRatio => MaxScore == 0 ? 0 : (double)TotalScore / MaxScore;

    public string DisplaySummary =>
        $"{CompletedAt:yyyy-MM-dd HH:mm} | {TotalScore}/{MaxScore} ({ScoreRatio:P0}) | {Topic}";
}
