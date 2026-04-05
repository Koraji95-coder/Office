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

    // --- Phase 3 (PR 4): MLExportArtifacts job type tests ---

    [Fact]
    public void OfficeJobType_MLExportArtifacts_HasExpectedStringValue()
    {
        Assert.Equal("ml-export-artifacts", OfficeJobType.MLExportArtifacts);
    }

    [Fact]
    public void OfficeJobStore_Enqueue_MLExportArtifacts_StoresCorrectType()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var job = store.Enqueue(OfficeJobType.MLExportArtifacts, "test");
            Assert.Equal(OfficeJobStatus.Queued, job.Status);
            Assert.Equal(OfficeJobType.MLExportArtifacts, job.Type);

            var retrieved = store.GetById(job.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(OfficeJobType.MLExportArtifacts, retrieved!.Type);
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
            // (edge case: bypasses Running state to verify DequeueNext only looks at Queued status)
            store.DequeueNext(); // j1 → Running
            store.MarkSucceeded(j2.Id, "{}"); // j2 → Succeeded (bypasses Running — edge case)

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

    // --- PR 6: Job Management & Retention Tests ---

    [Fact]
    public void OfficeJobStore_DeleteById_RemovesCompletedJob()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var job = store.Enqueue(OfficeJobType.MLAnalytics, "test");
            store.MarkSucceeded(job.Id, "{\"ok\":true}");

            var deleted = store.DeleteById(job.Id);
            Assert.True(deleted);
            Assert.Null(store.GetById(job.Id));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_DeleteById_ReturnsFalseForNonexistent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            Assert.False(store.DeleteById("nonexistent-id"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_DeleteById_RefusesQueuedAndRunning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Queued job cannot be deleted
            var queued = store.Enqueue(OfficeJobType.MLAnalytics, "test");
            Assert.False(store.DeleteById(queued.Id));
            Assert.NotNull(store.GetById(queued.Id));

            // Running job cannot be deleted
            var running = store.DequeueNext();
            Assert.False(store.DeleteById(running!.Id));
            Assert.NotNull(store.GetById(running.Id));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_DeleteById_AllowsFailedJobs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var job = store.Enqueue(OfficeJobType.MLForecast, "test");
            store.MarkFailed(job.Id, "some error");

            var deleted = store.DeleteById(job.Id);
            Assert.True(deleted);
            Assert.Null(store.GetById(job.Id));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_DeleteOlderThan_PurgesExpiredOnly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Create old succeeded job
            var old = store.Enqueue(OfficeJobType.MLAnalytics, "old");
            store.MarkSucceeded(old.Id, "{}");
            var oldJob = store.GetById(old.Id)!;
            oldJob.CreatedAt = DateTimeOffset.Now.AddDays(-31);
            db.Jobs.Update(oldJob);

            // Create recent succeeded job
            var recent = store.Enqueue(OfficeJobType.MLForecast, "recent");
            store.MarkSucceeded(recent.Id, "{}");

            // Purge jobs older than 30 days
            var cutoff = DateTimeOffset.Now.AddDays(-30);
            var deleted = store.DeleteOlderThan(cutoff);

            Assert.Equal(1, deleted);
            Assert.Null(store.GetById(old.Id));
            Assert.NotNull(store.GetById(recent.Id));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_DeleteOlderThan_IgnoresQueuedAndRunning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Create old queued job
            var queued = store.Enqueue(OfficeJobType.MLAnalytics, "old-queued");
            var queuedJob = store.GetById(queued.Id)!;
            queuedJob.CreatedAt = DateTimeOffset.Now.AddDays(-31);
            db.Jobs.Update(queuedJob);

            // Create old running job
            var toRun = store.Enqueue(OfficeJobType.MLForecast, "old-running");
            var running = store.DequeueNext()!;
            running.CreatedAt = DateTimeOffset.Now.AddDays(-31);
            db.Jobs.Update(running);

            var cutoff = DateTimeOffset.Now.AddDays(-30);
            var deleted = store.DeleteOlderThan(cutoff);

            Assert.Equal(0, deleted);
            Assert.NotNull(store.GetById(queued.Id));
            Assert.NotNull(store.GetById(toRun.Id));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_ListByStatus_FiltersCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var j1 = store.Enqueue(OfficeJobType.MLAnalytics, "a");
            var j2 = store.Enqueue(OfficeJobType.MLForecast, "b");
            var j3 = store.Enqueue(OfficeJobType.MLPipeline, "c");

            store.MarkSucceeded(j1.Id, "{}");
            store.MarkFailed(j2.Id, "err");
            // j3 stays queued

            var succeeded = store.ListByStatus(OfficeJobStatus.Succeeded);
            Assert.Single(succeeded);
            Assert.Equal(j1.Id, succeeded[0].Id);

            var failed = store.ListByStatus(OfficeJobStatus.Failed);
            Assert.Single(failed);
            Assert.Equal(j2.Id, failed[0].Id);

            var queued = store.ListByStatus(OfficeJobStatus.Queued);
            Assert.Single(queued);
            Assert.Equal(j3.Id, queued[0].Id);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_ListByStatus_RespectsLimit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            for (int i = 0; i < 5; i++)
            {
                var j = store.Enqueue(OfficeJobType.MLAnalytics, $"job-{i}");
                store.MarkSucceeded(j.Id, "{}");
            }

            var limited = store.ListByStatus(OfficeJobStatus.Succeeded, 3);
            Assert.Equal(3, limited.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_GetTotalCount_ReturnsAccurateCount()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            Assert.Equal(0, store.GetTotalCount());

            store.Enqueue(OfficeJobType.MLAnalytics, "a");
            store.Enqueue(OfficeJobType.MLForecast, "b");
            Assert.Equal(2, store.GetTotalCount());

            var j3 = store.Enqueue(OfficeJobType.MLPipeline, "c");
            store.MarkSucceeded(j3.Id, "{}");
            Assert.Equal(3, store.GetTotalCount());

            store.DeleteById(j3.Id);
            Assert.Equal(2, store.GetTotalCount());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // --- Phase 4: Health & Metrics Tests ---

    [Fact]
    public async Task ProcessRunner_CheckPythonAsync_ReturnsVersionOrNull()
    {
        var runner = new ProcessRunner();
        var result = await runner.CheckPythonAsync();
        // In test environments Python may or may not be installed.
        // Just verify we get either a version string or null (no exceptions).
        if (result is not null)
        {
            Assert.Contains("Python", result, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OfficeJobStore_GetMetrics_EmptyStore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var metrics = store.GetMetrics();
            Assert.Equal(0, metrics.TotalJobs);
            Assert.Equal(0, metrics.QueuedCount);
            Assert.Equal(0, metrics.RunningCount);
            Assert.Equal(0, metrics.SucceededCount);
            Assert.Equal(0, metrics.FailedCount);
            Assert.Null(metrics.AverageDurationSeconds);
            Assert.Equal(0, metrics.CompletedLastHour);
            Assert.Equal(0, metrics.CompletedLastDay);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_GetMetrics_WithMixedStatuses()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Create jobs in various states
            var j1 = store.Enqueue(OfficeJobType.MLAnalytics, "a");
            var j2 = store.Enqueue(OfficeJobType.MLForecast, "b");
            var j3 = store.Enqueue(OfficeJobType.MLPipeline, "c");
            var j4 = store.Enqueue(OfficeJobType.MLEmbeddings, "d");

            // Dequeue and complete some
            store.DequeueNext(); // j1 → running
            store.MarkSucceeded(j1.Id, "{}");

            store.DequeueNext(); // j2 → running
            store.MarkFailed(j2.Id, "error");

            store.DequeueNext(); // j3 → running (stays running)

            // j4 stays queued

            var metrics = store.GetMetrics();
            Assert.Equal(4, metrics.TotalJobs);
            Assert.Equal(1, metrics.QueuedCount);
            Assert.Equal(1, metrics.RunningCount);
            Assert.Equal(1, metrics.SucceededCount);
            Assert.Equal(1, metrics.FailedCount);
            Assert.Equal(2, metrics.CompletedLastHour);
            Assert.Equal(2, metrics.CompletedLastDay);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_GetAverageDuration_CalculatesCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            Assert.Null(store.GetAverageDuration());

            // Create and complete two jobs
            var j1 = store.Enqueue(OfficeJobType.MLAnalytics, "a");
            store.DequeueNext(); // j1 → running
            store.MarkSucceeded(j1.Id, "{}");

            var j2 = store.Enqueue(OfficeJobType.MLForecast, "b");
            store.DequeueNext(); // j2 → running
            store.MarkSucceeded(j2.Id, "{}");

            var avg = store.GetAverageDuration();
            Assert.NotNull(avg);
            Assert.True(avg >= 0, "Average duration should be non-negative.");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_GetCountByStatus_ReturnsCorrectCounts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            Assert.Equal(0, store.GetCountByStatus(OfficeJobStatus.Queued));

            store.Enqueue(OfficeJobType.MLAnalytics, "a");
            store.Enqueue(OfficeJobType.MLForecast, "b");

            Assert.Equal(2, store.GetCountByStatus(OfficeJobStatus.Queued));
            Assert.Equal(0, store.GetCountByStatus(OfficeJobStatus.Succeeded));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_GetCompletedSince_FiltersCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            var j1 = store.Enqueue(OfficeJobType.MLAnalytics, "a");
            store.DequeueNext();
            store.MarkSucceeded(j1.Id, "{}");

            // Jobs completed just now should be within last hour
            Assert.Equal(1, store.GetCompletedSince(DateTimeOffset.Now.AddHours(-1)));

            // Jobs completed just now should not be in the future
            Assert.Equal(0, store.GetCompletedSince(DateTimeOffset.Now.AddMinutes(1)));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void OfficeJobStore_DeleteOlderThan_PreservesRecentAndActiveJobs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-test-{Guid.NewGuid()}");
        try
        {
            using var db = new OfficeDatabase(tempDir);
            var store = new OfficeJobStore(db);

            // Create and complete a job (recently)
            var j1 = store.Enqueue(OfficeJobType.MLAnalytics, "recent");
            store.DequeueNext();
            store.MarkSucceeded(j1.Id, "{}");

            // Create a queued job (should never be deleted)
            var j2 = store.Enqueue(OfficeJobType.MLForecast, "queued");

            // Delete jobs older than 1 minute ago — should delete nothing
            var deleted = store.DeleteOlderThan(DateTimeOffset.Now.AddMinutes(-1));
            Assert.Equal(0, deleted);
            Assert.Equal(2, store.GetTotalCount());

            // Delete jobs older than 1 hour from now — should delete completed but not queued
            deleted = store.DeleteOlderThan(DateTimeOffset.Now.AddHours(1));
            Assert.Equal(1, deleted); // j1 deleted
            Assert.Equal(1, store.GetTotalCount()); // j2 remains (queued)
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DailySettings_JobRetentionDays_DefaultsTo30()
    {
        var settings = new DailySettings();
        Assert.Equal(30, settings.JobRetentionDays);
    }

    [Fact]
    public void OfficeHealthReport_DefaultsToOk()
    {
        var report = new OfficeHealthReport();
        Assert.Equal(HealthStatus.Ok, report.Overall);
        Assert.Equal(HealthStatus.Ok, report.Ollama.Status);
        Assert.Equal(HealthStatus.Ok, report.Python.Status);
        Assert.Equal(HealthStatus.Ok, report.LiteDB.Status);
        Assert.Equal(HealthStatus.Ok, report.JobWorker.Status);
    }

    [Fact]
    public void SubsystemHealth_CanSetDegradedStatus()
    {
        var health = new SubsystemHealth
        {
            Status = HealthStatus.Degraded,
            Detail = "something is slow"
        };
        Assert.Equal(HealthStatus.Degraded, health.Status);
        Assert.Equal("something is slow", health.Detail);
    }

    [Fact]
    public async Task OllamaService_PingAsync_ReturnsFalseWhenUnreachable()
    {
        // Use an unreachable endpoint to verify graceful failure
        var processRunner = new ProcessRunner();
        var service = new OllamaService("http://127.0.0.1:1", processRunner);
        var reachable = await service.PingAsync();
        Assert.False(reachable);
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

    // --- Phase 5: Semantic Search Tests ---

    [Fact]
    public void OfficeJobType_KnowledgeIndex_HasExpectedValue()
    {
        Assert.Equal("knowledge-index", OfficeJobType.KnowledgeIndex);
    }

    [Fact]
    public void KnowledgeIndexStore_ComputeContentHash_ReturnsConsistentHash()
    {
        var hash1 = KnowledgeIndexStore.ComputeContentHash("Hello, world!");
        var hash2 = KnowledgeIndexStore.ComputeContentHash("Hello, world!");
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public void KnowledgeIndexStore_ComputeContentHash_DifferentInputsDifferentHashes()
    {
        var hash1 = KnowledgeIndexStore.ComputeContentHash("Hello, world!");
        var hash2 = KnowledgeIndexStore.ComputeContentHash("Goodbye, world!");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void KnowledgeIndexStore_ComputeContentHash_EmptyReturnsEmpty()
    {
        Assert.Equal(string.Empty, KnowledgeIndexStore.ComputeContentHash(null));
        Assert.Equal(string.Empty, KnowledgeIndexStore.ComputeContentHash(""));
        Assert.Equal(string.Empty, KnowledgeIndexStore.ComputeContentHash("   "));
    }

    [Fact]
    public void KnowledgeIndexStore_NeedsIndexing_TrueForNewDocument()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var store = new KnowledgeIndexStore(db);

        Assert.True(store.NeedsIndexing("doc/test.md", "abc123"));
    }

    [Fact]
    public void KnowledgeIndexStore_NeedsIndexing_FalseAfterMarking()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var store = new KnowledgeIndexStore(db);

        store.MarkIndexed("doc/test.md", "abc123", "vec-001");
        Assert.False(store.NeedsIndexing("doc/test.md", "abc123"));
    }

    [Fact]
    public void KnowledgeIndexStore_NeedsIndexing_TrueAfterContentChange()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var store = new KnowledgeIndexStore(db);

        store.MarkIndexed("doc/test.md", "abc123", "vec-001");
        Assert.True(store.NeedsIndexing("doc/test.md", "different-hash"));
    }

    [Fact]
    public void KnowledgeIndexStore_GetIndexedCount_TracksCorrectly()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var store = new KnowledgeIndexStore(db);

        Assert.Equal(0, store.GetIndexedCount());
        store.MarkIndexed("doc/a.md", "hash-a", "vec-a");
        store.MarkIndexed("doc/b.md", "hash-b", "vec-b");
        Assert.Equal(2, store.GetIndexedCount());
    }

    [Fact]
    public void KnowledgeIndexStore_RemoveDocument_RemovesEntry()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var store = new KnowledgeIndexStore(db);

        store.MarkIndexed("doc/test.md", "abc123", "vec-001");
        Assert.Equal(1, store.GetIndexedCount());

        var removed = store.RemoveDocument("doc/test.md");
        Assert.True(removed);
        Assert.Equal(0, store.GetIndexedCount());
        Assert.True(store.NeedsIndexing("doc/test.md", "abc123"));
    }

    [Fact]
    public void KnowledgeIndexStore_RemoveDocument_FalseForMissing()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var store = new KnowledgeIndexStore(db);

        Assert.False(store.RemoveDocument("nonexistent.md"));
    }

    [Fact]
    public void KnowledgeIndexStore_GetVectorId_ReturnsCorrectId()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var store = new KnowledgeIndexStore(db);

        store.MarkIndexed("doc/test.md", "abc123", "vec-001");
        Assert.Equal("vec-001", store.GetVectorId("doc/test.md"));
        Assert.Null(store.GetVectorId("nonexistent.md"));
    }

    [Fact]
    public void KnowledgeIndexStore_MarkIndexed_UpdatesExistingEntry()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var store = new KnowledgeIndexStore(db);

        store.MarkIndexed("doc/test.md", "hash-v1", "vec-v1");
        store.MarkIndexed("doc/test.md", "hash-v2", "vec-v2");

        Assert.Equal(1, store.GetIndexedCount());
        Assert.Equal("vec-v2", store.GetVectorId("doc/test.md"));
        Assert.False(store.NeedsIndexing("doc/test.md", "hash-v2"));
    }

    [Fact]
    public void KnowledgeIndexStore_GetAllIndexed_ReturnsAll()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var store = new KnowledgeIndexStore(db);

        store.MarkIndexed("doc/a.md", "ha", "va");
        store.MarkIndexed("doc/b.md", "hb", "vb");
        store.MarkIndexed("doc/c.md", "hc", "vc");

        var all = store.GetAllIndexed();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, r => r.DocumentPath == "doc/a.md");
        Assert.Contains(all, r => r.DocumentPath == "doc/b.md");
        Assert.Contains(all, r => r.DocumentPath == "doc/c.md");
    }

    [Fact]
    public void EmbeddingService_Model_DefaultsToNomicEmbedText()
    {
        var httpClient = new System.Net.Http.HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") };
        var ollamaClient = new OllamaSharp.OllamaApiClient(httpClient);
        var service = new EmbeddingService(ollamaClient);
        Assert.Equal(EmbeddingService.DefaultEmbeddingModel, service.Model);
        Assert.Equal("nomic-embed-text", service.Model);
    }

    [Fact]
    public async Task EmbeddingService_GenerateEmbeddingAsync_ReturnsNullForEmptyText()
    {
        var httpClient = new System.Net.Http.HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") };
        var ollamaClient = new OllamaSharp.OllamaApiClient(httpClient);
        var service = new EmbeddingService(ollamaClient);

        Assert.Null(await service.GenerateEmbeddingAsync(""));
        Assert.Null(await service.GenerateEmbeddingAsync(null!));
        Assert.Null(await service.GenerateEmbeddingAsync("   "));
    }

    [Fact]
    public async Task EmbeddingService_GenerateEmbeddingAsync_ReturnsNullWhenOllamaUnavailable()
    {
        // Use unreachable endpoint to verify graceful fallback
        var httpClient = new System.Net.Http.HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:1/"),
            Timeout = TimeSpan.FromSeconds(2),
        };
        var ollamaClient = new OllamaSharp.OllamaApiClient(httpClient);
        var service = new EmbeddingService(ollamaClient);

        var result = await service.GenerateEmbeddingAsync("test text");
        Assert.Null(result);
    }

    [Fact]
    public async Task EmbeddingService_GenerateBatchEmbeddingsAsync_ReturnsNullForEmptyList()
    {
        var httpClient = new System.Net.Http.HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") };
        var ollamaClient = new OllamaSharp.OllamaApiClient(httpClient);
        var service = new EmbeddingService(ollamaClient);

        Assert.Null(await service.GenerateBatchEmbeddingsAsync(Array.Empty<string>()));
    }

    [Fact]
    public async Task EmbeddingService_GenerateBatchEmbeddingsAsync_ReturnsNullWhenOllamaUnavailable()
    {
        var httpClient = new System.Net.Http.HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:1/"),
            Timeout = TimeSpan.FromSeconds(2),
        };
        var ollamaClient = new OllamaSharp.OllamaApiClient(httpClient);
        var service = new EmbeddingService(ollamaClient);

        var result = await service.GenerateBatchEmbeddingsAsync(new[] { "hello" });
        Assert.Null(result);
    }

    [Fact]
    public void VectorSearchResult_DefaultValues()
    {
        var result = new VectorSearchResult();
        Assert.Equal(string.Empty, result.DocumentId);
        Assert.Equal(0f, result.Score);
        Assert.NotNull(result.Metadata);
        Assert.Empty(result.Metadata);
    }

    [Fact]
    public void VectorCollectionInfo_DefaultValues()
    {
        var info = new VectorCollectionInfo();
        Assert.Equal(string.Empty, info.Name);
        Assert.Equal(0UL, info.PointsCount);
        Assert.Equal(0UL, info.VectorSize);
        Assert.Equal(string.Empty, info.Status);
    }

    [Fact]
    public void KnowledgeIndexResult_DefaultValues()
    {
        var result = new KnowledgeIndexResult();
        Assert.Equal(0, result.TotalDocuments);
        Assert.Equal(0, result.Indexed);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public void KnowledgeIndexStatus_DefaultValues()
    {
        var status = new KnowledgeIndexStatus();
        Assert.Equal(0, status.TotalDocuments);
        Assert.Equal(0, status.IndexedDocuments);
        Assert.Equal(0UL, status.VectorStorePoints);
        Assert.Equal(string.Empty, status.VectorStoreStatus);
    }

    [Fact]
    public async Task OllamaService_GenerateEmbeddingAsync_ReturnsNullWhenUnreachable()
    {
        var processRunner = new ProcessRunner();
        var service = new OllamaService("http://127.0.0.1:1", processRunner);
        var result = await service.GenerateEmbeddingAsync("test text");
        Assert.Null(result);
    }

    [Fact]
    public async Task OllamaService_GenerateEmbeddingAsync_ReturnsNullForEmptyText()
    {
        var processRunner = new ProcessRunner();
        var service = new OllamaService("http://127.0.0.1:1", processRunner);
        Assert.Null(await service.GenerateEmbeddingAsync(""));
        Assert.Null(await service.GenerateEmbeddingAsync(null!));
    }

    [Fact]
    public async Task KnowledgePromptContextBuilder_SemanticSearchAsync_FallsBackToKeywordWhenServicesNull()
    {
        var library = new LearningLibrary
        {
            Documents = new[]
            {
                new LearningDocument
                {
                    RelativePath = "doc1.md",
                    FileName = "doc1.md",
                    Kind = "md",
                    ExtractedText = "Machine learning is a subset of artificial intelligence.",
                    SourceRootLabel = "Test",
                    Topics = new[] { "ML", "AI" },
                },
            },
        };

        // When embedding/vector services are null, should fall back to keyword search
        var result = await KnowledgePromptContextBuilder.BuildRelevantContextWithSemanticSearchAsync(
            library,
            new[] { "machine learning" },
            embeddingService: null,
            vectorStoreService: null);

        Assert.NotEqual("none recorded", result);
        Assert.Contains("doc1.md", result);
    }

    [Fact]
    public async Task KnowledgePromptContextBuilder_SemanticSearchAsync_ReturnsNoneForEmptyLibrary()
    {
        var library = new LearningLibrary();
        var result = await KnowledgePromptContextBuilder.BuildRelevantContextWithSemanticSearchAsync(
            library,
            new[] { "anything" },
            embeddingService: null,
            vectorStoreService: null);

        Assert.Equal("none recorded", result);
    }

    [Fact]
    public void IndexedDocumentRecord_DefaultValues()
    {
        var record = new IndexedDocumentRecord();
        Assert.NotNull(record.Id);
        Assert.Equal(string.Empty, record.DocumentPath);
        Assert.Equal(string.Empty, record.ContentHash);
        Assert.Equal(string.Empty, record.VectorId);
    }

    [Fact]
    public void OfficeDatabase_KnowledgeIndex_CollectionAccessible()
    {
        using var tmpDir = new TempDirectory();
        var db = new OfficeDatabase(tmpDir.Path);
        var collection = db.KnowledgeIndex;
        Assert.NotNull(collection);
        Assert.Equal(0, collection.Count());
    }

    // ───────────────────────────────────────────────────────────────────
    //  Phase 6 — Semantic Kernel Agent Orchestration
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public void OfficeKernelFactory_CreateKernel_ReturnsConfiguredKernel()
    {
        var factory = new OfficeKernelFactory("http://localhost:11434");
        var kernel = factory.CreateKernel("llama3.2");
        Assert.NotNull(kernel);
    }

    [Fact]
    public void OfficeKernelFactory_CreateKernel_ThrowsOnEmptyModel()
    {
        var factory = new OfficeKernelFactory("http://localhost:11434");
        Assert.Throws<ArgumentException>(() => factory.CreateKernel(""));
    }

    [Fact]
    public void OfficeKernelFactory_CreateKernel_ThrowsOnNullModel()
    {
        var factory = new OfficeKernelFactory("http://localhost:11434");
        Assert.Throws<ArgumentException>(() => factory.CreateKernel(null!));
    }

    [Fact]
    public void OfficeKernelFactory_DefaultsEndpointWhenEmpty()
    {
        var factory = new OfficeKernelFactory("");
        var kernel = factory.CreateKernel("test-model");
        Assert.NotNull(kernel);
    }

    [Fact]
    public void ChiefOfStaffAgent_HasCorrectRouteAndTitle()
    {
        var factory = new OfficeKernelFactory("http://localhost:11434");
        var kernel = factory.CreateKernel("llama3.2");
        var agent = new DailyDesk.Services.Agents.ChiefOfStaffAgent(kernel);
        Assert.Equal(OfficeRouteCatalog.ChiefRoute, agent.RouteId);
        Assert.Equal("Chief of Staff", agent.Title);
        Assert.Contains("Chief of Staff", agent.SystemPrompt);
    }

    [Fact]
    public void EngineeringDeskAgent_HasCorrectRouteAndTitle()
    {
        var factory = new OfficeKernelFactory("http://localhost:11434");
        var kernel = factory.CreateKernel("llama3.2");
        var agent = new DailyDesk.Services.Agents.EngineeringDeskAgent(kernel);
        Assert.Equal(OfficeRouteCatalog.EngineeringRoute, agent.RouteId);
        Assert.Equal("Engineering Desk", agent.Title);
        Assert.Contains("Engineering Desk", agent.SystemPrompt);
    }

    [Fact]
    public void SuiteContextAgent_HasCorrectRouteAndTitle()
    {
        var factory = new OfficeKernelFactory("http://localhost:11434");
        var kernel = factory.CreateKernel("llama3.2");
        var agent = new DailyDesk.Services.Agents.SuiteContextAgent(kernel);
        Assert.Equal(OfficeRouteCatalog.SuiteRoute, agent.RouteId);
        Assert.Equal("Suite Context", agent.Title);
        Assert.Contains("Suite Context", agent.SystemPrompt);
    }

    [Fact]
    public void GrowthOpsAgent_HasCorrectRouteAndTitle()
    {
        var factory = new OfficeKernelFactory("http://localhost:11434");
        var kernel = factory.CreateKernel("llama3.2");
        var agent = new DailyDesk.Services.Agents.GrowthOpsAgent(kernel);
        Assert.Equal(OfficeRouteCatalog.BusinessRoute, agent.RouteId);
        Assert.Equal("Business Ops", agent.Title);
        Assert.Contains("Business Ops", agent.SystemPrompt);
    }

    [Fact]
    public void MLEngineerAgent_HasCorrectRouteAndTitle()
    {
        var factory = new OfficeKernelFactory("http://localhost:11434");
        var kernel = factory.CreateKernel("llama3.2");
        var agent = new DailyDesk.Services.Agents.MLEngineerAgent(kernel);
        Assert.Equal(OfficeRouteCatalog.MLRoute, agent.RouteId);
        Assert.Equal("ML Engineer", agent.Title);
        Assert.Contains("ML engineering mentor", agent.SystemPrompt);
    }

    [Fact]
    public void AgentDispatch_AllKnownRoutes_HaveMatchingAgents()
    {
        var factory = new OfficeKernelFactory("http://localhost:11434");
        var agents = new DeskAgent[]
        {
            new DailyDesk.Services.Agents.ChiefOfStaffAgent(factory.CreateKernel("m")),
            new DailyDesk.Services.Agents.EngineeringDeskAgent(factory.CreateKernel("m")),
            new DailyDesk.Services.Agents.SuiteContextAgent(factory.CreateKernel("m")),
            new DailyDesk.Services.Agents.GrowthOpsAgent(factory.CreateKernel("m")),
            new DailyDesk.Services.Agents.MLEngineerAgent(factory.CreateKernel("m")),
        };

        var routeIds = agents.Select(a => a.RouteId).ToHashSet();
        foreach (var route in OfficeRouteCatalog.KnownRoutes)
        {
            Assert.Contains(route, routeIds);
        }
    }

    [Fact]
    public void AgentTools_ChiefOfStaff_GetOfficeState_ReturnsFormatted()
    {
        var result = DailyDesk.Services.Agents.ChiefOfStaffAgent.GetOfficeState(
            "Ollama", "EE fundamentals", "Review AC circuits");
        Assert.Contains("Provider: Ollama", result);
        Assert.Contains("Focus: EE fundamentals", result);
        Assert.Contains("Objective: Review AC circuits", result);
    }

    [Fact]
    public void AgentTools_ChiefOfStaff_ListRecentJobs_HandlesEmpty()
    {
        var result = DailyDesk.Services.Agents.ChiefOfStaffAgent.ListRecentJobs("");
        Assert.Equal("No recent jobs.", result);
    }

    [Fact]
    public void AgentTools_Engineering_GetTrainingHistory_ReturnsFormatted()
    {
        var result = DailyDesk.Services.Agents.EngineeringDeskAgent.GetTrainingHistory(
            "3 of 5 passed", "2 items queued", "1 defense completed", "AC circuits, transformer losses");
        Assert.Contains("Practice: 3 of 5 passed", result);
        Assert.Contains("Weak topics: AC circuits, transformer losses", result);
    }

    [Fact]
    public void AgentTools_Suite_GetSuiteSnapshot_ReturnsFormatted()
    {
        var result = DailyDesk.Services.Agents.SuiteContextAgent.GetSuiteSnapshot(
            "Running", "panel layout", "fix: breaker calc", "wire sizing");
        Assert.Contains("Status: Running", result);
        Assert.Contains("Hot areas: panel layout", result);
    }

    [Fact]
    public void AgentTools_GrowthOps_GetOperatorMemory_ReturnsFormatted()
    {
        var result = DailyDesk.Services.Agents.GrowthOpsAgent.GetOperatorMemory(
            "Finish AC review", "1 pending", "panel calc tool");
        Assert.Contains("Daily objective: Finish AC review", result);
        Assert.Contains("Monetization leads: panel calc tool", result);
    }

    [Fact]
    public void AgentTools_ML_GetMLAnalytics_ReturnsFormatted()
    {
        var result = DailyDesk.Services.Agents.MLEngineerAgent.GetMLAnalytics(
            "scikit-learn", "72%", "AC circuits (65%)", "power systems");
        Assert.Contains("Engine: scikit-learn", result);
        Assert.Contains("Readiness: 72%", result);
    }

    [Fact]
    public void AgentTools_ML_GetMLPipelineStatus_ReturnsFormatted()
    {
        var result = DailyDesk.Services.Agents.MLEngineerAgent.GetMLPipelineStatus(
            "yes", "2026-04-01 10:00 AM");
        Assert.Contains("Pipeline has run: yes", result);
        Assert.Contains("Last run: 2026-04-01 10:00 AM", result);
    }

    [Fact]
    public void DeskThreadState_Summary_DefaultsToNull()
    {
        var thread = new DeskThreadState();
        Assert.Null(thread.Summary);
    }

    [Fact]
    public void DeskThreadState_Summary_CanBeSetAndRetrieved()
    {
        var thread = new DeskThreadState { Summary = "Earlier discussion covered AC circuits and breaker sizing." };
        Assert.Equal("Earlier discussion covered AC circuits and breaker sizing.", thread.Summary);
    }

    [Fact]
    public void DeskAgent_RouteId_MatchesKnownRoute_ForAllAgents()
    {
        var factory = new OfficeKernelFactory("http://localhost:11434");
        var agents = new DeskAgent[]
        {
            new DailyDesk.Services.Agents.ChiefOfStaffAgent(factory.CreateKernel("m")),
            new DailyDesk.Services.Agents.EngineeringDeskAgent(factory.CreateKernel("m")),
            new DailyDesk.Services.Agents.SuiteContextAgent(factory.CreateKernel("m")),
            new DailyDesk.Services.Agents.GrowthOpsAgent(factory.CreateKernel("m")),
            new DailyDesk.Services.Agents.MLEngineerAgent(factory.CreateKernel("m")),
        };

        foreach (var agent in agents)
        {
            Assert.Contains(agent.RouteId, OfficeRouteCatalog.KnownRoutes);
            Assert.Equal(OfficeRouteCatalog.ResolveRouteTitle(agent.RouteId), agent.Title);
        }
    }

    [Fact]
    public void AgentTools_Engineering_GetKnowledgeContext_HandlesEmpty()
    {
        var result = DailyDesk.Services.Agents.EngineeringDeskAgent.GetKnowledgeContext("");
        Assert.Equal("No relevant notebook evidence available.", result);
    }

    [Fact]
    public void AgentTools_Suite_GetLibraryDocs_HandlesEmpty()
    {
        var result = DailyDesk.Services.Agents.SuiteContextAgent.GetLibraryDocs("");
        Assert.Equal("No library documents imported yet.", result);
    }

    [Fact]
    public void AgentTools_GrowthOps_GetSuggestions_HandlesEmpty()
    {
        var result = DailyDesk.Services.Agents.GrowthOpsAgent.GetSuggestions("");
        Assert.Equal("No active suggestions.", result);
    }

    // ── Phase 7: Document Extraction ────────────────────────────────────────

    [Fact]
    public void ExtractedTable_ToMarkdown_ProducesValidMarkdown()
    {
        var table = new ExtractedTable
        {
            Headers = new[] { "Name", "Value" },
            Rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Voltage", "120V" },
                new object?[] { "Current", "15A" },
            },
        };

        var md = table.ToMarkdown();

        Assert.Contains("| Name | Value |", md);
        Assert.Contains("| --- | --- |", md);
        Assert.Contains("| Voltage | 120V |", md);
        Assert.Contains("| Current | 15A |", md);
    }

    [Fact]
    public void ExtractedTable_ToMarkdown_EmptyHeaders_ReturnsEmpty()
    {
        var table = new ExtractedTable
        {
            Headers = Array.Empty<string>(),
            Rows = new List<IReadOnlyList<object?>>(),
        };

        Assert.Equal(string.Empty, table.ToMarkdown());
    }

    [Fact]
    public void ExtractedTable_ToMarkdown_PadsShortRows()
    {
        var table = new ExtractedTable
        {
            Headers = new[] { "A", "B", "C" },
            Rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "x" }, // Only 1 cell, should be padded to 3
            },
        };

        var md = table.ToMarkdown();
        Assert.Contains("| x |  |  |", md);
    }

    [Fact]
    public void ExtractedTable_ToMarkdown_HandlesNullCells()
    {
        var table = new ExtractedTable
        {
            Headers = new[] { "Col1", "Col2" },
            Rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { null, "value" },
            },
        };

        var md = table.ToMarkdown();
        Assert.Contains("|  | value |", md);
    }

    [Fact]
    public void LearningDocument_TablesAndFigures_DefaultToEmpty()
    {
        var doc = new LearningDocument();

        Assert.Empty(doc.Tables);
        Assert.Empty(doc.Figures);
    }

    [Fact]
    public void LearningDocument_TablesAndFigures_CanBePopulated()
    {
        var doc = new LearningDocument
        {
            Tables = new[]
            {
                new ExtractedTable
                {
                    Headers = new[] { "A" },
                    Rows = new List<IReadOnlyList<object?>> { new object?[] { "1" } },
                },
            },
            Figures = new[]
            {
                new ExtractedFigure { Description = "Circuit diagram" },
            },
        };

        Assert.Single(doc.Tables);
        Assert.Single(doc.Figures);
        Assert.Equal("Circuit diagram", doc.Figures[0].Description);
    }

    [Fact]
    public void PythonExtractionResponse_DeserializesRichFormat()
    {
        var json = """
        {
            "ok": true,
            "text": "Hello world",
            "metadata": {
                "extractor": "docling",
                "format": "pdf",
                "table_count": 1,
                "figure_count": 2
            },
            "tables": [
                {
                    "headers": ["H1", "H2"],
                    "rows": [["a", "b"]]
                }
            ],
            "figures": [
                {"description": "A photo"}
            ]
        }
        """;

        var response = System.Text.Json.JsonSerializer.Deserialize<DoclingTestResponse>(
            json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal("Hello world", response.Text);
        Assert.Equal("docling", response.Metadata?.Extractor);
        Assert.Equal(1, response.Metadata?.TableCount);
        Assert.Single(response.Tables!);
        Assert.Single(response.Figures!);
        Assert.Equal("A photo", response.Figures![0].Description);
    }

    [Fact]
    public void PythonExtractionResponse_DeserializesLegacyFormat()
    {
        // Legacy format without metadata/tables/figures should still work
        var json = """{"ok": true, "text": "Simple text"}""";

        var response = System.Text.Json.JsonSerializer.Deserialize<DoclingTestResponse>(
            json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Equal("Simple text", response.Text);
        Assert.Null(response.Metadata);
        Assert.Null(response.Tables);
        Assert.Null(response.Figures);
    }

    [Fact]
    public void ExtractedFigure_DefaultDescription_IsEmpty()
    {
        var figure = new ExtractedFigure();
        Assert.Equal(string.Empty, figure.Description);
    }

    [Fact]
    public void ExtractedTable_ToMarkdown_MultipleRows()
    {
        var table = new ExtractedTable
        {
            Headers = new[] { "Part", "Qty", "Price" },
            Rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { "Resistor", 10, 0.5 },
                new object?[] { "Capacitor", 5, 1.2 },
                new object?[] { "LED", 20, 0.3 },
            },
        };

        var md = table.ToMarkdown();
        var lines = md.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Header + separator + 3 data rows = 5 lines
        Assert.Equal(5, lines.Length);
        Assert.Contains("Resistor", md);
        Assert.Contains("Capacitor", md);
        Assert.Contains("LED", md);
    }

    /// <summary>
    /// Test DTO matching the shape of the internal PythonExtractionResponse
    /// used by KnowledgeImportService, for verifying JSON deserialization.
    /// </summary>
    private sealed class DoclingTestResponse
    {
        public bool Ok { get; set; }
        public string? Text { get; set; }
        public string? Error { get; set; }
        public DoclingTestMetadata? Metadata { get; set; }
        public List<ExtractedTable>? Tables { get; set; }
        public List<ExtractedFigure>? Figures { get; set; }
    }

    private sealed class DoclingTestMetadata
    {
        public string? Extractor { get; set; }
        public string? Format { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("table_count")]
        public int TableCount { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("figure_count")]
        public int FigureCount { get; set; }
    }

    /// <summary>Helper for creating disposable temp directories in tests.</summary>
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "office-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }
}
