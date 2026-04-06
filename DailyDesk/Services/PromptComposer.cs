using DailyDesk.Models;

namespace DailyDesk.Services;

public static class PromptComposer
{
    public static string BuildChiefSystemPrompt() =>
        """
        You are the chief of staff for a Windows desktop called Daily Desk.
        Keep your tone direct, practical, and grounded in operator reality.
        The app is separate from Suite, but it uses Suite as a proving ground for the user's electrical-engineering growth, repo progress, and later monetization.
        Return plain text with sections named TODAY, STUDY, SUITE, CAREER, and BUSINESS.
        """;

    public static string BuildChiefUserPrompt(
        SuiteSnapshot snapshot,
        IReadOnlyList<string> installedModels,
        LearningProfile profile,
        LearningLibrary library,
        TrainingHistorySummary history
    ) =>
        $"""
        User goals:
        - grow as an electrical engineer
        - improve Suite over time
        - find realistic monetization edges later

        Learning profile:
        - summary: {profile.Summary}
        - current need: {profile.CurrentNeed}
        - coaching rules: {ToSentence(profile.CoachingRules.Take(4).ToList())}
        - imported knowledge: {ToSentence(library.Documents.Take(4).Select(document => document.PromptSummary).ToList())}

        Training memory:
        - practice summary: {history.OverallSummary}
        - review queue: {history.ReviewQueueSummary}
        - defense summary: {history.DefenseSummary}
        - recent reflections: {ToSentence(history.RecentReflections.Take(3).Select(item => $"{item.Mode} {item.Focus}: {item.Reflection}").ToList())}

        Current Suite pulse:
        - {snapshot.StatusSummary}
        - hot areas: {ToSentence(snapshot.HotAreas)}
        - recent commits: {ToSentence(snapshot.RecentCommits.Take(4).ToList())}
        - next session order: {ToSentence(snapshot.NextSessionTasks.Take(4).ToList())}
        - monetization leads: {ToSentence(snapshot.MonetizationMoves.Take(4).ToList())}

        Installed models: {ToSentence(installedModels)}
        """;

    public static string BuildChallengeSystemPrompt() =>
        """
        You are an electrical engineering mentor.
        Build a single challenge that strengthens technical reasoning, design tradeoffs, and communication.
        Return plain text with sections TITLE, PROMPT, and WHAT GOOD LOOKS LIKE.
        """;

    public static string BuildStudyGuideSystemPrompt() =>
        """
        You are an electrical engineering mentor building a study guide from imported notebook material.
        Prefer notebook evidence over generic filler.
        If the imported notes are thin or incomplete, say that plainly instead of guessing.
        Return plain text with sections TITLE, NOTEBOOK SCOPE, CORE IDEAS, FORMULAS AND RULES, FAILURE MODES, SELF-CHECK, and NEXT DRILL.
        In SELF-CHECK, give 5 short quiz questions without answers.
        Keep the guide practical, operator-safe, and useful for review.
        """;

    public static string BuildChallengeUserPrompt(
        SuiteSnapshot snapshot,
        LearningProfile profile,
        LearningLibrary library,
        TrainingHistorySummary history
    ) =>
        $"""
        Tie the challenge to the user's active work.
        Hot areas: {ToSentence(snapshot.HotAreas)}
        Next steps: {ToSentence(snapshot.NextSessionTasks.Take(3).ToList())}
        Product focus later: {ToSentence(snapshot.MonetizationMoves.Take(3).ToList())}
        Current learning need: {profile.CurrentNeed}
        Review queue: {history.ReviewQueueSummary}
        Defense summary: {history.DefenseSummary}
        Recent reflections: {ToSentence(history.RecentReflections.Take(3).Select(item => $"{item.Mode} {item.Focus}: {item.Reflection}").ToList())}
        Imported sources: {ToSentence(library.Documents.Take(4).Select(document => document.PromptSummary).ToList())}
        """;

    public static string BuildStudyGuideUserPrompt(
        string focus,
        LearningProfile profile,
        LearningLibrary library,
        TrainingHistorySummary history
    )
    {
        var notebookEvidence = KnowledgePromptContextBuilder.BuildRelevantContext(
            library,
            new[]
            {
                focus,
                profile.CurrentNeed,
                history.ReviewQueueSummary,
                history.DefenseSummary,
                string.Join("; ", history.WeakTopics.Take(4).Select(item => item.Topic)),
            },
            maxDocuments: 3,
            maxTotalCharacters: 2600,
            maxExcerptCharacters: 760
        );

        return $"""
        Build a study guide from the imported notes.
        Focus: {focus}

        Learning profile:
        - current need: {profile.CurrentNeed}
        - coaching rules: {ToSentence(profile.CoachingRules.Take(4).ToList())}

        Training memory:
        - review queue: {history.ReviewQueueSummary}
        - defense summary: {history.DefenseSummary}
        - weak topics: {ToSentence(history.WeakTopics.Take(4).Select(item => $"{item.Topic} ({item.Accuracy:P0})").ToList())}

        Imported sources:
        - {ToSentence(library.Documents.Take(5).Select(document => document.PromptSummary).ToList())}

        Relevant notebook evidence:
        {notebookEvidence}

        Requirements:
        - stay grounded in the imported notes first
        - make the guide good for learning and later quizzing
        - identify missing notebook context when it matters
        - end with the best next quiz or drill focus
        """;
    }

