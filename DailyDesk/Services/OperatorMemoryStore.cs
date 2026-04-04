using System.IO;
using System.Text.Json;
using DailyDesk.Models;
using LiteDB;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace DailyDesk.Services;

public sealed class OperatorMemoryStore
{
    private readonly string _storePath;
    private readonly OfficeDatabase? _db;
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public OperatorMemoryStore(string? stateRootPath = null, OfficeDatabase? db = null)
    {
        var root = string.IsNullOrWhiteSpace(stateRootPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyDesk"
            )
            : Path.GetFullPath(stateRootPath);
        Directory.CreateDirectory(root);
        _storePath = Path.Combine(root, "operator-memory.json");
        _db = db;

        if (_db is not null)
        {
            MigrateFromJsonIfNeeded();
        }
    }

    public async Task<OperatorMemoryState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_db is not null)
        {
            return LoadFromDb();
        }

        var state = await LoadStateAsync(cancellationToken);
        NormalizeState(state);
        return state;
    }

    private OperatorMemoryState LoadFromDb()
    {
        var state = new OperatorMemoryState
        {
            Policies = _db!.Policies.FindAll().ToList(),
            Watchlists = _db.Watchlists.FindAll().ToList(),
            Suggestions = _db.Suggestions.Query().OrderByDescending(x => x.CreatedAt).Limit(240).ToList(),
            Activities = _db.Activities.Query().OrderByDescending(x => x.OccurredAt).Limit(600).ToList(),
            DailyRuns = _db.DailyRuns.Query().OrderByDescending(x => x.DateKey).Limit(30).ToList(),
            DeskThreads = _db.DeskThreads.FindAll().ToList(),
        };

        // Load workflow from BsonDocument collection
        var workflowDoc = _db.Workflow.FindById("current");
        if (workflowDoc is not null)
        {
            state.Workflow = BsonMapper.Global.Deserialize<OfficeWorkflowState>(workflowDoc);
        }

        NormalizeState(state);
        return state;
    }

    public async Task<OperatorMemoryState> SavePoliciesAsync(
        IReadOnlyList<AgentPolicy> policies,
        CancellationToken cancellationToken = default
    )
    {
        if (_db is not null)
        {
            _db.Policies.DeleteAll();
            foreach (var policy in policies) _db.Policies.Insert(ClonePolicy(policy));
            return LoadFromDb();
        }

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
        if (_db is not null)
        {
            _db.Watchlists.DeleteAll();
            foreach (var w in watchlists) _db.Watchlists.Insert(CloneWatchlist(w));
            return LoadFromDb();
        }

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
        if (_db is not null)
        {
            foreach (var suggestion in suggestions)
            {
                var existing = _db.Suggestions.FindOne(x => x.Id == suggestion.Id);
                if (existing is not null)
                {
                    _db.Suggestions.Update(CloneSuggestion(suggestion));
                }
                else
                {
                    _db.Suggestions.Insert(CloneSuggestion(suggestion));
                }
            }
            // Enforce max limit
            var count = _db.Suggestions.Count();
            if (count > 240)
            {
                var toRemove = _db.Suggestions.Query()
                    .OrderBy(x => x.CreatedAt)
                    .Limit(count - 240)
                    .ToList();
                foreach (var item in toRemove) _db.Suggestions.Delete(item.Id);
            }
            return LoadFromDb();
        }

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
        if (_db is not null)
        {
            var target = _db.Suggestions.FindOne(x => x.Id == suggestionId);
            if (target is not null)
            {
                target.Outcome = CloneOutcome(outcome);
                _db.Suggestions.Update(target);
            }
            return LoadFromDb();
        }

        var state = await LoadStateAsync(cancellationToken);
        var stateTarget = state.Suggestions.FirstOrDefault(item =>
            item.Id.Equals(suggestionId, StringComparison.OrdinalIgnoreCase)
        );
        if (stateTarget is not null)
        {
            stateTarget.Outcome = CloneOutcome(outcome);
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
        if (_db is not null)
        {
            var target = _db.Suggestions.FindOne(x => x.Id == suggestionId);
            if (target is not null)
            {
                target.ExecutionStatus = executionStatus;
                target.ExecutionSummary = executionSummary;
                target.ExecutionUpdatedAt = DateTimeOffset.Now;
                _db.Suggestions.Update(target);
            }
            return LoadFromDb();
        }

        var state = await LoadStateAsync(cancellationToken);
        var stateTarget = state.Suggestions.FirstOrDefault(item =>
            item.Id.Equals(suggestionId, StringComparison.OrdinalIgnoreCase)
        );
        if (stateTarget is not null)
        {
            stateTarget.ExecutionStatus = executionStatus;
            stateTarget.ExecutionSummary = executionSummary;
            stateTarget.ExecutionUpdatedAt = DateTimeOffset.Now;
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
        if (_db is not null)
        {
            var target = _db.Suggestions.FindOne(x => x.Id == suggestionId);
            if (target is not null)
            {
                target.LatestResultSummary = latestResultSummary;
                target.LatestResultDetail = latestResultDetail;
                target.LatestResultSources = latestResultSources.ToList();
                target.LatestResultPath = string.IsNullOrWhiteSpace(latestResultPath) ? string.Empty : latestResultPath.Trim();
                target.ExecutionUpdatedAt = DateTimeOffset.Now;
                _db.Suggestions.Update(target);
            }
            return LoadFromDb();
        }

        var state = await LoadStateAsync(cancellationToken);
        var stateTarget = state.Suggestions.FirstOrDefault(item =>
            item.Id.Equals(suggestionId, StringComparison.OrdinalIgnoreCase)
        );
        if (stateTarget is not null)
        {
            stateTarget.LatestResultSummary = latestResultSummary;
            stateTarget.LatestResultDetail = latestResultDetail;
            stateTarget.LatestResultSources = latestResultSources.ToList();
            stateTarget.LatestResultPath = string.IsNullOrWhiteSpace(latestResultPath)
                ? string.Empty
                : latestResultPath.Trim();
            stateTarget.ExecutionUpdatedAt = DateTimeOffset.Now;
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
        if (_db is not null)
        {
            // Remove existing run for same date
            var existing = _db.DailyRuns.FindOne(x => x.DateKey == dailyRun.DateKey);
            if (existing is not null)
            {
                _db.DailyRuns.Delete(existing.DateKey);
            }
            _db.DailyRuns.Insert(CloneDailyRun(dailyRun));
            // Enforce 30-item limit
            var count = _db.DailyRuns.Count();
            if (count > 30)
            {
                var oldest = _db.DailyRuns.Query().OrderBy(x => x.DateKey).Limit(count - 30).ToList();
                foreach (var item in oldest) _db.DailyRuns.Delete(item.DateKey);
            }
            return LoadFromDb();
        }

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
        if (_db is not null)
        {
            _db.DeskThreads.DeleteAll();
            foreach (var thread in deskThreads) _db.DeskThreads.Insert(CloneDeskThread(thread));
            return LoadFromDb();
        }

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
        if (_db is not null)
        {
            var doc = LiteDB.BsonMapper.Global.Serialize(CloneWorkflow(workflow));
            doc.AsDocument["_id"] = "current";
            _db.Workflow.Upsert(doc.AsDocument);
            return LoadFromDb();
        }

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
        if (_db is not null)
        {
            // Save each section to its collection
            _db.Policies.DeleteAll();
            foreach (var p in state.Policies) _db.Policies.Insert(ClonePolicy(p));
            _db.Watchlists.DeleteAll();
            foreach (var w in state.Watchlists) _db.Watchlists.Insert(CloneWatchlist(w));
            _db.DeskThreads.DeleteAll();
            foreach (var t in state.DeskThreads) _db.DeskThreads.Insert(CloneDeskThread(t));

            if (state.Workflow is not null)
            {
                var doc = LiteDB.BsonMapper.Global.Serialize(CloneWorkflow(state.Workflow));
                doc.AsDocument["_id"] = "current";
                _db.Workflow.Upsert(doc.AsDocument);
            }

            return LoadFromDb();
        }

        NormalizeState(state);
        await SaveStateAsync(state, cancellationToken);
        return state;
    }

    public async Task<OperatorMemoryState> ResetAsync(CancellationToken cancellationToken = default)
    {
        if (_db is not null)
        {
            _db.Policies.DeleteAll();
            _db.Watchlists.DeleteAll();
            _db.Suggestions.DeleteAll();
            _db.Activities.DeleteAll();
            _db.DailyRuns.DeleteAll();
            _db.DeskThreads.DeleteAll();
            _db.Workflow.DeleteAll();
            var defaultState = BuildDefaultState();
            foreach (var p in defaultState.Policies) _db.Policies.Insert(p);
            foreach (var t in defaultState.DeskThreads) _db.DeskThreads.Insert(t);
            return LoadFromDb();
        }

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
        if (_db is not null)
        {
            foreach (var activity in activities)
            {
                _db.Activities.Insert(CloneActivity(activity));
            }
            // Enforce 600-item limit
            var count = _db.Activities.Count();
            if (count > 600)
            {
                var oldestDocs = _db.Activities.Query()
                    .OrderBy(x => x.OccurredAt)
                    .Limit(count - 600)
                    .ToDocuments()
                    .Select(doc => doc["_id"])
                    .ToList();
                foreach (var id in oldestDocs) _db.Activities.Delete(id);
            }
            return LoadFromDb();
        }

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

    private void MigrateFromJsonIfNeeded()
    {
        if (_db!.HasMigrated("operator-memory")) return;
        if (!File.Exists(_storePath)) { _db.MarkMigrated("operator-memory"); return; }

        try
        {
            var json = File.ReadAllText(_storePath);
            var state = JsonSerializer.Deserialize<OperatorMemoryState>(json, _jsonOptions)
                ?? BuildDefaultState();

            foreach (var p in state.Policies) _db.Policies.Insert(p);
            foreach (var w in state.Watchlists) _db.Watchlists.Insert(w);
            foreach (var s in state.Suggestions) _db.Suggestions.Insert(s);
            foreach (var a in state.Activities) _db.Activities.Insert(a);
            foreach (var d in state.DailyRuns) _db.DailyRuns.Insert(d);
            foreach (var t in state.DeskThreads) _db.DeskThreads.Insert(t);

            if (state.Workflow is not null)
            {
                var doc = LiteDB.BsonMapper.Global.Serialize(state.Workflow);
                doc.AsDocument["_id"] = "current";
                _db.Workflow.Upsert(doc.AsDocument);
            }

            _db.MarkMigrated("operator-memory");

            var migratedPath = _storePath + ".migrated";
            if (!File.Exists(migratedPath))
            {
                File.Move(_storePath, migratedPath);
            }
        }
        catch
        {
            // Migration failure is non-fatal.
        }
    }
}
