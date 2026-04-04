using DailyDesk.Models;
using DailyDesk.Services;
using System.Reflection;
using Xunit;

namespace DailyDesk.Core.Tests;

public sealed class OfficeBrokerLogicTests
{
    [Theory]
    [InlineData(null, OfficeRouteCatalog.ChiefRoute)]
    [InlineData("", OfficeRouteCatalog.ChiefRoute)]
    [InlineData("engineering", OfficeRouteCatalog.EngineeringRoute)]
    [InlineData("BUSINESS", OfficeRouteCatalog.BusinessRoute)]
    [InlineData("unknown", OfficeRouteCatalog.ChiefRoute)]
    [InlineData("ml", OfficeRouteCatalog.MLRoute)]
    [InlineData("ML", OfficeRouteCatalog.MLRoute)]
    public void NormalizeRoute_ReturnsExpectedValue(string? route, string expected)
    {
        var actual = OfficeRouteCatalog.NormalizeRoute(route);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(OfficeRouteCatalog.BusinessRoute, "Growth Ops")]
    [InlineData(OfficeRouteCatalog.ChiefRoute, "Chief of Staff")]
    [InlineData(OfficeRouteCatalog.MLRoute, "ML Engineer")]
    public void ResolveRouteDisplayTitle_ReturnsExpectedLabel(string route, string expectedTitle)
    {
        var actual = OfficeRouteCatalog.ResolveRouteDisplayTitle(route);
        Assert.Equal(expectedTitle, actual);
    }

    [Fact]
    public void ResolveStage_TransitionsToCompleteWhenReflectionIsSaved()
    {
        var state = new OfficeLiveSessionState
        {
            PracticeGenerated = true,
            PracticeScored = true,
            DefenseGenerated = true,
            DefenseScored = true,
            ReflectionSaved = true,
        };

        var stage = OfficeStudySessionLogic.ResolveStage(state);
        Assert.Equal(TrainingSessionStage.Complete, stage);
    }