    public static string BuildBusinessSystemPrompt() =>
        """
        You are a business strategist helping package an engineering operations tool into something sellable later.
        Avoid hype. Push toward one clear job and one pilot-shaped offer.
        Return plain text with sections CORE OFFER, WHY IT WINS, and WHAT TO PROVE NEXT.
        """;

    public static string BuildBusinessUserPrompt(
        SuiteSnapshot snapshot,
        LearningProfile profile,
        LearningLibrary library,
        TrainingHistorySummary history
    ) =>
        $"""
        Keep the answer tied to the Suite repo and the user's electrical-engineering growth.
        Product pillars: {ToSentence(snapshot.ProductPillars.Take(4).ToList())}
        Later monetization leads: {ToSentence(snapshot.MonetizationMoves.Take(5).ToList())}
        Active hot areas: {ToSentence(snapshot.HotAreas)}
        Current learning profile: {profile.Summary}
        Current training memory: {history.OverallSummary} | {history.DefenseSummary}
        Recent reflections: {ToSentence(history.RecentReflections.Take(3).Select(item => $"{item.Mode} {item.Focus}: {item.Reflection}").ToList())}
        Imported knowledge: {ToSentence(library.Documents.Take(4).Select(document => document.PromptSummary).ToList())}
        """;

    public static string BuildMLEngineerSystemPrompt() =>
        """
        You are an ML engineering mentor embedded in a Windows desktop called Daily Desk.
        Your role is to help the operator understand and improve their machine learning pipeline.
        The pipeline uses Scikit-learn for learning analytics, PyTorch for document embeddings, and TensorFlow for progress forecasting.
        These tools analyze training history, knowledge documents, and operator decisions to produce actionable insights.
        The ML artifacts integrate with a companion app called Suite for electrical engineering production workflows.
        Keep answers practical, tied to real ML concepts, and grounded in the operator's actual data.
        Return plain text with sections: ML STATUS, INSIGHTS, RECOMMENDATIONS, and SUITE INTEGRATION.
        """;

    public static string BuildMLEngineerUserPrompt(
        MLAnalyticsResult? analytics,
        MLForecastResult? forecast,
        MLEmbeddingsResult? embeddings,
        LearningProfile profile,
        TrainingHistorySummary history
    )
    {
        var analyticsEngine = analytics?.Engine ?? "not run";
        var forecastEngine = forecast?.Engine ?? "not run";
        var embeddingsEngine = embeddings?.Engine ?? "not run";

        var weakTopics = analytics?.WeakTopics?.Take(5)
            .Select(t => $"{t.Topic} ({t.Accuracy:P0})")
            .ToList() ?? [];

        var plateaus = forecast?.Plateaus?.Take(3)
            .Select(p => $"{p.Topic} at {p.PlateauAccuracy:P0}")
            .ToList() ?? [];

        var anomalies = forecast?.Anomalies?.Take(3)
            .Select(a => $"{a.Topic}: dropped {a.Drop:P0} ({a.Severity})")
            .ToList() ?? [];

        var clusters = analytics?.TopicClusters?.Take(3)
            .Select(c => $"{c.Label}: {string.Join(", ", c.Topics.Take(4))}")
            .ToList() ?? [];

        return $"""
        ML pipeline status:
        - analytics engine: {analyticsEngine}
        - forecast engine: {forecastEngine}
        - embeddings engine: {embeddingsEngine}
        - overall readiness: {analytics?.OverallReadiness ?? 0:P0}

        Weak topics needing attention: {ToSentence(weakTopics)}
        Plateau detections: {ToSentence(plateaus)}
        Anomaly alerts: {ToSentence(anomalies)}
        Topic clusters: {ToSentence(clusters)}
        Operator decision pattern: {analytics?.OperatorPattern?.Pattern ?? "unknown"}

        Learning context:
        - current need: {profile.CurrentNeed}
        - training summary: {history.OverallSummary}
        - defense summary: {history.DefenseSummary}
        """;
    }

    /// <summary>
    /// Builds the system prompt for agent-generated practice test creation, as defined in
    /// AGENT_REPLY_GUIDE.md.  The prompt instructs the model to return exactly
    /// <paramref name="questionCount"/> multiple-choice questions with mixed difficulty and a
    /// full answer key with explanations — the canonical pattern from the guide.
    /// </summary>
    public static string BuildPracticeTestSystemPrompt(int questionCount) =>
        $"""
        You create practice tests for an aspiring electrical engineer who is also building operator-first automation software.
        Return strict JSON only.
        Generate exactly {questionCount} multiple-choice questions with mixed difficulty.
        Each question must include:
        - topic
        - difficulty
        - prompt
        - four options with keys A, B, C, D
        - correctOptionKey
        - explanation
        - suiteConnection
        The answer key and explanations must be included for every question.
        Keep the questions focused on electrical reasoning, standards, drafting safety, production workflows, and engineering judgment.
        """;

    private static string ToSentence(IReadOnlyList<string> items) =>
        items.Count == 0 ? "none recorded" : string.Join("; ", items);
}
