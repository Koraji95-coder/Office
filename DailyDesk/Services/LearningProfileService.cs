using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class LearningProfileService
{
    public LearningProfile Build(
        LearningLibrary library,
        TrainingHistorySummary trainingHistory,
        SuiteSnapshot snapshot
    )
    {
        var weakestTopic = trainingHistory.WeakTopics.FirstOrDefault();
        var activeTopics = trainingHistory
            .WeakTopics.Select(topic => topic.Topic)
            .Concat(library.TopicHeadlines)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        var repoTie = snapshot.HotAreas.FirstOrDefault() ?? "the current Suite hotspot";
        var monetizationTie =
            snapshot.MonetizationMoves.FirstOrDefault()
            ?? "drawing production control for electrical teams";

        var summary = weakestTopic is null
            ? $"The desk has {library.Documents.Count} local knowledge files and no scored baseline yet. Start broad, then narrow once misses appear."
            : $"The desk is coaching toward {weakestTopic.Topic} while tying the work back to {repoTie}. Imported material currently emphasizes {ToSentence(library.TopicHeadlines.Take(5).ToList())}.";

        var currentNeed = weakestTopic is null
            ? $"Generate a mixed baseline test, then add more personal notes or reference docs to {library.RootPath}."
            : $"Retest {weakestTopic.Topic}, then explain how it changes a design or review decision in {repoTie}.";

        var coachingRules = new List<string>
        {
            library.Documents.Count == 0
                ? "Use the knowledge folder for notes, PDFs, and DOCX references so coaching can anchor to your own material."
                : $"Use imported material from {library.Documents.Count} local knowledge files before falling back to generic coaching.",
            weakestTopic is null
                ? "Run mixed-difficulty practice until the desk has enough history to personalize the next drill."
                : $"Bias the next practice sets toward {weakestTopic.Topic} until accuracy clears 80%.",
            $"Tie explanations back to operator safety, production reliability, or engineering tradeoffs in {repoTie}.",
            $"Convert one solved concept into either career proof or a productizable workflow around {monetizationTie}.",
        };

        return new LearningProfile
        {
            Summary = summary,
            CurrentNeed = currentNeed,
            ActiveTopics = activeTopics,
            CoachingRules = coachingRules,
        };
    }

    private static string ToSentence(IReadOnlyList<string> items) =>
        items.Count == 0 ? "your starter notes" : string.Join("; ", items);
}