    [Fact]
    public void ResolveOfficeRootPath_SkipsArtifactTreesWithoutProjectFile()
    {
        var resolveMethod = typeof(OfficeBrokerOrchestrator).GetMethod(
            "ResolveOfficeRootPath",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.NotNull(resolveMethod);

        // Derive the real repo root by walking up from the test project directory
        // until we find the DailyDesk/DailyDesk.csproj marker.
        var repoRoot = FindRepoRoot();
        Assert.NotNull(repoRoot);

        var baseDirectory = Path.Combine(
            repoRoot!,
            "artifacts",
            "DailyDesk.Broker",
            "publish"
        );

        var actual = (string?)resolveMethod!.Invoke(null, [baseDirectory]);

        Assert.Equal(
            Path.GetFullPath(repoRoot!),
            actual
        );
    }

    [Fact]
    public void RewriteBaselineAssertion_UpdatesLegacyModelLoreToUnifiedBaseline()
    {
        const string unifiedModel = "qwen3:8b";
        const string content =
            "ANSWER\nThe Office baseline model is currently using the `qwen3:14b` Ollama model for all roles, as per the latest research integration.\n\nCAD OR SUITE LINK\nThe `qwen3:14b` model's output must stay review-first.";

        var actual = OfficeHistoricalStateNormalizer.RewriteBaselineAssertion(
            content,
            unifiedModel
        );

        Assert.DoesNotContain("qwen3:14b", actual, StringComparison.Ordinal);
        Assert.Contains("`qwen3:8b`", actual, StringComparison.Ordinal);
    }

    [Fact]
    public void LearningProfileBuild_UsesHumanFriendlyKnowledgeLibraryLanguage()
    {
        var service = new LearningProfileService();
        var knowledgePath = Path.Combine(Path.GetTempPath(), "OfficeTestKnowledge");
        var library = new LearningLibrary
        {
            RootPath = knowledgePath,
            Documents = [],
        };

        var profile = service.Build(library, new TrainingHistorySummary(), new SuiteSnapshot());

        Assert.Contains("knowledge library", profile.CurrentNeed, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(knowledgePath, profile.CurrentNeed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MLRouteIsRegisteredInKnownRoutes()
    {
        Assert.Contains(OfficeRouteCatalog.MLRoute, OfficeRouteCatalog.KnownRoutes);
    }

    [Theory]
    [InlineData(OfficeRouteCatalog.MLRoute, "ML Engineer")]
    public void ResolvePerspective_ReturnsMLEngineerForMLRoute(string route, string expectedPerspective)
    {
        var actual = OfficeRouteCatalog.ResolvePerspective(route);
        Assert.Equal(expectedPerspective, actual);
    }

    [Fact]
    public void MLRouteTitle_IsMLEngineer()
    {
        var title = OfficeRouteCatalog.ResolveRouteTitle(OfficeRouteCatalog.MLRoute);
        Assert.Equal("ML Engineer", title);
    }

    [Fact]
    public void MLEngineerSystemPrompt_ContainsMLFrameworks()
    {
        var prompt = PromptComposer.BuildMLEngineerSystemPrompt();
        Assert.Contains("Scikit-learn", prompt, StringComparison.Ordinal);
        Assert.Contains("PyTorch", prompt, StringComparison.Ordinal);
        Assert.Contains("TensorFlow", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void MLEngineerUserPrompt_IncludesAnalyticsContext()
    {
        var analytics = new MLAnalyticsResult
        {
            Ok = true,
            Engine = "sklearn",
            OverallReadiness = 0.72,
            WeakTopics = new List<MLTopicEntry>
            {
                new() { Topic = "grounding", Accuracy = 0.45 },
            },
            OperatorPattern = new MLOperatorPattern { Pattern = "balanced" },
        };

        var prompt = PromptComposer.BuildMLEngineerUserPrompt(
            analytics,
            null,
            null,
            new LearningProfile(),
            new TrainingHistorySummary()
        );

        Assert.Contains("sklearn", prompt, StringComparison.Ordinal);
        Assert.Contains("grounding", prompt, StringComparison.Ordinal);
        Assert.Contains("balanced", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void DailySettings_MLDefaults()
    {
        var settings = new DailySettings();
        Assert.Equal("qwen3:8b", settings.MLModel);
        Assert.False(settings.EnableMLPipeline);
        Assert.Equal(string.Empty, settings.MLArtifactExportPath);
    }

    [Fact]
    public void OfficeMLSection_DisabledByDefault()
    {
        var section = new OfficeMLSection();
        Assert.False(section.Enabled);
        Assert.Null(section.Analytics);
        Assert.Null(section.Forecast);
        Assert.Null(section.Embeddings);
    }

    [Fact]
    public void MLAnalyticsResult_FallbackDefaults()
    {
        var result = new MLAnalyticsResult { Ok = true, Engine = "fallback" };
        Assert.True(result.Ok);
        Assert.Equal("fallback", result.Engine);
        Assert.Empty(result.WeakTopics);
        Assert.Empty(result.StrongTopics);
        Assert.Equal(0.0, result.OverallReadiness);
    }

    [Fact]
    public void SuiteMLArtifact_HasCorrectDefaults()
    {
        var artifact = new SuiteMLArtifact();
        Assert.Equal("1.0.0", artifact.Version);
        Assert.Equal("office-ml-pipeline", artifact.Source);
        Assert.True(artifact.ReviewRequired);
    }

    [Fact]
    public void OnnxMLEngine_ReportsNoModelsWhenDirectoryMissing()
    {
        var engine = new OnnxMLEngine(Path.Combine(Path.GetTempPath(), "non-existent-dir"));
        Assert.False(engine.HasAnyModel);
        Assert.False(engine.IsAnalyticsModelAvailable);
        Assert.False(engine.IsEmbeddingsModelAvailable);
        Assert.False(engine.IsForecastModelAvailable);
    }

    [Fact]
    public void OnnxMLEngine_ReturnsNullWhenNoModels()
    {
        var engine = new OnnxMLEngine(Path.Combine(Path.GetTempPath(), "non-existent-dir"));

        var analyticsResult = engine.RunAnalytics([], []);
        Assert.Null(analyticsResult);

        var embeddingsResult = engine.RunEmbeddings([], null);
        Assert.Null(embeddingsResult);

        var forecastResult = engine.RunForecast([]);
        Assert.Null(forecastResult);
    }

    [Fact]
    public async Task MLAnalyticsService_FallsBackWhenNoPythonOrOnnx()
    {
        var processRunner = new ProcessRunner();
        var service = new MLAnalyticsService(
            processRunner,
            Path.Combine(Path.GetTempPath(), "no-scripts"),
            onnxEngine: new OnnxMLEngine(Path.Combine(Path.GetTempPath(), "no-models")),
            cacheTtl: TimeSpan.Zero
        );

        var attempts = new List<TrainingAttemptRecord>
        {
            new()
            {
                CompletedAt = DateTimeOffset.Now,
                Questions =
                [
                    new TrainingAttemptQuestionRecord { Topic = "grounding", Correct = true },
                    new TrainingAttemptQuestionRecord { Topic = "grounding", Correct = false },
                    new TrainingAttemptQuestionRecord { Topic = "protection", Correct = true },
                ],
            },
        };

        var result = await service.RunLearningAnalyticsAsync(attempts, []);

        Assert.Equal("fallback", result.Engine);
        Assert.False(result.Ok);
        Assert.Equal(0.5, result.WeakTopics.First(t => t.Topic == "grounding").Accuracy);
        Assert.Equal(1.0, result.StrongTopics.First(t => t.Topic == "protection").Accuracy);
    }

    [Fact]
    public async Task MLAnalyticsService_CachesResults()
    {
        var processRunner = new ProcessRunner();
        var service = new MLAnalyticsService(
            processRunner,
            Path.Combine(Path.GetTempPath(), "no-scripts"),
            onnxEngine: null,
            cacheTtl: TimeSpan.FromMinutes(10)
        );

        var attempts = new List<TrainingAttemptRecord>
        {
            new()
            {
                CompletedAt = DateTimeOffset.Now,
                Questions =
                [
                    new TrainingAttemptQuestionRecord { Topic = "test", Correct = true },
                ],
            },
        };

        var result1 = await service.RunLearningAnalyticsAsync(attempts, []);
        var result2 = await service.RunLearningAnalyticsAsync(attempts, []);

        // Same object reference means the cache was used
        Assert.Same(result1, result2);
    }

    [Fact]
    public async Task MLAnalyticsService_CacheInvalidation()
    {
        var processRunner = new ProcessRunner();
        var service = new MLAnalyticsService(
            processRunner,
            Path.Combine(Path.GetTempPath(), "no-scripts"),
            onnxEngine: null,
            cacheTtl: TimeSpan.FromMinutes(10)
        );

        var attempts = new List<TrainingAttemptRecord>
        {
            new()
            {
                CompletedAt = DateTimeOffset.Now,
                Questions =
                [
                    new TrainingAttemptQuestionRecord { Topic = "test", Correct = true },
                ],
            },
        };

        var result1 = await service.RunLearningAnalyticsAsync(attempts, []);
        service.InvalidateCache();
        var result2 = await service.RunLearningAnalyticsAsync(attempts, []);

        // After invalidation, a new result should be computed
        Assert.NotSame(result1, result2);
        Assert.Equal(result1.Engine, result2.Engine);
    }

    [Fact]
    public async Task MLAnalyticsService_ForecastFallbackReturnsEngineField()
    {
        var processRunner = new ProcessRunner();
        var service = new MLAnalyticsService(
            processRunner,
            Path.Combine(Path.GetTempPath(), "no-scripts"),
            cacheTtl: TimeSpan.Zero
        );

        var result = await service.RunProgressForecastAsync([]);

        Assert.False(result.Ok);
        Assert.Equal("fallback", result.Engine);
    }

    [Fact]
    public async Task MLAnalyticsService_EmbeddingsFallbackReturnsEngineField()
    {
        var processRunner = new ProcessRunner();
        var service = new MLAnalyticsService(
            processRunner,
            Path.Combine(Path.GetTempPath(), "no-scripts"),
            cacheTtl: TimeSpan.Zero
        );

        var result = await service.RunDocumentEmbeddingsAsync([]);

        Assert.False(result.Ok);
        Assert.Equal("fallback", result.Engine);
    }

    [Fact]
    public void MLAnalyticsService_ResolveAvailableEngine_ReportsFallback()
    {
        var processRunner = new ProcessRunner();
        var service = new MLAnalyticsService(
            processRunner,
            Path.Combine(Path.GetTempPath(), "no-scripts"),
            onnxEngine: new OnnxMLEngine(Path.Combine(Path.GetTempPath(), "no-models"))
        );

        // Without ONNX models and possibly without Python,
        // the engine should be either "python" or "fallback"
        var engine = service.ResolveAvailableEngine();
        Assert.NotEqual("onnx", engine);
    }

    [Fact]
    public void OnnxMLEngine_Dispose_IsIdempotent()
    {
        var engine = new OnnxMLEngine(Path.Combine(Path.GetTempPath(), "no-models"));
        engine.Dispose();
        engine.Dispose(); // Should not throw
    }

    // --- Phase 2: LiteDB Persistence Tests ---

    [Fact]
    public void OfficeDatabase_CreatesAndDisposes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            var db = new OfficeDatabase(tempDir);
            Assert.NotNull(db.Jobs);
            Assert.NotNull(db.PracticeAttempts);
            db.Dispose();

            Assert.True(File.Exists(Path.Combine(tempDir, "office.db")));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeDatabase_MigrationTracking()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            Assert.False(db.HasMigrated("test-store"));
            db.MarkMigrated("test-store");
            Assert.True(db.HasMigrated("test-store"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // --- Phase 3: Job Model Tests ---

    [Fact]
    public void OfficeJob_DefaultValues()
    {
        var job = new OfficeJob();
        Assert.NotNull(job.Id);
        Assert.Equal(OfficeJobStatus.Queued, job.Status);
        Assert.Equal(string.Empty, job.Type);
        Assert.Null(job.Error);
        Assert.Null(job.ResultJson);
    }

    [Fact]
    public void OfficeJobStore_EnqueueAndRetrieve()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var job = store.Enqueue(OfficeJobType.MLAnalytics, "test");
            Assert.Equal(OfficeJobStatus.Queued, job.Status);
            Assert.Equal(OfficeJobType.MLAnalytics, job.Type);

            var retrieved = store.GetById(job.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(job.Id, retrieved!.Id);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_DequeueNextSetsRunning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var job = store.Enqueue(OfficeJobType.MLForecast, "test");
            var dequeued = store.DequeueNext();

            Assert.NotNull(dequeued);
            Assert.Equal(job.Id, dequeued!.Id);
            Assert.Equal(OfficeJobStatus.Running, dequeued.Status);
            Assert.NotNull(dequeued.StartedAt);

            // No more queued jobs
            Assert.Null(store.DequeueNext());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_MarkSucceeded()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var job = store.Enqueue(OfficeJobType.MLPipeline, "test");
            store.MarkSucceeded(job.Id, "{\"ok\":true}");

            var completed = store.GetById(job.Id);
            Assert.NotNull(completed);
            Assert.Equal(OfficeJobStatus.Succeeded, completed!.Status);
            Assert.NotNull(completed.CompletedAt);
            Assert.Equal("{\"ok\":true}", completed.ResultJson);
            Assert.Null(completed.Error);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_MarkFailed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var job = store.Enqueue(OfficeJobType.MLEmbeddings, "test");
            store.MarkFailed(job.Id, "Something went wrong");

            var failed = store.GetById(job.Id);
            Assert.NotNull(failed);
            Assert.Equal(OfficeJobStatus.Failed, failed!.Status);
            Assert.NotNull(failed.CompletedAt);
            Assert.Equal("Something went wrong", failed.Error);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_ListRecentReturnsInOrder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            store.Enqueue(OfficeJobType.MLAnalytics, "first");
            store.Enqueue(OfficeJobType.MLForecast, "second");
            store.Enqueue(OfficeJobType.MLPipeline, "third");

            var jobs = store.ListRecent(10);
            Assert.Equal(3, jobs.Count);
            Assert.Equal("third", jobs[0].RequestedBy);
            Assert.Equal("first", jobs[2].RequestedBy);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // --- Phase 2: Polly Resilience Pipeline Tests ---

    [Fact]
    public void OfficeResiliencePipelines_OllamaBuilds()
    {
        var pipeline = OfficeResiliencePipelines.BuildOllamaPipeline();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void OfficeResiliencePipelines_WebResearchBuilds()
    {
        var pipeline = OfficeResiliencePipelines.BuildWebResearchPipeline();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void OfficeResiliencePipelines_PythonSubprocessBuilds()
    {
        var pipeline = OfficeResiliencePipelines.BuildPythonSubprocessPipeline();
        Assert.NotNull(pipeline);
    }

    // --- Phase 2: LiteDB-backed TrainingStore Tests ---

    [Fact]
    public async Task TrainingStore_LiteDB_SaveAndLoadPracticeAttempt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new TrainingStore(tempDir, db);

            var attempt = new TrainingAttemptRecord
            {
                CompletedAt = DateTimeOffset.Now,
                QuestionCount = 2,
                CorrectCount = 1,
                Questions =
                [
                    new TrainingAttemptQuestionRecord { Topic = "grounding", Correct = true },
                    new TrainingAttemptQuestionRecord { Topic = "protection", Correct = false },
                ],
            };

            var summary = await store.SavePracticeAttemptAsync(attempt);
            Assert.Equal(1, summary.TotalAttempts);
            Assert.Equal(2, summary.TotalQuestions);
            Assert.Equal(1, summary.CorrectAnswers);

            var allAttempts = store.LoadAllAttempts();
            Assert.Single(allAttempts);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TrainingStore_LiteDB_Reset()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new TrainingStore(tempDir, db);

            await store.SavePracticeAttemptAsync(new TrainingAttemptRecord
            {
                CompletedAt = DateTimeOffset.Now,
                Questions = [new TrainingAttemptQuestionRecord { Topic = "test", Correct = true }],
            });

            var summary = await store.ResetAsync();
            Assert.Equal(0, summary.TotalAttempts);
            Assert.Empty(store.LoadAllAttempts());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData("chief", true)]
    [InlineData("engineering", true)]
    [InlineData("suite", true)]
    [InlineData("business", true)]
    [InlineData("ml", true)]
    [InlineData("CHIEF", true)]
    [InlineData("unknown", false)]
    [InlineData("admin", false)]
    [InlineData("", false)]
    public void KnownRoutes_ContainsExpectedRoutes(string route, bool expected)
    {
        var isKnown = !string.IsNullOrWhiteSpace(route)
            && OfficeRouteCatalog.KnownRoutes.Contains(route, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expected, isKnown);
    }

    [Fact]
    public void ProcessRunner_CanBeCreatedWithoutLogger()
    {
        var runner = new ProcessRunner();
        Assert.NotNull(runner);
    }

    private static string? FindRepoRoot()
    {
        // Walk up from the test assembly's directory to find the repo root
        // (the directory containing DailyDesk/DailyDesk.csproj).
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var marker = Path.Combine(dir, "DailyDesk", "DailyDesk.csproj");
            if (File.Exists(marker))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}
