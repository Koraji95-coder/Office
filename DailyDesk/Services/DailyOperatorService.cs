using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class DailyOperatorService
{
    private readonly IModelProvider _modelProvider;
    private readonly string _model;

    public DailyOperatorService(IModelProvider modelProvider, string model)
    {
        _modelProvider = modelProvider;
        _model = model;
    }

    public async Task<DailyRunTemplate> CreateDailyRunAsync(
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
            var generated = await _modelProvider.GenerateJsonAsync<DailyRunContract>(
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
            if (converted is not null)
            {
                return converted;
            }
        }
        catch
        {
            // Fall back to deterministic planning.
        }

        return BuildFallbackDailyRun(snapshot, historySummary, learningProfile, operatorState);
    }

    private static string BuildSystemPrompt() =>
        """
        You are the Chief of Staff inside Daily Desk.
        Build one practical day plan for a user who is using this app as a career engine for electrical engineering growth.
        Keep the plan grounded in study, read-only Suite progress, and career proof.
        Return strict JSON only with:
        - objective
        - morningPlan
        - studyBlock
        - repoBlock
        - middayCheckpoint
        - endOfDayReview
        - carryoverQueue (3 to 5 items)
        Do not suggest repo mutation without saying it is a proposal requiring approval.
        """;

    private static string BuildUserPrompt(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        OperatorMemoryState operatorState
    ) =>
        $"""
        Build today's operator plan.

        Current Suite pulse:
        - status: {snapshot.StatusSummary}
        - hot areas: {JoinOrNone(snapshot.HotAreas)}
        - next session tasks: {JoinOrNone(snapshot.NextSessionTasks.Take(4).ToList())}
        - monetization leads: {JoinOrNone(snapshot.MonetizationMoves.Take(4).ToList())}

        Training memory:
        - practice summary: {historySummary.OverallSummary}
        - review queue: {historySummary.ReviewQueueSummary}
        - defense summary: {historySummary.DefenseSummary}
        - recent reflections: {JoinOrNone(historySummary.RecentReflections.Take(3).Select(item => $"{item.Mode} {item.Focus}: {item.Reflection}").ToList())}

        Learning profile:
        - summary: {learningProfile.Summary}
        - current need: {learningProfile.CurrentNeed}
        - active topics: {JoinOrNone(learningProfile.ActiveTopics.Take(6).ToList())}
        - imported knowledge: {JoinOrNone(learningLibrary.Documents.Take(4).Select(item => item.PromptSummary).ToList())}

        Operator memory:
        - pending approvals: {operatorState.PendingApprovalSuggestions.Count}
        - due watchlists: {operatorState.DueWatchlists.Count}
        - recent suggestion outcomes: {JoinOrNone(operatorState.RecentSuggestions.Where(item => !item.IsPending).Take(5).Select(item => $"{item.Title} => {item.StatusSummary}").ToList())}
        - recent activities: {JoinOrNone(operatorState.RecentActivities.Take(6).Select(item => item.DisplaySummary).ToList())}
        - policy summary: {JoinOrNone(operatorState.Policies.Select(item => item.DisplaySummary).ToList())}
        """;

    private DailyRunTemplate? ConvertContract(DailyRunContract? contract)
    {
        if (contract is null
            || string.IsNullOrWhiteSpace(contract.Objective)
            || string.IsNullOrWhiteSpace(contract.MorningPlan)
            || string.IsNullOrWhiteSpace(contract.StudyBlock)
            || string.IsNullOrWhiteSpace(contract.RepoBlock)
            || string.IsNullOrWhiteSpace(contract.EndOfDayReview))
        {
            return null;
        }

        return new DailyRunTemplate
        {
            DateKey = DateTime.Now.ToString("yyyy-MM-dd"),
            Objective = contract.Objective.Trim(),
            MorningPlan = contract.MorningPlan.Trim(),
            StudyBlock = contract.StudyBlock.Trim(),
            RepoBlock = contract.RepoBlock.Trim(),
            MiddayCheckpoint = string.IsNullOrWhiteSpace(contract.MiddayCheckpoint)
                ? "Check whether the current plan is still the highest-leverage use of time. If not, reduce scope instead of switching randomly."
                : contract.MiddayCheckpoint.Trim(),
            EndOfDayReview = contract.EndOfDayReview.Trim(),
            CarryoverQueue = contract.CarryoverQueue?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .Take(5)
                .ToList() ?? [],
            GenerationSource = $"{_modelProvider.ProviderId} via {_model}",
            GeneratedAt = DateTimeOffset.Now,
        };
    }

    private static DailyRunTemplate BuildFallbackDailyRun(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        OperatorMemoryState operatorState
    )
    {
        var topReview = historySummary.ReviewRecommendations.FirstOrDefault();
        var topSuiteTask = snapshot.NextSessionTasks.FirstOrDefault()
            ?? "Review the current Suite hotspot and turn it into one bounded next move.";
        var topApproval = operatorState.PendingApprovalSuggestions.FirstOrDefault()?.Title
            ?? "No approval items yet.";
        var carryover = new List<string>();

        if (topReview is not null)
        {
            carryover.Add($"Retest {topReview.Topic} and move it into oral-defense mode.");
        }

        carryover.Add(topSuiteTask);

        if (operatorState.DueWatchlists.Count > 0)
        {
            carryover.Add(
                $"Run due watchlist: {operatorState.DueWatchlists[0].Topic}."
            );
        }

        if (operatorState.PendingApprovalSuggestions.Count > 0)
        {
            carryover.Add($"Resolve pending approval: {topApproval}");
        }

        carryover.Add(
            "Capture one portfolio-quality sentence about what you learned today."
        );

        return new DailyRunTemplate
        {
            DateKey = DateTime.Now.ToString("yyyy-MM-dd"),
            Objective =
                "Identify the best study move, best read-only Suite move, and best career-proof move today.",
            MorningPlan =
                $"Run the chief pass, clear any approval blockers, and decide whether {topSuiteTask} still deserves the first repo block.",
            StudyBlock = topReview is null
                ? $"Generate one mixed practice set from {learningProfile.CurrentNeed}"
                : $"Focus on {topReview.Topic}. Retest it until the reasoning is stable, then explain how it changes a real decision in {snapshot.HotAreas.FirstOrDefault() ?? "the current Suite hotspot"}.",
            RepoBlock =
                $"Treat the next Suite block as read-only planning first: {topSuiteTask} Why it matters: {snapshot.HotAreas.FirstOrDefault() ?? "current product pressure"} needs bounded, reliable progress.",
            MiddayCheckpoint =
                "At midday, confirm you completed the study block or the repo block. If neither moved, reduce scope and finish one clean unit before switching contexts.",
            EndOfDayReview =
                "At the end of the day, record one thing you learned, one thing you proved about Suite, and one suggestion you accepted, rejected, or deferred.",
            CarryoverQueue = carryover.Take(5).ToList(),
            GenerationSource = "fallback daily operator",
            GeneratedAt = DateTimeOffset.Now,
        };
    }

    private static string JoinOrNone(IReadOnlyList<string> items) =>
        items.Count == 0 ? "none recorded" : string.Join("; ", items);

    private sealed class DailyRunContract
    {
        public string? Objective { get; set; }
        public string? MorningPlan { get; set; }
        public string? StudyBlock { get; set; }
        public string? RepoBlock { get; set; }
        public string? MiddayCheckpoint { get; set; }
        public string? EndOfDayReview { get; set; }
        public List<string>? CarryoverQueue { get; set; }
    }
}
