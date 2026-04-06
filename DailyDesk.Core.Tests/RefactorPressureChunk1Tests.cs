using DailyDesk.Models;
using DailyDesk.Services;
using OllamaSharp;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify MLPipelineCoordinator and KnowledgeCoordinator comply
/// with REFACTOR-PRESSURE.md chunk 1 requirements.
///
/// Chunk 1 prescribes splitting OfficeBrokerOrchestrator into domain coordinators:
///   - MLPipelineCoordinator  — ML job dispatch, result retrieval, export artifacts.
///   - KnowledgeCoordinator   — import, indexing, context building.
///
/// Each test group is aligned to one of these responsibilities and verifies that:
///   1. The coordinator can be constructed as a standalone class (no OfficeBrokerOrchestrator).
///   2. Every operation described in chunk 1 is functional.
///   3. Cross-coordinator independence: both coordinators can share a database without conflict.
/// </summary>
[Collection("CoordinatorTests")]
public sealed class RefactorPressureChunk1Tests
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
                "chunk1-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Returns an MLAnalyticsService wired to non-existent Python scripts and ONNX models
    /// so it always falls back to the built-in fallback engine.
    /// </summary>
    private static MLAnalyticsService BuildFallbackMlService() =>
        new MLAnalyticsService(
            new ProcessRunner(),
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "no-scripts-" + Guid.NewGuid().ToString("N")[..6]),
            onnxEngine: new OnnxMLEngine(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "no-models-" + Guid.NewGuid().ToString("N")[..6])),
            cacheTtl: TimeSpan.Zero);

    /// <summary>
    /// Returns an EmbeddingService pointed at an unreachable Ollama port — calls return null.
    /// </summary>
    private static EmbeddingService BuildUnavailableEmbeddingService() =>
        new EmbeddingService(new OllamaApiClient(new Uri("http://localhost:11999")));

    /// <summary>
    /// Returns a VectorStoreService pointed at a non-running Qdrant instance — calls return null/false.
    /// </summary>
    private static VectorStoreService BuildUnavailableVectorStore() =>
        new VectorStoreService(host: "localhost", port: 16335);

    private static MLPipelineCoordinator BuildMLCoordinator(OfficeDatabase db) =>
        new MLPipelineCoordinator(BuildFallbackMlService(), new MLResultStore(db));

    private static KnowledgeCoordinator BuildKnowledgeCoordinator(OfficeDatabase db) =>
        new KnowledgeCoordinator(
            BuildUnavailableEmbeddingService(),
            BuildUnavailableVectorStore(),
            new KnowledgeIndexStore(db));

    private static IReadOnlyList<TrainingAttemptRecord> MakeSampleAttempts() =>
    [
        new TrainingAttemptRecord
        {
            CompletedAt = DateTimeOffset.Now,
            Questions =
            [
                new TrainingAttemptQuestionRecord { Topic = "protection", Correct = true },
                new TrainingAttemptQuestionRecord { Topic = "grounding", Correct = false },
                new TrainingAttemptQuestionRecord { Topic = "grounding", Correct = true },
            ],
        },
    ];

    private static LearningDocument MakeDocument(string path, string text = "", string summary = "") =>
        new LearningDocument
        {
            RelativePath = path,
            Kind = "markdown",
            SourceRootLabel = "test",
            ExtractedText = text,
            Summary = summary,
        };

    // -------------------------------------------------------------------------
    // Chunk 1: MLPipelineCoordinator — standalone construction
    // -------------------------------------------------------------------------

    [Fact]
    public void Chunk1_MLPipelineCoordinator_CanBeConstructedWithoutOrchestrator()
    {
        using var tmp = new TempDirectory();
        var db = new OfficeDatabase(tmp.Path);

        // Must construct without any reference to OfficeBrokerOrchestrator
        var coordinator = BuildMLCoordinator(db);
        Assert.NotNull(coordinator);
    }

    // -------------------------------------------------------------------------
    // Chunk 1: MLPipelineCoordinator — ML job dispatch
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Chunk1_MLPipelineCoordinator_DispatchesAnalyticsJob_ReturnsResult()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildMLCoordinator(new OfficeDatabase(tmp.Path));

        var result = await coordinator.RunMLAnalyticsAsync(MakeSampleAttempts(), []);

        Assert.NotNull(result);
        Assert.Equal("fallback", result.Engine);
    }

    [Fact]
    public async Task Chunk1_MLPipelineCoordinator_DispatchesForecastJob_ReturnsResult()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildMLCoordinator(new OfficeDatabase(tmp.Path));

        var result = await coordinator.RunMLForecastAsync(MakeSampleAttempts());

        Assert.NotNull(result);
        Assert.Equal("fallback", result.Engine);
    }

    [Fact]
    public async Task Chunk1_MLPipelineCoordinator_DispatchesEmbeddingsJob_ReturnsResult()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildMLCoordinator(new OfficeDatabase(tmp.Path));

        var result = await coordinator.RunMLEmbeddingsAsync([]);

        Assert.NotNull(result);
        Assert.Equal("fallback", result.Engine);
    }

    [Fact]
    public async Task Chunk1_MLPipelineCoordinator_DispatchesFullPipelineJob_ReturnsAllResults()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildMLCoordinator(new OfficeDatabase(tmp.Path));

        var result = await coordinator.RunFullMLPipelineAsync(MakeSampleAttempts(), [], [], tmp.Path);

        Assert.NotNull(result.Analytics);
        Assert.NotNull(result.Forecast);
        Assert.NotNull(result.Embeddings);
        Assert.NotNull(result.Artifacts);
        Assert.NotEmpty(result.ExportPath);
    }

    // -------------------------------------------------------------------------
    // Chunk 1: MLPipelineCoordinator — result retrieval
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Chunk1_MLPipelineCoordinator_RetrievesAnalyticsResultFromStore()
    {
        using var tmp = new TempDirectory();
        var db = new OfficeDatabase(tmp.Path);
        var store = new MLResultStore(db);
        var coordinator = new MLPipelineCoordinator(BuildFallbackMlService(), store);

        await coordinator.RunMLAnalyticsAsync(MakeSampleAttempts(), []);

        // Result must be retrievable from the store after dispatch
        var retrieved = store.LoadAnalytics();
        Assert.NotNull(retrieved);
        Assert.Equal("fallback", retrieved.Engine);
    }

    [Fact]
    public async Task Chunk1_MLPipelineCoordinator_RetrievesForecastResultFromStore()
    {
        using var tmp = new TempDirectory();
        var db = new OfficeDatabase(tmp.Path);
        var store = new MLResultStore(db);
        var coordinator = new MLPipelineCoordinator(BuildFallbackMlService(), store);

        await coordinator.RunMLForecastAsync(MakeSampleAttempts());

        var retrieved = store.LoadForecast();
        Assert.NotNull(retrieved);
        Assert.Equal("fallback", retrieved.Engine);
    }

    [Fact]
    public async Task Chunk1_MLPipelineCoordinator_RetrievesEmbeddingsResultFromStore()
    {
        using var tmp = new TempDirectory();
        var db = new OfficeDatabase(tmp.Path);
        var store = new MLResultStore(db);
        var coordinator = new MLPipelineCoordinator(BuildFallbackMlService(), store);

        await coordinator.RunMLEmbeddingsAsync([]);

        var retrieved = store.LoadEmbeddings();
        Assert.NotNull(retrieved);
        Assert.Equal("fallback", retrieved.Engine);
    }

    [Fact]
    public async Task Chunk1_MLPipelineCoordinator_RetrievesLastRunTimestampAfterFullPipeline()
    {
        using var tmp = new TempDirectory();
        var db = new OfficeDatabase(tmp.Path);
        var store = new MLResultStore(db);
        var coordinator = new MLPipelineCoordinator(BuildFallbackMlService(), store);

        var before = DateTimeOffset.Now.AddSeconds(-1);
        await coordinator.RunFullMLPipelineAsync([], [], [], tmp.Path);

        var timestamp = store.LoadLastRunTimestamp();
        Assert.NotNull(timestamp);
        Assert.True(timestamp.Value >= before, "Last-run timestamp must be set after a full pipeline run");
    }

    // -------------------------------------------------------------------------
    // Chunk 1: MLPipelineCoordinator — export artifacts
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Chunk1_MLPipelineCoordinator_ExportsArtifacts_ReturnsBundle()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildMLCoordinator(new OfficeDatabase(tmp.Path));

        var bundle = await coordinator.ExportSuiteArtifactsAsync(
            new MLAnalyticsResult { Ok = false, Engine = "fallback" },
            new MLEmbeddingsResult { Ok = false, Engine = "fallback" },
            new MLForecastResult { Ok = false, Engine = "fallback" },
            tmp.Path);

        Assert.NotNull(bundle);
    }

    [Fact]
    public async Task Chunk1_MLPipelineCoordinator_ExportsArtifacts_WritesFileToDisk()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildMLCoordinator(new OfficeDatabase(tmp.Path));

        await coordinator.ExportSuiteArtifactsAsync(
            new MLAnalyticsResult { Ok = false, Engine = "fallback" },
            new MLEmbeddingsResult { Ok = false, Engine = "fallback" },
            new MLForecastResult { Ok = false, Engine = "fallback" },
            tmp.Path);

        var artifactsDir = System.IO.Path.Combine(tmp.Path, "ml-artifacts");
        Assert.True(Directory.Exists(artifactsDir));
        Assert.NotEmpty(Directory.GetFiles(artifactsDir, "suite-artifacts-*.json"));
    }

    // -------------------------------------------------------------------------
    // Chunk 1: KnowledgeCoordinator — standalone construction
    // -------------------------------------------------------------------------

    [Fact]
    public void Chunk1_KnowledgeCoordinator_CanBeConstructedWithoutOrchestrator()
    {
        using var tmp = new TempDirectory();
        var db = new OfficeDatabase(tmp.Path);

        var coordinator = BuildKnowledgeCoordinator(db);
        Assert.NotNull(coordinator);
    }

    // -------------------------------------------------------------------------
    // Chunk 1: KnowledgeCoordinator — indexing
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Chunk1_KnowledgeCoordinator_IndexesDocuments_ReturnsResult()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildKnowledgeCoordinator(new OfficeDatabase(tmp.Path));

        var result = await coordinator.RunKnowledgeIndexAsync([]);

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalDocuments);
        Assert.True(result.IndexedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task Chunk1_KnowledgeCoordinator_IndexesDocuments_SkipsEmptyContent()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildKnowledgeCoordinator(new OfficeDatabase(tmp.Path));

        var result = await coordinator.RunKnowledgeIndexAsync([
            MakeDocument("doc/empty.md"),
        ]);

        Assert.Equal(1, result.TotalDocuments);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task Chunk1_KnowledgeCoordinator_IndexesDocuments_SkipsAlreadyIndexed()
    {
        using var tmp = new TempDirectory();
        var db = new OfficeDatabase(tmp.Path);
        var indexStore = new KnowledgeIndexStore(db);

        const string path = "doc/indexed.md";
        const string text = "This document has already been indexed.";
        indexStore.MarkIndexed(path, KnowledgeIndexStore.ComputeContentHash(text), "vec-001");

        var coordinator = new KnowledgeCoordinator(
            BuildUnavailableEmbeddingService(),
            BuildUnavailableVectorStore(),
            indexStore);

        var result = await coordinator.RunKnowledgeIndexAsync([
            MakeDocument(path, text: text),
        ]);

        Assert.Equal(1, result.TotalDocuments);
        Assert.Equal(1, result.Skipped); // same hash — skip without re-embedding
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task Chunk1_KnowledgeCoordinator_IndexesDocuments_CountsFailuresWhenEmbeddingUnavailable()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildKnowledgeCoordinator(new OfficeDatabase(tmp.Path));

        var result = await coordinator.RunKnowledgeIndexAsync([
            MakeDocument("doc/new.md", text: "Content that needs embedding."),
        ]);

        Assert.Equal(1, result.TotalDocuments);
        Assert.Equal(0, result.Indexed);
        Assert.Equal(1, result.Failed); // embedding service unavailable
    }

    [Fact]
    public async Task Chunk1_KnowledgeCoordinator_IndexesDocuments_UsesSummaryWhenExtractedTextAbsent()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildKnowledgeCoordinator(new OfficeDatabase(tmp.Path));

        // Summary provides text content when ExtractedText is empty
        var result = await coordinator.RunKnowledgeIndexAsync([
            MakeDocument("doc/summary.md", summary: "This document only has a summary."),
        ]);

        Assert.Equal(1, result.TotalDocuments);
        Assert.Equal(0, result.Skipped); // summary supplies content → not skipped
        Assert.Equal(1, result.Failed);  // but embedding service unavailable → failed
    }

    // -------------------------------------------------------------------------
    // Chunk 1: KnowledgeCoordinator — context building
    // -------------------------------------------------------------------------

    [Fact]
    public void Chunk1_KnowledgeCoordinator_BuildsContext_ReturnsNoneForEmptyLibrary()
    {
        var library = new LearningLibrary { Documents = [] };

        var context = KnowledgePromptContextBuilder.BuildRelevantContext(
            library,
            hints: ["relay protection"],
            maxDocuments: 3);

        Assert.Equal("none recorded", context);
    }

    [Fact]
    public void Chunk1_KnowledgeCoordinator_BuildsContext_IncludesMatchingDocumentExcerpt()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    RelativePath = "notes/relay-protection.md",
                    Kind = "markdown",
                    SourceRootLabel = "test",
                    ExtractedText = "Relay protection schemes include overcurrent and distance protection.",
                    Topics = ["relay protection", "overcurrent"],
                },
            ],
        };

        var context = KnowledgePromptContextBuilder.BuildRelevantContext(
            library,
            hints: ["relay protection"],
            maxDocuments: 3);

        Assert.NotEqual("none recorded", context);
        Assert.Contains("relay-protection.md", context, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Chunk1_KnowledgeCoordinator_BuildsContext_ReturnsNoneForDocumentsWithNoContent()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    RelativePath = "notes/empty.md",
                    Kind = "markdown",
                    SourceRootLabel = "test",
                    ExtractedText = "",
                    Summary = "",
                },
            ],
        };

        var context = KnowledgePromptContextBuilder.BuildRelevantContext(
            library,
            hints: ["relay protection"],
            maxDocuments: 3);

        Assert.Equal("none recorded", context);
    }

    [Fact]
    public async Task Chunk1_KnowledgeCoordinator_BuildsContextWithSemanticSearch_FallsBackToKeyword()
    {
        // With both embedding and vector store unavailable (null parameters),
        // BuildRelevantContextWithSemanticSearchAsync must fall back to keyword search.
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    RelativePath = "notes/grounding.md",
                    Kind = "markdown",
                    SourceRootLabel = "test",
                    ExtractedText = "Grounding systems provide a low-resistance path for fault currents.",
                    Topics = ["grounding", "fault current"],
                },
            ],
        };

        var context = await KnowledgePromptContextBuilder.BuildRelevantContextWithSemanticSearchAsync(
            library,
            hints: ["grounding"],
            embeddingService: null,
            vectorStoreService: null,
            maxDocuments: 3);

        Assert.NotEqual("none recorded", context);
        Assert.Contains("grounding.md", context, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Chunk 1: KnowledgeCoordinator — status retrieval
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Chunk1_KnowledgeCoordinator_RetrievesIndexStatus_ReflectsTotalDocuments()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildKnowledgeCoordinator(new OfficeDatabase(tmp.Path));

        var status = await coordinator.GetKnowledgeIndexStatusAsync(totalDocuments: 20);

        Assert.Equal(20, status.TotalDocuments);
        Assert.Equal(0, status.IndexedDocuments);
    }

    [Fact]
    public async Task Chunk1_KnowledgeCoordinator_RetrievesIndexStatus_ReflectsIndexedCount()
    {
        using var tmp = new TempDirectory();
        var db = new OfficeDatabase(tmp.Path);
        var indexStore = new KnowledgeIndexStore(db);

        indexStore.MarkIndexed("doc/a.md", "hash-a", "vec-a");
        indexStore.MarkIndexed("doc/b.md", "hash-b", "vec-b");
        indexStore.MarkIndexed("doc/c.md", "hash-c", "vec-c");

        var coordinator = new KnowledgeCoordinator(
            BuildUnavailableEmbeddingService(),
            BuildUnavailableVectorStore(),
            indexStore);

        var status = await coordinator.GetKnowledgeIndexStatusAsync(totalDocuments: 10);

        Assert.Equal(10, status.TotalDocuments);
        Assert.Equal(3, status.IndexedDocuments);
    }

    [Fact]
    public async Task Chunk1_KnowledgeCoordinator_RetrievesIndexStatus_VectorStoreUnreachable()
    {
        using var tmp = new TempDirectory();
        var coordinator = BuildKnowledgeCoordinator(new OfficeDatabase(tmp.Path));

        var status = await coordinator.GetKnowledgeIndexStatusAsync(totalDocuments: 5);

        Assert.Equal("unreachable", status.VectorStoreStatus);
        Assert.Equal(0UL, status.VectorStorePoints);
    }

    // -------------------------------------------------------------------------
    // Chunk 1: Coordinator independence — both coordinators share a database
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Chunk1_BothCoordinators_ShareDatabase_WithoutConflict()
    {
        using var tmp = new TempDirectory();
        var db = new OfficeDatabase(tmp.Path);

        var mlCoordinator = BuildMLCoordinator(db);
        var knowledgeCoordinator = BuildKnowledgeCoordinator(db);

        // Each coordinator operates on a different store within the same database
        var analyticsTask = mlCoordinator.RunMLAnalyticsAsync(MakeSampleAttempts(), []);
        var indexTask = knowledgeCoordinator.RunKnowledgeIndexAsync([]);

        await Task.WhenAll(analyticsTask, indexTask);

        var mlResult = await analyticsTask;
        var kResult = await indexTask;

        Assert.NotNull(mlResult);
        Assert.NotNull(kResult);
    }

    [Fact]
    public async Task Chunk1_BothCoordinators_ProduceIsolatedResults()
    {
        using var tmp = new TempDirectory();
        var db = new OfficeDatabase(tmp.Path);
        var mlStore = new MLResultStore(db);
        var indexStore = new KnowledgeIndexStore(db);

        var mlCoordinator = new MLPipelineCoordinator(BuildFallbackMlService(), mlStore);
        var knowledgeCoordinator = new KnowledgeCoordinator(
            BuildUnavailableEmbeddingService(),
            BuildUnavailableVectorStore(),
            indexStore);

        await mlCoordinator.RunMLAnalyticsAsync(MakeSampleAttempts(), []);
        indexStore.MarkIndexed("doc/test.md", "hash-test", "vec-test");

        var mlStatus = mlStore.LoadAnalytics();
        var knowledgeStatus = await knowledgeCoordinator.GetKnowledgeIndexStatusAsync(totalDocuments: 1);

        // ML store has analytics; knowledge store has indexed document
        Assert.NotNull(mlStatus);
        Assert.Equal(1, knowledgeStatus.IndexedDocuments);
    }
}
