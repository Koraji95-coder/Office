using System.IO;
using System.Text.Json;
using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class OperatorMemoryStore
{
    private readonly string _storePath;
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public OperatorMemoryStore(string? stateRootPath = null)
    {
        var root = string.IsNullOrWhiteSpace(stateRootPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyDesk"
            )
            : Path.GetFullPath(stateRootPath);
        Directory.CreateDirectory(root);
        _storePath = Path.Combine(root, "operator-memory.json");
    }

    public async Task<OperatorMemoryState> LoadAsync(CancellationToken cancellationToken = default)
    {
        var state = await LoadStateAsync(cancellationToken);
        NormalizeState(state);
        return state;
    }

    public async Task<OperatorMemoryState> SavePoliciesAsync(
        IReadOnlyList<AgentPolicy> policies,
        CancellationToken cancellationToken = default
    )
    {
        var state = await LoadStateAsync(cancellationToken);
        state.Policies = policies.Select(ClonePolicy).ToList();
        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> SaveWatchlistsAsync(
        IReadOnlyList<ResearchWatchlist> watchlists,
        CancellationToken cancellationToken = default
    )
    {
        var state = await LoadStateAsync(cancellationToken);
        state.Watchlists = watchlists.Select(CloneWatchlist).ToList();
        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> UpsertSuggestionsAsync(
        IReadOnlyList<SuggestedAction> suggestions,
        CancellationToken cancellationToken = default
    )
    {
        var state = await LoadStateAsync(cancellationToken);
        foreach (var suggestion in suggestions)
        {
            var existingIndex = state.Suggestions.FindIndex(item =>
                item.Id.Equals(suggestion.Id, StringComparison.OrdinalIgnoreCase)
            );
            if (existingIndex >= 0)
            {
                state.Suggestions[existingIndex] = CloneSuggestion(suggestion);
            }
            else
            {
                state.Suggestions.Insert(0, CloneSuggestion(suggestion));
            }
        }

        state.Suggestions = state.Suggestions
            .OrderByDescending(item => item.CreatedAt)
            .Take(240)
            .ToList();

        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> RecordSuggestionOutcomeAsync(
        string suggestionId,
        SuggestionOutcome outcome,
        CancellationToken cancellationToken = default
    )
    {
        var state = await LoadStateAsync(cancellationToken);
        var target = state.Suggestions.FirstOrDefault(item =>
            item.Id.Equals(suggestionId, StringComparison.OrdinalIgnoreCase)
        );
        if (target is not null)
        {
            target.Outcome = CloneOutcome(outcome);
        }

        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> UpdateSuggestionExecutionAsync(
        string suggestionId,
        string executionStatus,
        string executionSummary,
        CancellationToken cancellationToken = default
    )
    {
        var state = await LoadStateAsync(cancellationToken);
        var target = state.Suggestions.FirstOrDefault(item =>
            item.Id.Equals(suggestionId, StringComparison.OrdinalIgnoreCase)
        );
        if (target is not null)
        {
            target.ExecutionStatus = executionStatus;
            target.ExecutionSummary = executionSummary;
            target.ExecutionUpdatedAt = DateTimeOffset.Now;
        }

        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> UpdateSuggestionResearchResultAsync(
        string suggestionId,
        string latestResultSummary,
        string latestResultDetail,
        IReadOnlyList<string> latestResultSources,
        string? latestResultPath,
        CancellationToken cancellationToken = default
    )
    {
        var state = await LoadStateAsync(cancellationToken);
        var target = state.Suggestions.FirstOrDefault(item =>
            item.Id.Equals(suggestionId, StringComparison.OrdinalIgnoreCase)
        );
        if (target is not null)
        {
            target.LatestResultSummary = latestResultSummary;
            target.LatestResultDetail = latestResultDetail;
            target.LatestResultSources = latestResultSources.ToList();
            target.LatestResultPath = string.IsNullOrWhiteSpace(latestResultPath)
                ? string.Empty
                : latestResultPath.Trim();
            target.ExecutionUpdatedAt = DateTimeOffset.Now;
        }

        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> SaveDailyRunAsync(
        DailyRunTemplate dailyRun,
        CancellationToken cancellationToken = default
    )
    {
        var state = await LoadStateAsync(cancellationToken);
        state.DailyRuns.RemoveAll(item =>
            item.DateKey.Equals(dailyRun.DateKey, StringComparison.OrdinalIgnoreCase)
        );
        state.DailyRuns.Insert(0, CloneDailyRun(dailyRun));
        state.DailyRuns = state.DailyRuns
            .OrderByDescending(item => item.DateKey)
            .ThenByDescending(item => item.GeneratedAt)
            .Take(30)
            .ToList();

        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> SaveDeskThreadsAsync(
        IReadOnlyList<DeskThreadState> deskThreads,
        CancellationToken cancellationToken = default
    )
    {
        var state = await LoadStateAsync(cancellationToken);
        state.DeskThreads = deskThreads.Select(CloneDeskThread).ToList();
        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> SaveWorkflowAsync(
        OfficeWorkflowState workflow,
        CancellationToken cancellationToken = default
    )
    {
        var state = await LoadStateAsync(cancellationToken);
        state.Workflow = CloneWorkflow(workflow);
        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> SaveSnapshotAsync(
        OperatorMemoryState state,
        CancellationToken cancellationToken = default
    )
    {
        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> ResetAsync(CancellationToken cancellationToken = default)
    {
        var state = BuildDefaultState();
        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> RecordActivityAsync(
        OperatorActivityRecord activity,
        CancellationToken cancellationToken = default
    )
    {
        return await RecordActivitiesAsync([activity], cancellationToken);
    }

    public async Task<OperatorMemoryState> RecordActivitiesAsync(
        IReadOnlyList<OperatorActivityRecord> activities,
        CancellationToken cancellationToken = default
    )
    {
        var state = await LoadStateAsync(cancellationToken);
        foreach (var activity in activities)
        {
            state.Activities.Insert(0, CloneActivity(activity));
        }

        state.Activities = state.Activities
            .OrderByDescending(item => item.OccurredAt)
            .Take(600)
            .ToList();

        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    private async Task<OperatorMemoryState> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storePath))
        {
            return BuildDefaultState();
        }

        try
        {
            var payload = await File.ReadAllTextAsync(_storePath, cancellationToken);
            return JsonSerializer.Deserialize<OperatorMemoryState>(payload, _jsonOptions)
                ?? BuildDefaultState();
        }
        catch
        {
            return BuildDefaultState();
        }
    }

    private async Task SaveStateAsync(
        OperatorMemoryState state,
        CancellationToken cancellationToken
    )
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await File.WriteAllTextAsync(_storePath, json, cancellationToken);
    }

    private static void NormalizeState(OperatorMemoryState state)
    {
        if (state.Policies.Count == 0)
        {
            state.Policies = BuildDefaultPolicies();
        }

        if (state.Watchlists.Count == 0)
        {
            state.Watchlists = BuildDefaultWatchlists();
        }

        state.Suggestions ??= [];
        state.Activities ??= [];
        state.DailyRuns ??= [];
        state.DeskThreads ??= [];
        state.Suggestions = state.Suggestions
            .OrderByDescending(item => item.ExecutionUpdatedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.CreatedAt)
            .GroupBy(BuildSuggestionSemanticKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.ExecutionUpdatedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.CreatedAt)
            .ToList();

        if (state.DeskThreads.Count == 0)
        {
            state.DeskThreads = BuildDefaultDeskThreads();
        }

        state.Workflow ??= new OfficeWorkflowState();
    }

    private static OperatorMemoryState BuildDefaultState() =>
        new()
        {
            Policies = BuildDefaultPolicies(),
            Watchlists = BuildDefaultWatchlists(),
            DeskThreads = BuildDefaultDeskThreads(),
            Workflow = new OfficeWorkflowState(),
        };

    private static List<AgentPolicy> BuildDefaultPolicies() =>
    [
        new()
        {
            Role = "Chief of Staff",
            AutonomyLevel = "Autonomous Prep",
            RequiresApproval = false,
            ReviewCadence = "Daily",
            AllowedActionClasses =
            [
                "daily planning",
                "queue routing",
                "prep synthesis",
                "career routing",
            ],
        },
        new()
        {
            Role = "EE Mentor",
            AutonomyLevel = "Autonomous Prep",
            RequiresApproval = false,
            ReviewCadence = "Daily",
            AllowedActionClasses =
            [
                "training prep",
                "challenge generation",
                "explanation coaching",
                "oral-defense prep",
            ],
        },
        new()
        {
            Role = "Test Builder",
            AutonomyLevel = "Autonomous Prep",
            RequiresApproval = false,
            ReviewCadence = "Daily",
            AllowedActionClasses =
            [
                "practice generation",
                "difficulty routing",
                "review scheduling",
            ],
        },
        new()
        {
            Role = "Repo Coach",
            AutonomyLevel = "Prepare",
            RequiresApproval = true,
            ReviewCadence = "Daily",
            AllowedActionClasses =
            [
                "repo analysis",
                "implementation proposals",
                "learning tie-ins",
            ],
        },
        new()
        {
            Role = "Business Strategist",
            AutonomyLevel = "Prepare",
            RequiresApproval = true,
            ReviewCadence = "Weekly",
            AllowedActionClasses =
            [
                "offer framing",
                "pilot definition",
                "market validation",
            ],
        },
    ];

    private static List<ResearchWatchlist> BuildDefaultWatchlists() =>
        [];

    private static List<DeskThreadState> BuildDefaultDeskThreads()
    {
        var createdAt = DateTimeOffset.Now;
        return
        [
            BuildDefaultDeskThread(
                "chief",
                "Chief of Staff",
                "I route the day across Suite, engineering, CAD, and business. Ask for a brief, a plan, or a synthesis."
            ),
            BuildDefaultDeskThread(
                "engineering",
                "Engineering Desk",
                "I combine EE coaching, CAD workflow judgment, and training prep. Ask for explanations, drills, design reasoning, or drafting review guidance."
            ),
            BuildDefaultDeskThread(
                "suite",
                "Suite Context",
                "I keep the office aware of Suite trust, availability, and workflow context in a calm, read-only way."
            ),
            BuildDefaultDeskThread(
                "business",
                "Business Ops",
                "I translate current capability into internal operating discipline, offers, proof points, and monetization paths without hype."
            ),
        ];

        DeskThreadState BuildDefaultDeskThread(string id, string title, string intro) =>
            new()
            {
                DeskId = id,
                DeskTitle = title,
                UpdatedAt = createdAt,
                Messages =
                [
                    new DeskMessageRecord
                    {
                        DeskId = id,
                        Role = "assistant",
                        Author = title,
                        Kind = "system",
                        Content = intro,
                        CreatedAt = createdAt,
                    },
                ],
            };
    }

    private static AgentPolicy ClonePolicy(AgentPolicy policy) =>
        new()
        {
            Role = policy.Role,
            AutonomyLevel = policy.AutonomyLevel,
            RequiresApproval = policy.RequiresApproval,
            ReviewCadence = policy.ReviewCadence,
            AllowedActionClasses = [.. policy.AllowedActionClasses],
        };

    private static ResearchWatchlist CloneWatchlist(ResearchWatchlist watchlist) =>
        new()
        {
            Id = watchlist.Id,
            Topic = watchlist.Topic,
            Query = watchlist.Query,
            Frequency = watchlist.Frequency,
            PreferredPerspective = watchlist.PreferredPerspective,
            SaveToKnowledgeDefault = watchlist.SaveToKnowledgeDefault,
            IsEnabled = watchlist.IsEnabled,
            LastRunAt = watchlist.LastRunAt,
        };

    private static SuggestedAction CloneSuggestion(SuggestedAction suggestion) =>
        new()
        {
            Id = suggestion.Id,
            Title = suggestion.Title,
            SourceAgent = suggestion.SourceAgent,
            ActionType = suggestion.ActionType,
            Priority = suggestion.Priority,
            Rationale = suggestion.Rationale,
            ExpectedBenefit = suggestion.ExpectedBenefit,
            LinkedArea = suggestion.LinkedArea,
            WhatYouLearn = suggestion.WhatYouLearn,
            ProductImpact = suggestion.ProductImpact,
            CareerValue = suggestion.CareerValue,
            RequiresApproval = suggestion.RequiresApproval,
            CreatedAt = suggestion.CreatedAt,
            Outcome = suggestion.Outcome is null ? null : CloneOutcome(suggestion.Outcome),
            ExecutionStatus = suggestion.ExecutionStatus,
            ExecutionSummary = suggestion.ExecutionSummary,
            ExecutionUpdatedAt = suggestion.ExecutionUpdatedAt,
            LatestResultSummary = suggestion.LatestResultSummary,
            LatestResultDetail = suggestion.LatestResultDetail,
            LatestResultSources = suggestion.LatestResultSources.ToList(),
            LatestResultPath = suggestion.LatestResultPath,
        };

    private static string BuildSuggestionSemanticKey(SuggestedAction suggestion) =>
        string.Join(
            "|",
            suggestion.SourceAgent.Trim(),
            suggestion.ActionType.Trim(),
            suggestion.LinkedArea.Trim(),
            suggestion.Title.Trim()
        );

    private static DeskThreadState CloneDeskThread(DeskThreadState thread) =>
        new()
        {
            DeskId = thread.DeskId,
            DeskTitle = thread.DeskTitle,
            UpdatedAt = thread.UpdatedAt,
            Messages = thread.Messages.Select(CloneDeskMessage).ToList(),
        };

    private static OfficeWorkflowState CloneWorkflow(OfficeWorkflowState workflow) =>
        new()
        {
            ActiveRoute = workflow.ActiveRoute,
            LastAutoRoute = workflow.LastAutoRoute,
            LastAutoRouteReason = workflow.LastAutoRouteReason,
            StudyFocus = workflow.StudyFocus,
            PracticeDifficulty = workflow.PracticeDifficulty,
            PracticeQuestionCount = workflow.PracticeQuestionCount,
            CurrentPracticeTest = workflow.CurrentPracticeTest,
            CurrentDefenseScenario = workflow.CurrentDefenseScenario,
            LatestDefenseEvaluation = workflow.LatestDefenseEvaluation,
            LatestResearchReport = workflow.LatestResearchReport,
            LastScoredSessionMode = workflow.LastScoredSessionMode,
            LastScoredSessionFocus = workflow.LastScoredSessionFocus,
            ReflectionContextSummary = workflow.ReflectionContextSummary,
            PracticeGeneratedAt = workflow.PracticeGeneratedAt,
            PracticeScoredAt = workflow.PracticeScoredAt,
            DefenseGeneratedAt = workflow.DefenseGeneratedAt,
            DefenseScoredAt = workflow.DefenseScoredAt,
            ReflectionSavedAt = workflow.ReflectionSavedAt,
            UpdatedAt = workflow.UpdatedAt,
        };

    private static DeskMessageRecord CloneDeskMessage(DeskMessageRecord message) =>
        new()
        {
            Id = message.Id,
            DeskId = message.DeskId,
            Role = message.Role,
            Author = message.Author,
            Kind = message.Kind,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
        };

    private static SuggestionOutcome CloneOutcome(SuggestionOutcome outcome) =>
        new()
        {
            Status = outcome.Status,
            Reason = outcome.Reason,
            OutcomeNote = outcome.OutcomeNote,
            RecordedAt = outcome.RecordedAt,
        };

    private static DailyRunTemplate CloneDailyRun(DailyRunTemplate dailyRun) =>
        new()
        {
            DateKey = dailyRun.DateKey,
            Objective = dailyRun.Objective,
            MorningPlan = dailyRun.MorningPlan,
            StudyBlock = dailyRun.StudyBlock,
            RepoBlock = dailyRun.RepoBlock,
            MiddayCheckpoint = dailyRun.MiddayCheckpoint,
            EndOfDayReview = dailyRun.EndOfDayReview,
            CarryoverQueue = [.. dailyRun.CarryoverQueue],
            GenerationSource = dailyRun.GenerationSource,
            GeneratedAt = dailyRun.GeneratedAt,
        };

    private static OperatorActivityRecord CloneActivity(OperatorActivityRecord activity) =>
        new()
        {
            EventType = activity.EventType,
            Agent = activity.Agent,
            Topic = activity.Topic,
            Summary = activity.Summary,
            OccurredAt = activity.OccurredAt,
        };
}
