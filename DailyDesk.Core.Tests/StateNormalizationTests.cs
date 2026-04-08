using System.IO;
using System.Text.Json;
using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Unit tests for the state normalization logic in OfficeSessionStateStore.Normalize()
/// and OperatorMemoryStore.NormalizeState(), exercised through their public LoadAsync /
/// ResetAsync APIs using JSON-file fallback (no LiteDB) to avoid database contention.
/// </summary>
public sealed class StateNormalizationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    // =========================================================================
    // OfficeSessionStateStore.Normalize() – via LoadAsync / ResetAsync
    // =========================================================================

    [Fact]
    public async Task OfficeSession_Normalize_WhitespaceFocus_SetsDefault()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { Focus = "   " });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal("Protection, grounding, standards, drafting safety", state.Focus);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_Normalize_ExistingFocus_TrimsAndPreserves()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { Focus = "  Grounding  " });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal("Grounding", state.Focus);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_Normalize_WhitespaceFocusReason_SetsDefault()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { FocusReason = "   " });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal(
                "Set a focus manually or start from a review target to begin a guided session.",
                state.FocusReason
            );
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_Normalize_ExistingFocusReason_TrimsAndPreserves()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { FocusReason = "  Custom reason  " });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal("Custom reason", state.FocusReason);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_Normalize_WhitespaceDifficulty_SetsDefault()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { Difficulty = "" });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal("Mixed", state.Difficulty);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_Normalize_ExistingDifficulty_TrimsAndPreserves()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { Difficulty = "  Hard  " });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal("Hard", state.Difficulty);
        }
        finally { CleanUp(tempDir); }
    }

    [Theory]
    [InlineData(0, 3)]   // below min -> clamped to 3
    [InlineData(2, 3)]   // below min -> clamped to 3
    [InlineData(3, 3)]   // at min -> unchanged
    [InlineData(7, 7)]   // in range -> unchanged
    [InlineData(10, 10)] // at max -> unchanged
    [InlineData(11, 10)] // above max -> clamped to 10
    [InlineData(99, 10)] // well above max -> clamped to 10
    public async Task OfficeSession_Normalize_QuestionCount_Clamped(int input, int expected)
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { QuestionCount = input });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal(expected, state.QuestionCount);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_Normalize_EmptyPracticeResultSummary_SetsDefault()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { PracticeResultSummary = "" });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal("No scored practice yet.", state.PracticeResultSummary);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_Normalize_EmptyDefenseScoreSummary_SetsDefault()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { DefenseScoreSummary = "" });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal("No scored oral-defense answer yet.", state.DefenseScoreSummary);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_Normalize_EmptyDefenseFeedbackSummary_SetsDefault()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { DefenseFeedbackSummary = "" });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal(
                "Score a typed answer to get rubric feedback and follow-up coaching.",
                state.DefenseFeedbackSummary
            );
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_Normalize_EmptyReflectionContextSummary_SetsDefault()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { ReflectionContextSummary = "" });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal(
                "Score a practice or defense session to save a reflection.",
                state.ReflectionContextSummary
            );
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_Normalize_UnknownRoute_NormalizesToChiefRoute()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { CurrentRoute = "unknown-route" });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal(OfficeRouteCatalog.ChiefRoute, state.CurrentRoute);
        }
        finally { CleanUp(tempDir); }
    }

    [Theory]
    [InlineData(OfficeRouteCatalog.ChiefRoute)]
    [InlineData(OfficeRouteCatalog.EngineeringRoute)]
    [InlineData(OfficeRouteCatalog.SuiteRoute)]
    [InlineData(OfficeRouteCatalog.BusinessRoute)]
    [InlineData(OfficeRouteCatalog.MLRoute)]
    public async Task OfficeSession_Normalize_KnownRoute_Preserved(string route)
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            WriteSessionJson(tempDir, new OfficeLiveSessionState { CurrentRoute = route });

            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.LoadAsync();

            Assert.Equal(route, state.CurrentRoute);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OfficeSession_ResetAsync_ReturnsNormalizedDefaultState()
    {
        var tempDir = MakeTempDir("session-norm");
        try
        {
            var store = new OfficeSessionStateStore(tempDir);
            var state = await store.ResetAsync();

            Assert.Equal(OfficeRouteCatalog.ChiefRoute, state.CurrentRoute);
            Assert.Equal("Protection, grounding, standards, drafting safety", state.Focus);
            Assert.Equal("Mixed", state.Difficulty);
            Assert.InRange(state.QuestionCount, 3, 10);
            Assert.NotNull(state.ActiveDefenseScenario);
        }
        finally { CleanUp(tempDir); }
    }

    // =========================================================================
    // OperatorMemoryStore.NormalizeState() – via LoadAsync / ResetAsync
    // =========================================================================

    [Fact]
    public async Task OperatorMemory_NormalizeState_EmptyPolicies_SetsDefaultPolicies()
    {
        var tempDir = MakeTempDir("mem-norm");
        try
        {
            WriteOperatorMemoryJson(tempDir, new OperatorMemoryState { Policies = [] });

            var store = new OperatorMemoryStore(tempDir, db: null);
            var state = await store.LoadAsync();

            Assert.NotEmpty(state.Policies);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OperatorMemory_NormalizeState_NonEmptyPolicies_PreservesThem()
    {
        var tempDir = MakeTempDir("mem-norm");
        try
        {
            var policies = new List<AgentPolicy>
            {
                new() { Role = "Custom Role", AutonomyLevel = "Manual" },
            };
            WriteOperatorMemoryJson(tempDir, new OperatorMemoryState { Policies = policies });

            var store = new OperatorMemoryStore(tempDir, db: null);
            var state = await store.LoadAsync();

            Assert.Single(state.Policies);
            Assert.Equal("Custom Role", state.Policies[0].Role);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OperatorMemory_NormalizeState_EmptyWatchlists_LeavesNotNullList()
    {
        var tempDir = MakeTempDir("mem-norm");
        try
        {
            WriteOperatorMemoryJson(tempDir, new OperatorMemoryState
            {
                Policies = [new() { Role = "X", AutonomyLevel = "Manual" }],
                Watchlists = [],
            });

            var store = new OperatorMemoryStore(tempDir, db: null);
            var state = await store.LoadAsync();

            // Default watchlists are an empty list; normalization must not null it
            Assert.NotNull(state.Watchlists);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OperatorMemory_NormalizeState_EmptyDeskThreads_SetsDefaultThreads()
    {
        var tempDir = MakeTempDir("mem-norm");
        try
        {
            WriteOperatorMemoryJson(tempDir, new OperatorMemoryState
            {
                Policies = [new() { Role = "X", AutonomyLevel = "Manual" }],
                DeskThreads = [],
            });

            var store = new OperatorMemoryStore(tempDir, db: null);
            var state = await store.LoadAsync();

            Assert.NotEmpty(state.DeskThreads);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OperatorMemory_NormalizeState_NonEmptyDeskThreads_PreservesThem()
    {
        var tempDir = MakeTempDir("mem-norm");
        try
        {
            var memState = new OperatorMemoryState
            {
                Policies = [new() { Role = "X", AutonomyLevel = "Manual" }],
                DeskThreads =
                [
                    new() { DeskId = "custom-desk", DeskTitle = "Custom Desk", Messages = [] },
                ],
            };
            WriteOperatorMemoryJson(tempDir, memState);

            var store = new OperatorMemoryStore(tempDir, db: null);
            var state = await store.LoadAsync();

            Assert.Single(state.DeskThreads);
            Assert.Equal("custom-desk", state.DeskThreads[0].DeskId);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OperatorMemory_NormalizeState_NullWorkflow_SetsDefaultWorkflow()
    {
        var tempDir = MakeTempDir("mem-norm");
        try
        {
            // Write raw JSON with "Workflow": null to force the null path
            const string json =
                """{"Policies":[{"Role":"X","AutonomyLevel":"Manual","RequiresApproval":false,"ReviewCadence":"Daily","AllowedActionClasses":[]}],"Suggestions":[],"Watchlists":[],"DailyRuns":[],"Activities":[],"DeskThreads":[{"DeskId":"d1","DeskTitle":"D","UpdatedAt":"2024-01-01T00:00:00+00:00","Messages":[]}],"Workflow":null}""";
            File.WriteAllText(Path.Combine(tempDir, "operator-memory.json"), json);

            var store = new OperatorMemoryStore(tempDir, db: null);
            var state = await store.LoadAsync();

            Assert.NotNull(state.Workflow);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OperatorMemory_NormalizeState_DuplicateSuggestions_DeduplicatesBySemanticKey()
    {
        var tempDir = MakeTempDir("mem-norm");
        try
        {
            var t = DateTimeOffset.UtcNow;
            // Two suggestions with identical semantic key (same agent, type, area, title)
            var suggestions = new List<SuggestedAction>
            {
                new() { Id = "s1", SourceAgent = "Agent", ActionType = "review", LinkedArea = "EE", Title = "Test", CreatedAt = t },
                new() { Id = "s2", SourceAgent = "Agent", ActionType = "review", LinkedArea = "EE", Title = "Test", CreatedAt = t.AddSeconds(-1) },
            };
            var memState = new OperatorMemoryState
            {
                Policies = [new() { Role = "X", AutonomyLevel = "Manual" }],
                DeskThreads = [new() { DeskId = "d", DeskTitle = "D", Messages = [] }],
                Suggestions = suggestions,
            };
            WriteOperatorMemoryJson(tempDir, memState);

            var store = new OperatorMemoryStore(tempDir, db: null);
            var state = await store.LoadAsync();

            // After deduplication the two identical-key suggestions collapse to one
            Assert.Single(state.Suggestions);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OperatorMemory_NormalizeState_DistinctSuggestions_AllPreserved()
    {
        var tempDir = MakeTempDir("mem-norm");
        try
        {
            var t = DateTimeOffset.UtcNow;
            var suggestions = new List<SuggestedAction>
            {
                new() { Id = "s1", SourceAgent = "Agent", ActionType = "review", LinkedArea = "EE", Title = "Test A", CreatedAt = t },
                new() { Id = "s2", SourceAgent = "Agent", ActionType = "review", LinkedArea = "EE", Title = "Test B", CreatedAt = t.AddSeconds(-1) },
            };
            var memState = new OperatorMemoryState
            {
                Policies = [new() { Role = "X", AutonomyLevel = "Manual" }],
                DeskThreads = [new() { DeskId = "d", DeskTitle = "D", Messages = [] }],
                Suggestions = suggestions,
            };
            WriteOperatorMemoryJson(tempDir, memState);

            var store = new OperatorMemoryStore(tempDir, db: null);
            var state = await store.LoadAsync();

            Assert.Equal(2, state.Suggestions.Count);
        }
        finally { CleanUp(tempDir); }
    }

    [Fact]
    public async Task OperatorMemory_ResetAsync_ReturnsNormalizedDefaultState()
    {
        var tempDir = MakeTempDir("mem-norm");
        try
        {
            var store = new OperatorMemoryStore(tempDir, db: null);
            var state = await store.ResetAsync();

            Assert.NotEmpty(state.Policies);
            Assert.NotEmpty(state.DeskThreads);
            Assert.NotNull(state.Workflow);
        }
        finally { CleanUp(tempDir); }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string MakeTempDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanUp(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static void WriteSessionJson(string tempDir, OfficeLiveSessionState state)
    {
        var path = Path.Combine(tempDir, "broker-live-session.json");
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static void WriteOperatorMemoryJson(string tempDir, OperatorMemoryState state)
    {
        var path = Path.Combine(tempDir, "operator-memory.json");
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(path, json);
    }
}
