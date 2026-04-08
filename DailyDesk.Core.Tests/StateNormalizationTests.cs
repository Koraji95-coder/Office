using System.IO;
using System.Text.Json;
using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Tests that verify the state normalization behavior documented in
/// <c>OfficeSessionStateStore.Normalize()</c> and <c>OperatorMemoryStore.NormalizeState()</c>.
/// Both private methods are exercised through the public <c>LoadAsync</c> / <c>ResetAsync</c>
/// surface, writing known raw JSON to the store paths and then asserting the returned state.
/// </summary>
public sealed class StateNormalizationTests : IDisposable
{
    private readonly string _tempDir;

    public StateNormalizationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"StateNormalizationTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    // ──────────────────────────────────────────────────────────────────────
    // OfficeSessionStateStore.Normalize()
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SessionState_ResetAsync_ReturnsFullyNormalizedDefaults()
    {
        var store = new OfficeSessionStateStore(_tempDir);
        var state = await store.ResetAsync();

        Assert.Equal(OfficeRouteCatalog.ChiefRoute, state.CurrentRoute);
        Assert.Equal("Protection, grounding, standards, drafting safety", state.Focus);
        Assert.Equal(
            "Set a focus manually or start from a review target to begin a guided session.",
            state.FocusReason);
        Assert.Equal("Mixed", state.Difficulty);
        Assert.InRange(state.QuestionCount, 3, 10);
        Assert.NotNull(state.ActiveDefenseScenario);
        Assert.Equal("No scored practice yet.", state.PracticeResultSummary);
        Assert.Equal("No scored oral-defense answer yet.", state.DefenseScoreSummary);
        Assert.Equal(
            "Score a typed answer to get rubric feedback and follow-up coaching.",
            state.DefenseFeedbackSummary);
        Assert.Equal(
            "Score a practice or defense session to save a reflection.",
            state.ReflectionContextSummary);
    }

