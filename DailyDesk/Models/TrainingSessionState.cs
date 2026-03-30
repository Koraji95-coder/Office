namespace DailyDesk.Models;

public sealed class TrainingSessionState
{
    public TrainingSessionStage Stage { get; init; } = TrainingSessionStage.Plan;
    public string Focus { get; init; } = "Protection, grounding, standards, drafting safety";
    public string FocusReason { get; init; } =
        "Set a focus manually or start from a review target to begin a guided session.";
    public string StageSummary { get; init; } =
        "Plan the session first, then generate practice, run defense, and save a reflection.";
    public bool PracticeGenerated { get; init; }
    public bool PracticeScored { get; init; }
    public bool DefenseGenerated { get; init; }
    public bool DefenseScored { get; init; }
    public bool ReflectionSaved { get; init; }
    public string HistoryFilePath { get; init; } = string.Empty;
    public bool HistoryExists { get; init; }
    public DateTimeOffset? LastHistoryWriteAt { get; init; }

    public string PracticeStatus =>
        PracticeScored
            ? "Practice scored and written to history."
            : PracticeGenerated
                ? "Practice generated and ready to score."
                : "Practice not started.";

    public string DefenseStatus =>
        DefenseScored
            ? "Defense scored and written to history."
            : DefenseGenerated
                ? "Defense generated and ready to score."
                : "Defense not started.";

    public string ReflectionStatus =>
        ReflectionSaved
            ? "Reflection saved to local training memory."
            : "Reflection not saved yet.";

    public string ChecklistSummary =>
        $"{(PracticeGenerated ? "practice ready" : "practice pending")} | {(PracticeScored ? "practice scored" : "practice unscored")} | {(DefenseGenerated ? "defense ready" : "defense pending")} | {(DefenseScored ? "defense scored" : "defense unscored")} | {(ReflectionSaved ? "reflection saved" : "reflection pending")}";

    public string HistorySummary =>
        HistoryExists
            ? $"History file: {HistoryFilePath} | last write {LastHistoryWriteAt:yyyy-MM-dd HH:mm}"
            : $"History file path: {HistoryFilePath} | not created yet. It appears after you score a practice test, score a defense answer, and save a reflection.";
}
