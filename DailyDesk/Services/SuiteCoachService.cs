using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class SuiteCoachService
{
    private readonly IModelProvider _modelProvider;
    private readonly string _model;

    public SuiteCoachService(IModelProvider modelProvider, string model)
    {
        _modelProvider = modelProvider;
        _model = model;
    }

    public async Task<IReadOnlyList<SuggestedAction>> CreateSuggestionsAsync(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        OperatorMemoryState operatorState,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var generated = await _modelProvider.GenerateJsonAsync<SuiteCoachContract>(
                _model,
                BuildSystemPrompt(),
                BuildUserPrompt(
                    snapshot,
                    historySummary,
                    learningProfile,
                    learningLibrary,
                    operatorState
                ),
                cancellationToken
            );

            var converted = ConvertContract(generated);
            if (converted.Count > 0)
            {
                return converted;
            }
        }
        catch
        {
            // Fall back to deterministic suggestions.
        }

        return BuildFallbackSuggestions(snapshot, historySummary, learningProfile);
    }

    private static string BuildSystemPrompt() =>
        """
        You are Repo Coach inside Daily Desk.
        You only propose read-only next moves and implementation proposals for later approval.
        Return strict JSON only with:
        - suggestions: array of 3 items
        Each suggestion must include:
        - title
        - actionType
        - priority
        - rationale
        - expectedBenefit
        - linkedArea
        - whatYouLearn
        - productImpact
        - careerValue
        - requiresApproval
        Keep the advice narrow, practical, and useful for an electrical engineering career engine.
        """;

    private static string BuildUserPrompt(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        OperatorMemoryState operatorState
    ) =>
        $"""
        Build the best next read-only Suite coach suggestions.

        Current Suite pulse:
        - status: {snapshot.StatusSummary}
        - hot areas: {JoinOrNone(snapshot.HotAreas)}
        - changed files: {JoinOrNone(snapshot.ChangedFiles.Take(8).ToList())}
        - next session tasks: {JoinOrNone(snapshot.NextSessionTasks.Take(5).ToList())}
        - recent commits: {JoinOrNone(snapshot.RecentCommits.Take(5).ToList())}
        - monetization leads: {JoinOrNone(snapshot.MonetizationMoves.Take(4).ToList())}

        Current learning state:
        - need: {learningProfile.CurrentNeed}
        - active topics: {JoinOrNone(learningProfile.ActiveTopics.Take(6).ToList())}
        - review queue: {historySummary.ReviewQueueSummary}
        - defense summary: {historySummary.DefenseSummary}
        - imported knowledge: {JoinOrNone(learningLibrary.Documents.Take(4).Select(item => item.PromptSummary).ToList())}

        Suggestion memory:
        - recent accepted/rejected/deferred items: {JoinOrNone(operatorState.RecentSuggestions.Where(item => !item.IsPending).Take(6).Select(item => $"{item.Title} => {item.StatusSummary}").ToList())}
        - pending approvals: {JoinOrNone(operatorState.PendingApprovalSuggestions.Take(5).Select(item => item.Title).ToList())}
        - recent activities: {JoinOrNone(operatorState.RecentActivities.Take(6).Select(item => item.DisplaySummary).ToList())}
        """;

    private static IReadOnlyList<SuggestedAction> ConvertContract(SuiteCoachContract? contract)
    {
        if (contract?.Suggestions is null || contract.Suggestions.Count == 0)
        {
            return Array.Empty<SuggestedAction>();
        }

        var suggestions = new List<SuggestedAction>();
        foreach (var item in contract.Suggestions.Take(5))
        {
            if (string.IsNullOrWhiteSpace(item.Title)
                || string.IsNullOrWhiteSpace(item.Rationale)
                || string.IsNullOrWhiteSpace(item.ExpectedBenefit))
            {
                continue;
            }

            suggestions.Add(
                new SuggestedAction
                {
                    Title = item.Title.Trim(),
                    SourceAgent = "Repo Coach",
                    ActionType = string.IsNullOrWhiteSpace(item.ActionType)
                        ? "repo_proposal"
                        : item.ActionType.Trim(),
                    Priority = string.IsNullOrWhiteSpace(item.Priority)
                        ? "medium"
                        : item.Priority.Trim(),
                    Rationale = item.Rationale.Trim(),
                    ExpectedBenefit = item.ExpectedBenefit.Trim(),
                    LinkedArea = string.IsNullOrWhiteSpace(item.LinkedArea)
                        ? "Suite"
                        : item.LinkedArea.Trim(),
                    WhatYouLearn = string.IsNullOrWhiteSpace(item.WhatYouLearn)
                        ? "This should sharpen implementation judgment and systems thinking."
                        : item.WhatYouLearn.Trim(),
                    ProductImpact = string.IsNullOrWhiteSpace(item.ProductImpact)
                        ? "This should improve product clarity or reduce delivery risk."
                        : item.ProductImpact.Trim(),
                    CareerValue = string.IsNullOrWhiteSpace(item.CareerValue)
                        ? "This should strengthen your operator-first electrical automation story."
                        : item.CareerValue.Trim(),
                    RequiresApproval = item.RequiresApproval,
                    CreatedAt = DateTimeOffset.Now,
                }
            );
        }

        return suggestions;
    }

    private static IReadOnlyList<SuggestedAction> BuildFallbackSuggestions(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile
    )
    {
        var reviewTarget = historySummary.ReviewRecommendations.FirstOrDefault()?.Topic
            ?? learningProfile.ActiveTopics.FirstOrDefault()
            ?? "electrical production judgment";
        var firstTask = snapshot.NextSessionTasks.FirstOrDefault()
            ?? "Review the current Suite hotspot and split the next task into a proposal-sized unit.";
        var secondTask = snapshot.HotAreas.FirstOrDefault()
            ?? "current workspace pressure";
        var firstMonetization = snapshot.MonetizationMoves.FirstOrDefault()
            ?? "drawing production control for electrical teams";

        return
        [
            new SuggestedAction
            {
                Title = $"Proposal pass: {firstTask}",
                SourceAgent = "Repo Coach",
                ActionType = "repo_proposal",
                Priority = "high",
                Rationale =
                    $"The repo already points to this as the next bounded move, and it maps to {secondTask}.",
                ExpectedBenefit =
                    "You get a concrete next step without mutating the repo yet, which keeps decisions reversible.",
                LinkedArea = secondTask,
                WhatYouLearn =
                    $"You sharpen how {reviewTarget} changes implementation judgment instead of treating it as isolated study.",
                ProductImpact =
                    "This keeps Suite moving on a pressure area while preserving review-first discipline.",
                CareerValue =
                    "This becomes stronger proof that you can translate EE reasoning into safe software delivery choices.",
                RequiresApproval = true,
                CreatedAt = DateTimeOffset.Now,
            },
            new SuggestedAction
            {
                Title = $"Trace one learning loop into {secondTask}",
                SourceAgent = "Repo Coach",
                ActionType = "study_to_repo",
                Priority = "medium",
                Rationale =
                    $"Your current learning profile is already pushing toward {reviewTarget}, and {secondTask} is the cleanest place to apply it.",
                ExpectedBenefit =
                    "This turns study into implementation judgment and prevents disconnected practice.",
                LinkedArea = reviewTarget,
                WhatYouLearn =
                    $"You practice explaining how {reviewTarget} affects a real design or review decision.",
                ProductImpact =
                    "Suite gains sharper guardrails and clearer decision framing before any implementation begins.",
                CareerValue =
                    "You build a stronger story about domain-aware automation, not just feature work.",
                RequiresApproval = true,
                CreatedAt = DateTimeOffset.Now,
            },
            new SuggestedAction
            {
                Title = $"Capture the product angle behind {firstMonetization}",
                SourceAgent = "Repo Coach",
                ActionType = "career_proof",
                Priority = "medium",
                Rationale =
                    "The repo already hints at a narrow commercial angle, and you should convert current work into a proof point early.",
                ExpectedBenefit =
                    "You reduce wasted feature work by forcing each proposed task to map to operator value or career proof.",
                LinkedArea = firstMonetization,
                WhatYouLearn =
                    "You learn to connect technical work, user trust, and monetization instead of treating them as separate tracks.",
                ProductImpact =
                    "This helps keep Suite pointed at one credible job rather than broad platform sprawl.",
                CareerValue =
                    "You get a cleaner portfolio and interview narrative around electrical production-control workflows.",
                RequiresApproval = true,
                CreatedAt = DateTimeOffset.Now,
            },
        ];
    }

    private static string JoinOrNone(IReadOnlyList<string> items) =>
        items.Count == 0 ? "none recorded" : string.Join("; ", items);

    private sealed class SuiteCoachContract
    {
        public List<SuiteCoachSuggestionContract>? Suggestions { get; set; }
    }

    private sealed class SuiteCoachSuggestionContract
    {
        public string? Title { get; set; }
        public string? ActionType { get; set; }
        public string? Priority { get; set; }
        public string? Rationale { get; set; }
        public string? ExpectedBenefit { get; set; }
        public string? LinkedArea { get; set; }
        public string? WhatYouLearn { get; set; }
        public string? ProductImpact { get; set; }
        public string? CareerValue { get; set; }
        public bool RequiresApproval { get; set; } = true;
    }
}