    [Fact]
    public async Task SessionState_LoadAsync_AppliesDefaultsForMissingStringFields()
    {
        // Write a raw JSON file with empty/null string fields
        var rawState = new
        {
            CurrentRoute = "",
            Focus = "",
            FocusReason = (string?)null,
            Difficulty = "   ",
            QuestionCount = 0,
            PracticeResultSummary = (string?)null,
            DefenseScoreSummary = "  ",
            DefenseFeedbackSummary = "",
            ReflectionContextSummary = (string?)null,
        };
        var json = JsonSerializer.Serialize(rawState, new JsonSerializerOptions { WriteIndented = true });
        var sessionPath = Path.Combine(_tempDir, "broker-live-session.json");
        await File.WriteAllTextAsync(sessionPath, json);

        var store = new OfficeSessionStateStore(_tempDir);
        var state = await store.LoadAsync();

        Assert.Equal(OfficeRouteCatalog.ChiefRoute, state.CurrentRoute);
        Assert.Equal("Protection, grounding, standards, drafting safety", state.Focus);
        Assert.Equal(
            "Set a focus manually or start from a review target to begin a guided session.",
            state.FocusReason);
        Assert.Equal("Mixed", state.Difficulty);
        Assert.Equal("No scored practice yet.", state.PracticeResultSummary);
        Assert.Equal("No scored oral-defense answer yet.", state.DefenseScoreSummary);
        Assert.Equal(
            "Score a typed answer to get rubric feedback and follow-up coaching.",
            state.DefenseFeedbackSummary);
        Assert.Equal(
            "Score a practice or defense session to save a reflection.",
            state.ReflectionContextSummary);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    [InlineData(7, 7)]
    [InlineData(10, 10)]
    [InlineData(11, 10)]
    [InlineData(100, 10)]
    public async Task SessionState_LoadAsync_ClampsQuestionCountToValidRange(int input, int expected)
    {
        var rawState = new { QuestionCount = input };
        var json = JsonSerializer.Serialize(rawState);
        var sessionPath = Path.Combine(_tempDir, "broker-live-session.json");
        await File.WriteAllTextAsync(sessionPath, json);

        var store = new OfficeSessionStateStore(_tempDir);
        var state = await store.LoadAsync();

        Assert.Equal(expected, state.QuestionCount);
    }

    [Theory]
    [InlineData(null, "chief")]
    [InlineData("", "chief")]
    [InlineData("unknown-route", "chief")]
    [InlineData("engineering", "engineering")]
    [InlineData("ENGINEERING", "engineering")]
    [InlineData("ml", "ml")]
    public async Task SessionState_LoadAsync_NormalizesCurrentRoute(string? inputRoute, string expectedRoute)
    {
        var rawState = new { CurrentRoute = inputRoute };
        var json = JsonSerializer.Serialize(rawState);
        var sessionPath = Path.Combine(_tempDir, "broker-live-session.json");
        await File.WriteAllTextAsync(sessionPath, json);

        var store = new OfficeSessionStateStore(_tempDir);
        var state = await store.LoadAsync();

        Assert.Equal(expectedRoute, state.CurrentRoute);
    }

    [Fact]
    public async Task SessionState_LoadAsync_PreservesValidValues()
    {
        var rawState = new
        {
            CurrentRoute = "engineering",
            Focus = "  Transformer design  ",
            FocusReason = " Deep dive  ",
            Difficulty = " Hard ",
            QuestionCount = 5,
            PracticeResultSummary = " 80% ",
            DefenseScoreSummary = " 90% ",
            DefenseFeedbackSummary = " Great answer. ",
            ReflectionContextSummary = " Reflection saved. ",
        };
        var json = JsonSerializer.Serialize(rawState);
        var sessionPath = Path.Combine(_tempDir, "broker-live-session.json");
        await File.WriteAllTextAsync(sessionPath, json);

        var store = new OfficeSessionStateStore(_tempDir);
        var state = await store.LoadAsync();

        // Route normalized, whitespace trimmed from strings; values otherwise preserved
        Assert.Equal("engineering", state.CurrentRoute);
        Assert.Equal("Transformer design", state.Focus);
        Assert.Equal("Deep dive", state.FocusReason);
        Assert.Equal("Hard", state.Difficulty);
        Assert.Equal(5, state.QuestionCount);
        Assert.Equal("80%", state.PracticeResultSummary);
        Assert.Equal("90%", state.DefenseScoreSummary);
        Assert.Equal("Great answer.", state.DefenseFeedbackSummary);
        Assert.Equal("Reflection saved.", state.ReflectionContextSummary);
    }

    [Fact]
    public async Task SessionState_LoadAsync_ReturnsNormalizedDefaultsWhenFileAbsent()
    {
        // No file written — LoadAsync should fall back to a new normalized state
        var store = new OfficeSessionStateStore(_tempDir);
        var state = await store.LoadAsync();

        Assert.Equal(OfficeRouteCatalog.ChiefRoute, state.CurrentRoute);
        Assert.Equal("Mixed", state.Difficulty);
        Assert.InRange(state.QuestionCount, 3, 10);
        Assert.NotNull(state.ActiveDefenseScenario);
    }

    // ──────────────────────────────────────────────────────────────────────
    // OperatorMemoryStore.NormalizeState()
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OperatorMemory_ResetAsync_SeedsDefaultPoliciesAndThreads()
    {
        var store = new OperatorMemoryStore(_tempDir);
        var state = await store.ResetAsync();

        Assert.NotEmpty(state.Policies);
        // Default watchlists are intentionally empty — normalization does not seed them
        Assert.NotNull(state.Watchlists);
        Assert.NotEmpty(state.DeskThreads);
        Assert.NotNull(state.Workflow);
        Assert.NotNull(state.Suggestions);
        Assert.NotNull(state.Activities);
        Assert.NotNull(state.DailyRuns);
    }

    [Fact]
    public async Task OperatorMemory_LoadAsync_SeedsDefaultPoliciesWhenNoneStored()
    {
        // Write a minimal JSON file with no policies
        var rawState = new { Policies = Array.Empty<object>() };
        var json = JsonSerializer.Serialize(rawState);
        var memoryPath = Path.Combine(_tempDir, "operator-memory.json");
        await File.WriteAllTextAsync(memoryPath, json);

        var store = new OperatorMemoryStore(_tempDir);
        var state = await store.LoadAsync();

        Assert.NotEmpty(state.Policies);
        // The five canonical roles must all be present
        var roles = state.Policies.Select(p => p.Role).ToHashSet();
        Assert.Contains("Chief of Staff", roles);
        Assert.Contains("EE Mentor", roles);
        Assert.Contains("Test Builder", roles);
        Assert.Contains("Repo Coach", roles);
        Assert.Contains("Business Strategist", roles);
    }

    [Fact]
    public async Task OperatorMemory_LoadAsync_SeedsDefaultDeskThreadsWhenNoneStored()
    {
        var rawState = new { DeskThreads = Array.Empty<object>() };
        var json = JsonSerializer.Serialize(rawState);
        var memoryPath = Path.Combine(_tempDir, "operator-memory.json");
        await File.WriteAllTextAsync(memoryPath, json);

        var store = new OperatorMemoryStore(_tempDir);
        var state = await store.LoadAsync();

        Assert.NotEmpty(state.DeskThreads);
    }

    [Fact]
    public async Task OperatorMemory_LoadAsync_InitializesNullCollectionsToEmptyLists()
    {
        // Write a JSON file that omits Suggestions, Activities, DailyRuns entirely
        var rawState = new { };
        var json = JsonSerializer.Serialize(rawState);
        var memoryPath = Path.Combine(_tempDir, "operator-memory.json");
        await File.WriteAllTextAsync(memoryPath, json);

        var store = new OperatorMemoryStore(_tempDir);
        var state = await store.LoadAsync();

        Assert.NotNull(state.Suggestions);
        Assert.NotNull(state.Activities);
        Assert.NotNull(state.DailyRuns);
    }

    [Fact]
    public async Task OperatorMemory_LoadAsync_InitializesNullWorkflowToDefaultInstance()
    {
        var rawState = new { Workflow = (object?)null };
        var json = JsonSerializer.Serialize(rawState);
        var memoryPath = Path.Combine(_tempDir, "operator-memory.json");
        await File.WriteAllTextAsync(memoryPath, json);

        var store = new OperatorMemoryStore(_tempDir);
        var state = await store.LoadAsync();

        Assert.NotNull(state.Workflow);
    }

    [Fact]
    public async Task OperatorMemory_LoadAsync_DeduplicatesSuggestionsBySemanticKey()
    {
        // Create two suggestions with the same SourceAgent + ActionType + Title (semantic key)
        // and a third with a distinct key.
        var now = DateTimeOffset.UtcNow;
        var suggestions = new[]
        {
            new
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "Improve grounding spec",
                SourceAgent = "EE Mentor",
                ActionType = "training prep",
                CreatedAt = now.AddMinutes(-10),
                ExecutionUpdatedAt = (DateTimeOffset?)null,
                Priority = "medium",
                Rationale = "",
                ExpectedBenefit = "",
                LinkedArea = "",
                WhatYouLearn = "",
                ProductImpact = "",
                CareerValue = "",
                RequiresApproval = true,
                ExecutionStatus = "not_queued",
                ExecutionSummary = "",
                LatestResultSummary = "",
                LatestResultDetail = "",
                LatestResultSources = Array.Empty<string>(),
                LatestResultPath = "",
            },
            new
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "Improve grounding spec",    // duplicate key
                SourceAgent = "EE Mentor",
                ActionType = "training prep",
                CreatedAt = now.AddMinutes(-5),      // newer — should win
                ExecutionUpdatedAt = (DateTimeOffset?)null,
                Priority = "medium",
                Rationale = "",
                ExpectedBenefit = "",
                LinkedArea = "",
                WhatYouLearn = "",
                ProductImpact = "",
                CareerValue = "",
                RequiresApproval = true,
                ExecutionStatus = "not_queued",
                ExecutionSummary = "",
                LatestResultSummary = "",
                LatestResultDetail = "",
                LatestResultSources = Array.Empty<string>(),
                LatestResultPath = "",
            },
            new
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "Draft protection checklist",  // distinct key
                SourceAgent = "Test Builder",
                ActionType = "practice generation",
                CreatedAt = now,
                ExecutionUpdatedAt = (DateTimeOffset?)null,
                Priority = "high",
                Rationale = "",
                ExpectedBenefit = "",
                LinkedArea = "",
                WhatYouLearn = "",
                ProductImpact = "",
                CareerValue = "",
                RequiresApproval = false,
                ExecutionStatus = "not_queued",
                ExecutionSummary = "",
                LatestResultSummary = "",
                LatestResultDetail = "",
                LatestResultSources = Array.Empty<string>(),
                LatestResultPath = "",
            },
        };

        var rawState = new { Suggestions = suggestions };
        var json = JsonSerializer.Serialize(rawState, new JsonSerializerOptions { WriteIndented = true });
        var memoryPath = Path.Combine(_tempDir, "operator-memory.json");
        await File.WriteAllTextAsync(memoryPath, json);

        var store = new OperatorMemoryStore(_tempDir);
        var state = await store.LoadAsync();

        // Duplicates collapsed: only 2 distinct suggestions should survive
        Assert.Equal(2, state.Suggestions.Count);
    }

    [Fact]
    public async Task OperatorMemory_LoadAsync_PreservesExistingPoliciesWithoutOverwriting()
    {
        // Write a non-empty custom policy list — normalization must not replace it
        var rawState = new
        {
            Policies = new[]
            {
                new
                {
                    Role = "Custom Agent",
                    AutonomyLevel = "Manual",
                    RequiresApproval = true,
                    ReviewCadence = "Weekly",
                    AllowedActionClasses = Array.Empty<string>(),
                }
            }
        };
        var json = JsonSerializer.Serialize(rawState);
        var memoryPath = Path.Combine(_tempDir, "operator-memory.json");
        await File.WriteAllTextAsync(memoryPath, json);

        var store = new OperatorMemoryStore(_tempDir);
        var state = await store.LoadAsync();

        // Existing policy is preserved; no default policies injected on top
        Assert.Single(state.Policies);
        Assert.Equal("Custom Agent", state.Policies[0].Role);
    }
}
