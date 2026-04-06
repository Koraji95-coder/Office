using DailyDesk.Models;
using DailyDesk.Services;
using OllamaSharp;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify the orchestrator graph construction described in
/// REFACTOR-PRESSURE.md chunk 1 (OfficeBrokerOrchestrator — Monolithic Coordinator).
///
/// These tests prove the core value of the refactor:
///   - Each domain coordinator (StudySessionCoordinator, ResearchCoordinator,
///     MLPipelineCoordinator) can be constructed and exercised independently,
///     without instantiating the full OfficeBrokerOrchestrator graph.
///   - Data produced by one coordinator flows correctly into another coordinator,
///     validating the coordinator interaction boundaries.
/// </summary>
public sealed class OrchestratorGraphIntegrationTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "orch-graph-test-" + Guid.NewGuid().ToString("N")[..8]
            );
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Builds a StudySessionCoordinator with a JSON-fallback TrainingStore (no LiteDB)
    /// to avoid parallel-init contention in tests.
    /// </summary>
    private static StudySessionCoordinator BuildStudyCoordinator(string tempDir) =>
        new StudySessionCoordinator(
            new TrainingStore(tempDir, db: null),
            new OralDefenseService(new StubModelProvider(), "test-model"),
            new LearningProfileService()
        );

    /// <summary>
    /// Builds a ResearchCoordinator with a JSON-fallback OperatorMemoryStore.
    /// </summary>
    private static ResearchCoordinator BuildResearchCoordinator(string tempDir) =>
        new ResearchCoordinator(new OperatorMemoryStore(tempDir, db: null));

    /// <summary>
    /// Builds a KnowledgeCoordinator with unavailable external services (Ollama/Qdrant) so
    /// that indexing attempts fall back gracefully without requiring live infrastructure.
    /// </summary>
    private static KnowledgeCoordinator BuildKnowledgeCoordinator(OfficeDatabase db) =>
        new KnowledgeCoordinator(
            new EmbeddingService(new OllamaSharp.OllamaApiClient(new Uri("http://localhost:11999"))),
            new VectorStoreService(host: "localhost", port: 16335),
            new KnowledgeIndexStore(db)
        );

    /// <summary>
    /// Builds an MLPipelineCoordinator using the fallback ML engine (no Python/ONNX required).
    /// </summary>
    private static MLPipelineCoordinator BuildMLCoordinator(OfficeDatabase db) =>
        new MLPipelineCoordinator(
            new MLAnalyticsService(
                new ProcessRunner(),
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "no-scripts-" + Guid.NewGuid().ToString("N")[..6]
                ),
                onnxEngine: new OnnxMLEngine(
                    System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        "no-models-" + Guid.NewGuid().ToString("N")[..6]
                    )
                ),
                cacheTtl: TimeSpan.Zero
            ),
            new MLResultStore(db)
        );

    private static PracticeTest BuildTest(int questionCount = 3)
    {
        var questions = Enumerable
            .Range(0, questionCount)
            .Select(i => new TrainingQuestion
            {
                Topic = i % 2 == 0 ? "grounding" : "protection",
                Difficulty = "Standard",
                Prompt = $"Question {i}?",
                CorrectOptionKey = "A",
                Explanation = $"Explanation {i}.",
                SuiteConnection = $"Suite connection {i}.",
                Options =
                [
                    new TrainingOption { Key = "A", Text = "Correct answer" },
                    new TrainingOption { Key = "B", Text = "Wrong answer" },
                ],
            })
            .ToList();

        return new PracticeTest
        {
            Title = "Graph Integration Test",
            Focus = "protection",
            Difficulty = "Standard",
            Questions = questions,
        };
    }

    private static OfficePracticeAnswerInput Answer(int index, string key) =>
        new() { QuestionIndex = index, SelectedOptionKey = key };

    // -------------------------------------------------------------------------
    // Group 1: Independent construction — each coordinator builds without the
    // full OfficeBrokerOrchestrator graph.
    // -------------------------------------------------------------------------

    [Fact]
    public void OrchestratorGraph_StudySessionCoordinator_CanBeConstructedIndependently()
    {
        using var tmpDir = new TempDirectory();

        // Should not throw — no reference to OfficeBrokerOrchestrator required.
        var coordinator = BuildStudyCoordinator(tmpDir.Path);

        Assert.NotNull(coordinator);
    }

    [Fact]
    public void OrchestratorGraph_ResearchCoordinator_CanBeConstructedIndependently()
    {
        using var tmpDir = new TempDirectory();

        var coordinator = BuildResearchCoordinator(tmpDir.Path);

        Assert.NotNull(coordinator);
    }

    [Fact]
    public void OrchestratorGraph_MLPipelineCoordinator_CanBeConstructedIndependently()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);

        var coordinator = BuildMLCoordinator(db);

        Assert.NotNull(coordinator);
    }

    [Fact]
    public void OrchestratorGraph_AllFourCoordinators_CanBeConstructedTogether()
    {
        // Verifies that all four coordinator graph nodes can be wired up in a single test,
        // mirroring what a thin OfficeBrokerOrchestrator facade would do.
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);

        var studyCoordinator = BuildStudyCoordinator(tmpDir.Path);
        var researchCoordinator = BuildResearchCoordinator(tmpDir.Path);
        var mlCoordinator = BuildMLCoordinator(db);
        var knowledgeCoordinator = BuildKnowledgeCoordinator(db);

        Assert.NotNull(studyCoordinator);
        Assert.NotNull(researchCoordinator);
        Assert.NotNull(mlCoordinator);
        Assert.NotNull(knowledgeCoordinator);
    }

    // -------------------------------------------------------------------------
    // Group 2: Data flow — StudySessionCoordinator → MLPipelineCoordinator.
    //
    // A practice attempt saved through StudySessionCoordinator can be retrieved
    // and fed directly into MLPipelineCoordinator analytics. This validates the
    // boundary between the two coordinators without needing the orchestrator.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OrchestratorGraph_StudyAttempt_FlowsIntoMLAnalytics()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);

        var studyCoordinator = BuildStudyCoordinator(tmpDir.Path);
        var mlCoordinator = BuildMLCoordinator(db);

        // Step 1: score and save a practice attempt via StudySessionCoordinator.
        var test = BuildTest(3);
        var correctCount = StudySessionCoordinator.ScoreAnswers(
            test,
            [Answer(0, "A"), Answer(1, "A"), Answer(2, "B")]
        );
        var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount);
        await studyCoordinator.SavePracticeAttemptAsync(attempt);

        // Step 2: load the saved attempts via TrainingStore (shared state) and
        // feed into MLPipelineCoordinator analytics.
        var trainingStore = new TrainingStore(tmpDir.Path, db: null);
        var attempts = trainingStore.LoadAllAttempts();

        var analyticsResult = await mlCoordinator.RunMLAnalyticsAsync(attempts, []);

        Assert.NotNull(analyticsResult);
        Assert.Equal("fallback", analyticsResult.Engine);
        // The attempt had mixed correct/wrong answers across grounding and protection topics.
        Assert.NotNull(analyticsResult.WeakTopics);
        Assert.NotNull(analyticsResult.StrongTopics);
    }

    [Fact]
    public async Task OrchestratorGraph_MultipleAttempts_FlowToMLAnalyticsCorrectly()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);

        var studyCoordinator = BuildStudyCoordinator(tmpDir.Path);
        var mlCoordinator = BuildMLCoordinator(db);

        // Save two attempts via StudySessionCoordinator.
        for (var i = 0; i < 2; i++)
        {
            var test = BuildTest(2);
            var correctCount = StudySessionCoordinator.ScoreAnswers(
                test,
                [Answer(0, "A")]
            );
            var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount);
            await studyCoordinator.SavePracticeAttemptAsync(attempt);
        }

        var trainingStore = new TrainingStore(tmpDir.Path, db: null);
        var attempts = trainingStore.LoadAllAttempts();

        var analyticsResult = await mlCoordinator.RunMLAnalyticsAsync(attempts, []);

        Assert.NotNull(analyticsResult);
        Assert.Equal(2, attempts.Count);
    }

    // -------------------------------------------------------------------------
    // Group 3: Coordinator independence — ResearchCoordinator and
    // MLPipelineCoordinator operate on separate stores and do not interfere.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OrchestratorGraph_ResearchAndML_DoNotInterfereWithEachOther()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);

        var researchCoordinator = BuildResearchCoordinator(tmpDir.Path);
        var mlCoordinator = BuildMLCoordinator(db);

        // Save watchlists via ResearchCoordinator.
        var watchlists = new List<ResearchWatchlist>
        {
            new()
            {
                Id = "w1",
                Topic = "Protection Research",
                Query = "protective relay standards",
                IsEnabled = true,
            },
        };
        var memoryState = await researchCoordinator.SaveWatchlistsAsync(watchlists);

        // Run ML analytics via MLPipelineCoordinator with empty data.
        var analyticsResult = await mlCoordinator.RunMLAnalyticsAsync([], []);

        // Both coordinators should operate correctly without interfering.
        Assert.Single(memoryState.Watchlists);
        Assert.Equal("w1", memoryState.Watchlists[0].Id);
        Assert.NotNull(analyticsResult);
        Assert.Equal("fallback", analyticsResult.Engine);
    }

    [Fact]
    public async Task OrchestratorGraph_ResearchWatchlist_StateIsolatedFromMLStore()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);

        var researchCoordinator = BuildResearchCoordinator(tmpDir.Path);
        var mlStore = new MLResultStore(db);

        // Save a watchlist and then run ML analytics.
        await researchCoordinator.SaveWatchlistsAsync(
        [
            new ResearchWatchlist
            {
                Id = "isolate-test",
                Topic = "Isolation Test",
                Query = "test isolation query",
                IsEnabled = true,
            },
        ]);

        // MLResultStore should not be affected by ResearchCoordinator operations.
        var analyticsBeforeRun = mlStore.LoadAnalytics();
        Assert.Null(analyticsBeforeRun);

        // After running ML analytics, ResearchCoordinator state should remain intact.
        var mlCoordinator = BuildMLCoordinator(db);
        await mlCoordinator.RunMLAnalyticsAsync([], []);

        var reloaded = await researchCoordinator.LoadMemoryStateAsync();
        Assert.Single(reloaded.Watchlists);
        Assert.Equal("Isolation Test", reloaded.Watchlists[0].Topic);
    }

    // -------------------------------------------------------------------------
    // Group 4: Full end-to-end flow through the coordinator graph.
    //
    // Score answers → save attempt → run ML analytics → export artifacts.
    // This is the domain workflow that previously required constructing the full
    // OfficeBrokerOrchestrator; here it is exercised at the coordinator level.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OrchestratorGraph_FullDomainFlow_ScoreToAnalyticsToExport()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);

        var studyCoordinator = BuildStudyCoordinator(tmpDir.Path);
        var mlCoordinator = BuildMLCoordinator(db);

        // Stage 1: Score practice test and save attempt.
        var test = BuildTest(4);
        var correctCount = StudySessionCoordinator.ScoreAnswers(
            test,
            [Answer(0, "A"), Answer(1, "A"), Answer(2, "B"), Answer(3, "A")]
        );
        var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount);
        var summary = await studyCoordinator.SavePracticeAttemptAsync(attempt);

        Assert.Equal(1, summary.TotalAttempts);

        // Stage 2: Run the full ML pipeline using the saved attempt data.
        var trainingStore = new TrainingStore(tmpDir.Path, db: null);
        var attempts = trainingStore.LoadAllAttempts();

        var pipelineResult = await mlCoordinator.RunFullMLPipelineAsync(
            attempts,
            [],
            [],
            tmpDir.Path
        );

        Assert.NotNull(pipelineResult);
        Assert.NotNull(pipelineResult.Analytics);
        Assert.NotNull(pipelineResult.Forecast);
        Assert.NotNull(pipelineResult.Embeddings);
        Assert.NotEmpty(pipelineResult.ExportPath);
        Assert.True(File.Exists(pipelineResult.ExportPath));
    }

    [Fact]
    public async Task OrchestratorGraph_StudyCoordinator_ResultSummaryReflectsAllAttempts()
    {
        using var tmpDir = new TempDirectory();

        var studyCoordinator = BuildStudyCoordinator(tmpDir.Path);

        // Save three attempts and verify the training summary aggregates all of them.
        for (var i = 0; i < 3; i++)
        {
            var test = BuildTest(2);
            var correctCount = StudySessionCoordinator.ScoreAnswers(test, [Answer(0, "A")]);
            var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount);
            await studyCoordinator.SavePracticeAttemptAsync(attempt);
        }

        var trainingStore = new TrainingStore(tmpDir.Path, db: null);
        var summary = await trainingStore.LoadSummaryAsync();

        Assert.Equal(3, summary.TotalAttempts);
    }

    [Fact]
    public async Task OrchestratorGraph_MLCoordinator_PersistsAnalyticsFromStudyAttemptData()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var mlStore = new MLResultStore(db);

        var studyCoordinator = BuildStudyCoordinator(tmpDir.Path);
        var mlCoordinator = new MLPipelineCoordinator(
            new MLAnalyticsService(
                new ProcessRunner(),
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "no-scripts-persist"),
                onnxEngine: new OnnxMLEngine(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "no-models-persist")
                ),
                cacheTtl: TimeSpan.Zero
            ),
            mlStore
        );

        // Save a practice attempt.
        var test = BuildTest(2);
        var correctCount = StudySessionCoordinator.ScoreAnswers(test, [Answer(0, "A"), Answer(1, "B")]);
        var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount);
        await studyCoordinator.SavePracticeAttemptAsync(attempt);

        // Load attempt and run ML analytics — result must be persisted to the shared MLResultStore.
        var trainingStore = new TrainingStore(tmpDir.Path, db: null);
        var attempts = trainingStore.LoadAllAttempts();
        await mlCoordinator.RunMLAnalyticsAsync(attempts, []);

        var persisted = mlStore.LoadAnalytics();
        Assert.NotNull(persisted);
        Assert.Equal("fallback", persisted!.Engine);
    }

    // -------------------------------------------------------------------------
    // Group 5: KnowledgeCoordinator graph integration.
    //
    // KnowledgeCoordinator handles document indexing and vector-store queries.
    // External services (Ollama, Qdrant) are unreachable in tests, so the
    // coordinator falls back gracefully. These tests verify construction,
    // empty-input handling, and store isolation within the full 4-coordinator graph.
    // -------------------------------------------------------------------------

    [Fact]
    public void OrchestratorGraph_KnowledgeCoordinator_CanBeConstructedIndependently()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);

        // Should not throw — no reference to OfficeBrokerOrchestrator required.
        var coordinator = BuildKnowledgeCoordinator(db);

        Assert.NotNull(coordinator);
    }

    [Fact]
    public async Task OrchestratorGraph_KnowledgeCoordinator_EmptyDocumentList_ReturnsZeroCounts()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var coordinator = BuildKnowledgeCoordinator(db);

        // Indexing an empty list must always succeed with zero counts.
        var result = await coordinator.RunKnowledgeIndexAsync([]);

        Assert.Equal(0, result.TotalDocuments);
        Assert.Equal(0, result.Indexed);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task OrchestratorGraph_KnowledgeCoordinator_DocumentWithNoText_IsSkipped()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var coordinator = BuildKnowledgeCoordinator(db);

        var documents = new[]
        {
            new LearningDocument
            {
                RelativePath = "docs/empty.md",
                Kind = "MD",
                SourceRootLabel = "graph-test",
                ExtractedText = "",
                Summary = "",
            },
        };

        // A document with no extractable text must be skipped, not failed.
        var result = await coordinator.RunKnowledgeIndexAsync(documents);

        Assert.Equal(1, result.TotalDocuments);
        Assert.Equal(0, result.Indexed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task OrchestratorGraph_KnowledgeCoordinator_StateIsolatedFromTrainingStore()
    {
        // Verifies that KnowledgeIndexStore state does not bleed into TrainingStore.
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);

        var studyCoordinator = BuildStudyCoordinator(tmpDir.Path);
        var knowledgeCoordinator = BuildKnowledgeCoordinator(db);

        // Save a practice attempt via StudySessionCoordinator.
        var test = BuildTest(2);
        var correctCount = StudySessionCoordinator.ScoreAnswers(test, [Answer(0, "A")]);
        var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount);
        await studyCoordinator.SavePracticeAttemptAsync(attempt);

        // Index an empty document list via KnowledgeCoordinator — should not
        // disturb the TrainingStore written by StudySessionCoordinator.
        await knowledgeCoordinator.RunKnowledgeIndexAsync([]);

        var trainingStore = new TrainingStore(tmpDir.Path, db: null);
        var attempts = trainingStore.LoadAllAttempts();

        Assert.Single(attempts);
    }

    // -------------------------------------------------------------------------
    // Private stub
    // -------------------------------------------------------------------------

    private sealed class StubModelProvider : IModelProvider
    {
        public string ProviderId => "stub";
        public string ProviderLabel => "Stub";

        public Task<IReadOnlyList<string>> GetInstalledModelsAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<string> GenerateAsync(
            string model,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Stub provider unavailable.");

        public Task<T?> GenerateJsonAsync<T>(
            string model,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Stub provider unavailable.");

        public Task<bool> PingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
