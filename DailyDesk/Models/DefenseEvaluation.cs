namespace DailyDesk.Models;

public sealed class DefenseEvaluation
{
    public string Summary { get; init; } = string.Empty;
    public string NextReviewRecommendation { get; init; } = string.Empty;
    public int TotalScore { get; init; }
    public int MaxScore { get; init; } = 20;
    public IReadOnlyList<DefenseRubricItem> RubricItems { get; init; } = Array.Empty<DefenseRubricItem>();
    public IReadOnlyList<string> RecommendedFollowUps { get; init; } = Array.Empty<string>();

    public double ScoreRatio => MaxScore == 0 ? 0 : (double)TotalScore / MaxScore;

    public string DisplaySummary => $"{TotalScore}/{MaxScore} ({ScoreRatio:P0}) - {Summary}";
}
