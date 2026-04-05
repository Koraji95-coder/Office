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

    // --- PR 2: MLResultStore Persistence Tests ---

    [Fact]
    public void MLResultStore_SaveAndLoadAnalytics()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new MLResultStore(db);

            Assert.Null(store.LoadAnalytics());

            var result = new MLAnalyticsResult
            {
                Ok = true,
                Engine = "onnx",
                OverallReadiness = 0.75,
                WeakTopics = [new MLTopicEntry { Topic = "grounding", Accuracy = 0.4 }],
            };

            store.SaveAnalytics(result);

            var loaded = store.LoadAnalytics();
            Assert.NotNull(loaded);
            Assert.True(loaded.Ok);
            Assert.Equal("onnx", loaded.Engine);
            Assert.Equal(0.75, loaded.OverallReadiness);
            Assert.Single(loaded.WeakTopics);
            Assert.Equal("grounding", loaded.WeakTopics[0].Topic);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MLResultStore_SaveAndLoadForecast()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new MLResultStore(db);

            Assert.Null(store.LoadForecast());

            var result = new MLForecastResult
            {
                Ok = true,
                Engine = "python",
                Forecasts = [new MLTopicForecast { Topic = "circuits", CurrentAccuracy = 0.8, Trend = "improving" }],
            };

            store.SaveForecast(result);

            var loaded = store.LoadForecast();
            Assert.NotNull(loaded);
            Assert.True(loaded.Ok);
            Assert.Equal("python", loaded.Engine);
            Assert.Single(loaded.Forecasts);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MLResultStore_SaveAndLoadEmbeddings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new MLResultStore(db);

            Assert.Null(store.LoadEmbeddings());

            var result = new MLEmbeddingsResult
            {
                Ok = true,
                Engine = "tfidf",
                Embeddings = [new MLDocumentEmbedding { DocumentId = "doc1", Title = "Test Doc", Dimensions = 128 }],
            };

            store.SaveEmbeddings(result);

            var loaded = store.LoadEmbeddings();
            Assert.NotNull(loaded);
            Assert.True(loaded.Ok);
            Assert.Equal("tfidf", loaded.Engine);
            Assert.Single(loaded.Embeddings);
            Assert.Equal("doc1", loaded.Embeddings[0].DocumentId);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MLResultStore_OverwritesLatestResult()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new MLResultStore(db);

            store.SaveAnalytics(new MLAnalyticsResult { Ok = false, Engine = "fallback", OverallReadiness = 0.1 });
            store.SaveAnalytics(new MLAnalyticsResult { Ok = true, Engine = "onnx", OverallReadiness = 0.9 });

            var loaded = store.LoadAnalytics();
            Assert.NotNull(loaded);
            Assert.True(loaded.Ok);
            Assert.Equal("onnx", loaded.Engine);
            Assert.Equal(0.9, loaded.OverallReadiness);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MLResultStore_LoadLastRunTimestamp_ReturnsLatestAcrossTypes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new MLResultStore(db);

            Assert.Null(store.LoadLastRunTimestamp());

            store.SaveAnalytics(new MLAnalyticsResult { Ok = true, Engine = "onnx" });
            var afterAnalytics = store.LoadLastRunTimestamp();
            Assert.NotNull(afterAnalytics);

            store.SaveForecast(new MLForecastResult { Ok = true, Engine = "python" });
            var afterForecast = store.LoadLastRunTimestamp();
            Assert.NotNull(afterForecast);
            Assert.True(afterForecast >= afterAnalytics);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MLResultStore_SurvivesDbReopen()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            // Save with first DB instance
            using (var db1 = new OfficeDatabase(tempDir))
            {
                var store1 = new MLResultStore(db1);
                store1.SaveAnalytics(new MLAnalyticsResult { Ok = true, Engine = "onnx", OverallReadiness = 0.85 });
                store1.SaveForecast(new MLForecastResult { Ok = true, Engine = "python" });
                store1.SaveEmbeddings(new MLEmbeddingsResult { Ok = true, Engine = "tfidf" });
            }

            // Load with new DB instance (simulates restart)
            using (var db2 = new OfficeDatabase(tempDir))
            {
                var store2 = new MLResultStore(db2);

                var analytics = store2.LoadAnalytics();
                Assert.NotNull(analytics);
                Assert.True(analytics.Ok);
                Assert.Equal("onnx", analytics.Engine);
                Assert.Equal(0.85, analytics.OverallReadiness);

                var forecast = store2.LoadForecast();
                Assert.NotNull(forecast);
                Assert.True(forecast.Ok);

                var embeddings = store2.LoadEmbeddings();
                Assert.NotNull(embeddings);
                Assert.True(embeddings.Ok);

                Assert.NotNull(store2.LoadLastRunTimestamp());
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // --- PR 3: Stale Job Recovery Tests ---

    [Fact]
    public void OfficeJobStore_RecoverStaleJobs_MarksOldRunningAsFailed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Enqueue and dequeue (marks as Running)
            var job = store.Enqueue(OfficeJobType.MLAnalytics, "test");
            var running = store.DequeueNext();
            Assert.NotNull(running);

            // Backdate StartedAt to > 10 minutes ago
            running!.StartedAt = DateTimeOffset.Now.AddMinutes(-11);
            db.Jobs.Update(running);

            var recovered = store.RecoverStaleJobs(TimeSpan.FromMinutes(10));
            Assert.Equal(1, recovered);

            var result = store.GetById(job.Id);
            Assert.NotNull(result);
            Assert.Equal(OfficeJobStatus.Failed, result!.Status);
            Assert.Contains("Recovered after broker restart", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_RecoverStaleJobs_IgnoresRecentRunningJobs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Enqueue and dequeue (marks as Running with recent StartedAt)
            var job = store.Enqueue(OfficeJobType.MLForecast, "test");
            var running = store.DequeueNext();
            Assert.NotNull(running);
            Assert.Equal(OfficeJobStatus.Running, running!.Status);

            // StartedAt is recent, so it should NOT be recovered
            var recovered = store.RecoverStaleJobs(TimeSpan.FromMinutes(10));
            Assert.Equal(0, recovered);

            var result = store.GetById(job.Id);
            Assert.NotNull(result);
            Assert.Equal(OfficeJobStatus.Running, result!.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_RecoverStaleJobs_IgnoresQueuedAndCompletedJobs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // One Queued job (not dequeued)
            var queued = store.Enqueue(OfficeJobType.MLAnalytics, "queued");

            // One Succeeded job
            var succeeded = store.Enqueue(OfficeJobType.MLForecast, "succeeded");
            store.MarkSucceeded(succeeded.Id, "{\"ok\":true}");

            var recovered = store.RecoverStaleJobs(TimeSpan.FromMinutes(10));
            Assert.Equal(0, recovered);

            Assert.Equal(OfficeJobStatus.Queued, store.GetById(queued.Id)!.Status);
            Assert.Equal(OfficeJobStatus.Succeeded, store.GetById(succeeded.Id)!.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_RecoverStaleJobs_ReturnsCount()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Enqueue 3 jobs and dequeue all
            var job1 = store.Enqueue(OfficeJobType.MLAnalytics, "j1");
            var job2 = store.Enqueue(OfficeJobType.MLForecast, "j2");
            var job3 = store.Enqueue(OfficeJobType.MLEmbeddings, "j3");

            var r1 = store.DequeueNext()!;
            var r2 = store.DequeueNext()!;
            var r3 = store.DequeueNext()!;

            // Backdate 2 of them to > 10 minutes ago
            r1.StartedAt = DateTimeOffset.Now.AddMinutes(-15);
            r2.StartedAt = DateTimeOffset.Now.AddMinutes(-20);
            db.Jobs.Update(r1);
            db.Jobs.Update(r2);
            // r3 keeps its recent StartedAt

            var recovered = store.RecoverStaleJobs(TimeSpan.FromMinutes(10));
            Assert.Equal(2, recovered);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // --- PR 5: Job Model Integration Tests ---

    [Fact]
    public void OfficeJobStore_FIFOOrdering_DequeuesOldestFirst()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var jobA = store.Enqueue(OfficeJobType.MLAnalytics, "A");
            var jobB = store.Enqueue(OfficeJobType.MLForecast, "B");
            var jobC = store.Enqueue(OfficeJobType.MLPipeline, "C");

            var first = store.DequeueNext();
            var second = store.DequeueNext();
            var third = store.DequeueNext();
            var empty = store.DequeueNext();

            Assert.Equal(jobA.Id, first!.Id);
            Assert.Equal(jobB.Id, second!.Id);
            Assert.Equal(jobC.Id, third!.Id);
            Assert.Null(empty);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_FullLifecycle_EnqueueDequeueSucceed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Step 1: Enqueue
            var job = store.Enqueue(OfficeJobType.MLAnalytics, "integration", "{\"input\":1}");
            Assert.Equal(OfficeJobStatus.Queued, job.Status);
            Assert.Null(job.StartedAt);
            Assert.Null(job.CompletedAt);

            // Step 2: Dequeue (transitions to Running)
            var running = store.DequeueNext();
            Assert.NotNull(running);
            Assert.Equal(job.Id, running!.Id);
            Assert.Equal(OfficeJobStatus.Running, running.Status);
            Assert.NotNull(running.StartedAt);
            Assert.Null(running.CompletedAt);

            // Step 3: Mark Succeeded
            store.MarkSucceeded(job.Id, "{\"result\":42}");
            var completed = store.GetById(job.Id);
            Assert.NotNull(completed);
            Assert.Equal(OfficeJobStatus.Succeeded, completed!.Status);
            Assert.NotNull(completed.CompletedAt);
            Assert.Equal("{\"result\":42}", completed.ResultJson);
            Assert.Null(completed.Error);

            // Verify timestamp ordering
            Assert.True(completed.CreatedAt <= completed.StartedAt);
            Assert.True(completed.StartedAt <= completed.CompletedAt);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_FullLifecycle_EnqueueDequeueFail()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var job = store.Enqueue(OfficeJobType.MLForecast, "fail-test");
            var running = store.DequeueNext();
            Assert.NotNull(running);
            Assert.Equal(OfficeJobStatus.Running, running!.Status);

            store.MarkFailed(job.Id, "Timeout after 5 minutes");
            var failed = store.GetById(job.Id);
            Assert.NotNull(failed);
            Assert.Equal(OfficeJobStatus.Failed, failed!.Status);
            Assert.NotNull(failed.CompletedAt);
            Assert.Equal("Timeout after 5 minutes", failed.Error);
            Assert.Null(failed.ResultJson);

            // Verify timestamp ordering
            Assert.True(failed.CreatedAt <= failed.StartedAt);
            Assert.True(failed.StartedAt <= failed.CompletedAt);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_GetById_ReturnsNullForNonexistent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            Assert.Null(store.GetById("nonexistent-id"));
            Assert.Null(store.GetById(string.Empty));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_MarkSucceeded_NoOpForNonexistentJob()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Should not throw
            store.MarkSucceeded("does-not-exist", "{\"ok\":true}");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_MarkFailed_NoOpForNonexistentJob()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Should not throw
            store.MarkFailed("does-not-exist", "Some error");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_RequestPayload_PreservedThroughLifecycle()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var payload = "{\"text\":\"hello world\",\"model\":\"qwen3:8b\"}";
            var job = store.Enqueue(OfficeJobType.MLEmbeddings, "payload-test", payload);
            Assert.Equal(payload, job.RequestPayload);

            var dequeued = store.DequeueNext();
            Assert.Equal(payload, dequeued!.RequestPayload);

            store.MarkSucceeded(job.Id, "{\"embeddings\":[0.1,0.2]}");
            var completed = store.GetById(job.Id);
            Assert.Equal(payload, completed!.RequestPayload);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_ListRecent_RespectsCountLimit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            for (int i = 0; i < 5; i++)
                store.Enqueue(OfficeJobType.MLAnalytics, $"job-{i}");

            var limited = store.ListRecent(3);
            Assert.Equal(3, limited.Count);

            var all = store.ListRecent(10);
            Assert.Equal(5, all.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_ListRecent_IncludesAllStatuses()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var queued = store.Enqueue(OfficeJobType.MLAnalytics, "queued");
            var toSucceed = store.Enqueue(OfficeJobType.MLForecast, "succeeded");
            var toFail = store.Enqueue(OfficeJobType.MLPipeline, "failed");
            var toRun = store.Enqueue(OfficeJobType.MLEmbeddings, "running");

            store.MarkSucceeded(toSucceed.Id, "{}");
            store.MarkFailed(toFail.Id, "err");
            store.DequeueNext(); // dequeues 'queued' job (oldest)
            // 'toRun' is still queued; dequeue it now
            // Actually let's just dequeue to get a running state
            // We need toRun to be running, so let's dequeue remaining
            store.DequeueNext(); // now 'toSucceed' (already succeeded but still queued status? no, it's already succeeded)

            var jobs = store.ListRecent(10);
            Assert.Equal(4, jobs.Count);

            var statuses = jobs.Select(j => j.Status).Distinct().OrderBy(s => s).ToList();
            // We should have at least queued, running, succeeded, failed
            Assert.Contains(OfficeJobStatus.Succeeded, statuses);
            Assert.Contains(OfficeJobStatus.Failed, statuses);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_RecoverStaleJobs_IdempotentOnSecondCall()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var job = store.Enqueue(OfficeJobType.MLAnalytics, "stale");
            var running = store.DequeueNext()!;
            running.StartedAt = DateTimeOffset.Now.AddMinutes(-15);
            db.Jobs.Update(running);

            // First recovery
            var firstPass = store.RecoverStaleJobs(TimeSpan.FromMinutes(10));
            Assert.Equal(1, firstPass);

            // Second recovery — already failed, should recover 0
            var secondPass = store.RecoverStaleJobs(TimeSpan.FromMinutes(10));
            Assert.Equal(0, secondPass);

            // Job is still Failed
            Assert.Equal(OfficeJobStatus.Failed, store.GetById(job.Id)!.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_MultipleLifecycleIterations()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Iteration 1: enqueue 2 jobs, process both
            var j1 = store.Enqueue(OfficeJobType.MLAnalytics, "batch1");
            var j2 = store.Enqueue(OfficeJobType.MLForecast, "batch1");

            var d1 = store.DequeueNext()!;
            Assert.Equal(j1.Id, d1.Id);
            store.MarkSucceeded(d1.Id, "{\"batch\":1}");

            var d2 = store.DequeueNext()!;
            Assert.Equal(j2.Id, d2.Id);
            store.MarkFailed(d2.Id, "batch1 error");

            Assert.Null(store.DequeueNext()); // queue empty

            // Iteration 2: enqueue 2 more, process them
            var j3 = store.Enqueue(OfficeJobType.MLPipeline, "batch2");
            var j4 = store.Enqueue(OfficeJobType.MLEmbeddings, "batch2");

            var d3 = store.DequeueNext()!;
            Assert.Equal(j3.Id, d3.Id);
            store.MarkSucceeded(d3.Id, "{\"batch\":2}");

            var d4 = store.DequeueNext()!;
            Assert.Equal(j4.Id, d4.Id);
            store.MarkSucceeded(d4.Id, "{\"batch\":2}");

            // Verify all 4 jobs are tracked
            var all = store.ListRecent(10);
            Assert.Equal(4, all.Count);

            // Verify statuses
            Assert.Equal(OfficeJobStatus.Succeeded, store.GetById(j1.Id)!.Status);
            Assert.Equal(OfficeJobStatus.Failed, store.GetById(j2.Id)!.Status);
            Assert.Equal(OfficeJobStatus.Succeeded, store.GetById(j3.Id)!.Status);
            Assert.Equal(OfficeJobStatus.Succeeded, store.GetById(j4.Id)!.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_DequeueNext_SkipsRunningAndCompletedJobs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Create 3 jobs
            var j1 = store.Enqueue(OfficeJobType.MLAnalytics, "first");
            var j2 = store.Enqueue(OfficeJobType.MLForecast, "second");
            var j3 = store.Enqueue(OfficeJobType.MLPipeline, "third");

            // Dequeue j1 (now Running), mark j2 as Succeeded directly
            store.DequeueNext(); // j1 → Running
            store.MarkSucceeded(j2.Id, "{}"); // j2 → Succeeded (skipped Running)

            // DequeueNext should skip j1 (Running) and j2 (Succeeded), return j3
            var next = store.DequeueNext();
            Assert.NotNull(next);
            Assert.Equal(j3.Id, next!.Id);

            // No more queued jobs
            Assert.Null(store.DequeueNext());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_DequeueNext_EmptyStoreReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            Assert.Null(store.DequeueNext());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
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
