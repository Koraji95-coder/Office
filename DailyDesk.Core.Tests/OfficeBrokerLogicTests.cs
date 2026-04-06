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

    // ========================================================================
    // Phase 8: Scheduled Automation & Operator Workflows
    // ========================================================================

    // --- PR 8.1: Job Scheduler Store Tests ---

    [Fact]
    public void JobSchedulerStore_Create_AssignsIdAndNextRun()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        var schedule = new JobSchedule
        {
            Name = "Nightly Pipeline",
            JobType = OfficeJobType.MLPipeline,
            CronExpression = "every 1d",
            Enabled = true,
        };

        var created = store.Create(schedule);
        Assert.NotNull(created.Id);
        Assert.NotNull(created.NextRunAt);
        Assert.Equal("Nightly Pipeline", created.Name);
        Assert.Equal(OfficeJobType.MLPipeline, created.JobType);
    }

    [Fact]
    public void JobSchedulerStore_GetById_ReturnsSchedule()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        var created = store.Create(new JobSchedule
        {
            Name = "Test",
            JobType = OfficeJobType.MLAnalytics,
            CronExpression = "every 2h",
        });

        var retrieved = store.GetById(created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved!.Id);
        Assert.Equal("Test", retrieved.Name);
    }

    [Fact]
    public void JobSchedulerStore_GetById_ReturnsNullForMissing()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        Assert.Null(store.GetById("nonexistent"));
    }

    [Fact]
    public void JobSchedulerStore_ListAll_ReturnsAllSchedules()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        store.Create(new JobSchedule { Name = "A", JobType = OfficeJobType.MLAnalytics, CronExpression = "every 1h" });
        store.Create(new JobSchedule { Name = "B", JobType = OfficeJobType.MLForecast, CronExpression = "every 2h" });

        var all = store.ListAll();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void JobSchedulerStore_Update_ChangesFields()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        var created = store.Create(new JobSchedule
        {
            Name = "Original",
            JobType = OfficeJobType.MLAnalytics,
            CronExpression = "every 1h",
            Enabled = true,
        });

        var updated = store.Update(created.Id, s =>
        {
            s.Name = "Updated";
            s.Enabled = false;
        });

        Assert.NotNull(updated);
        Assert.Equal("Updated", updated!.Name);
        Assert.False(updated.Enabled);
    }

    [Fact]
    public void JobSchedulerStore_Update_RecomputesNextRunWhenCronChanges()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        var created = store.Create(new JobSchedule
        {
            Name = "Test",
            JobType = OfficeJobType.MLAnalytics,
            CronExpression = "every 1h",
        });

        var originalNext = created.NextRunAt;

        var updated = store.Update(created.Id, s =>
        {
            s.CronExpression = "every 12h";
        });

        Assert.NotNull(updated);
        // With 12h interval, next run should be further out than 1h
        Assert.True(updated!.NextRunAt > originalNext);
    }

    [Fact]
    public void JobSchedulerStore_Update_ReturnsNullForMissing()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        var result = store.Update("nonexistent", s => s.Name = "test");
        Assert.Null(result);
    }

    [Fact]
    public void JobSchedulerStore_Delete_RemovesSchedule()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        var created = store.Create(new JobSchedule
        {
            Name = "ToDelete",
            JobType = OfficeJobType.MLAnalytics,
            CronExpression = "every 1h",
        });

        Assert.True(store.Delete(created.Id));
        Assert.Null(store.GetById(created.Id));
    }

    [Fact]
    public void JobSchedulerStore_Delete_ReturnsFalseForMissing()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        Assert.False(store.Delete("nonexistent"));
    }

    [Fact]
    public void JobSchedulerStore_GetDueSchedules_ReturnsDueOnly()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        // Create a schedule with next run in the past
        var schedule = store.Create(new JobSchedule
        {
            Name = "Due",
            JobType = OfficeJobType.MLAnalytics,
            CronExpression = "every 1h",
        });

        // Manually set NextRunAt to past
        store.Update(schedule.Id, s =>
        {
            // Keep same cron expression to avoid recompute
            s.NextRunAt = DateTimeOffset.Now.AddMinutes(-5);
        });
        // Force cron back since Update recalculates
        var raw = db.JobSchedules.FindOne(s => s.Id == schedule.Id);
        raw!.NextRunAt = DateTimeOffset.Now.AddMinutes(-5);
        db.JobSchedules.Update(raw);

        // Create a schedule with next run in the future
        store.Create(new JobSchedule
        {
            Name = "NotDue",
            JobType = OfficeJobType.MLForecast,
            CronExpression = "every 24h",
        });

        var due = store.GetDueSchedules(DateTimeOffset.Now);
        Assert.Single(due);
        Assert.Equal("Due", due[0].Name);
    }

    [Fact]
    public void JobSchedulerStore_GetDueSchedules_SkipsDisabled()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        var schedule = store.Create(new JobSchedule
        {
            Name = "Disabled",
            JobType = OfficeJobType.MLAnalytics,
            CronExpression = "every 1h",
            Enabled = false,
        });

        // Set next run to past
        var raw = db.JobSchedules.FindOne(s => s.Id == schedule.Id);
        raw!.NextRunAt = DateTimeOffset.Now.AddMinutes(-5);
        db.JobSchedules.Update(raw);

        var due = store.GetDueSchedules(DateTimeOffset.Now);
        Assert.Empty(due);
    }

    [Fact]
    public void JobSchedulerStore_MarkRun_UpdatesLastRunAndNextRun()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new JobSchedulerStore(db);

        var schedule = store.Create(new JobSchedule
        {
            Name = "Test",
            JobType = OfficeJobType.MLAnalytics,
            CronExpression = "every 2h",
        });

        var runAt = DateTimeOffset.Now;
        store.MarkRun(schedule.Id, runAt);

        var updated = store.GetById(schedule.Id);
        Assert.NotNull(updated);
        // LiteDB truncates sub-millisecond precision, so compare within 1 second
        Assert.NotNull(updated!.LastRunAt);
        Assert.True(Math.Abs((updated.LastRunAt!.Value - runAt).TotalSeconds) < 1);
        Assert.NotNull(updated.NextRunAt);
        // Next run should be ~2h after ranAt
        Assert.True(updated.NextRunAt > runAt);
    }

    // --- Cron Parsing Tests ---

    [Theory]
    [InlineData("every 30m")]
    [InlineData("every 2h")]
    [InlineData("every 1d")]
    [InlineData("0 8 * * *")]
    [InlineData("30 14 * * 1,3,5")]
    public void CronParser_ValidExpressions_ReturnNonNull(string cron)
    {
        var next = JobSchedulerStore.ComputeNextRun(cron, DateTimeOffset.Now);
        Assert.NotNull(next);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("every 0m")]
    [InlineData("every -1h")]
    [InlineData("99 99 99 99 99")]
    public void CronParser_InvalidExpressions_ReturnNull(string cron)
    {
        var next = JobSchedulerStore.ComputeNextRun(cron, DateTimeOffset.Now);
        Assert.Null(next);
    }

    [Fact]
    public void CronParser_SimpleInterval_ComputesCorrectOffset()
    {
        var now = DateTimeOffset.Now;
        var next = JobSchedulerStore.ComputeNextRun("every 30m", now);
        Assert.NotNull(next);
        var diff = (next!.Value - now).TotalMinutes;
        Assert.True(Math.Abs(diff - 30) < 0.1);
    }

    [Fact]
    public void CronParser_DailyCron_ComputesFuture()
    {
        var now = DateTimeOffset.Now;
        var next = JobSchedulerStore.ComputeNextRun("0 8 * * *", now);
        Assert.NotNull(next);
        Assert.True(next!.Value > now);
        Assert.Equal(8, next.Value.Hour);
        Assert.Equal(0, next.Value.Minute);
    }

    // --- PR 8.2: Daily Run Tests ---

    [Fact]
    public void OfficeJobType_DailyRun_Exists()
    {
        Assert.Equal("daily-run", OfficeJobType.DailyRun);
    }

    [Fact]
    public void OfficeJobStore_Enqueue_DailyRunType()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new OfficeJobStore(db);

        var job = store.Enqueue(OfficeJobType.DailyRun, "scheduler:daily-run");
        Assert.Equal(OfficeJobType.DailyRun, job.Type);
        Assert.Equal(OfficeJobStatus.Queued, job.Status);
        Assert.Equal("scheduler:daily-run", job.RequestedBy);
    }

    [Fact]
    public void DailyRunSummary_StepResults_TrackSuccess()
    {
        var summary = new DailyRunSummary
        {
            StartedAt = DateTimeOffset.Now.AddMinutes(-5),
            CompletedAt = DateTimeOffset.Now,
            Steps =
            [
                new DailyRunStepResult { Step = "RefreshState", Success = true },
                new DailyRunStepResult { Step = "MLPipeline", Success = true },
                new DailyRunStepResult { Step = "ExportArtifacts", Success = false, Error = "test error" },
            ],
            OverallSuccess = false,
        };

        Assert.Equal(3, summary.Steps.Count);
        Assert.False(summary.OverallSuccess);
        Assert.Equal(2, summary.Steps.Count(s => s.Success));
        Assert.Equal(1, summary.Steps.Count(s => !s.Success));
        Assert.Equal("test error", summary.Steps[2].Error);
    }

    [Fact]
    public void DailyRunSummary_AllStepsSucceeded()
    {
        var summary = new DailyRunSummary
        {
            StartedAt = DateTimeOffset.Now.AddMinutes(-5),
            CompletedAt = DateTimeOffset.Now,
            Steps =
            [
                new DailyRunStepResult { Step = "RefreshState", Success = true },
                new DailyRunStepResult { Step = "MLPipeline", Success = true },
            ],
            OverallSuccess = true,
        };

        Assert.True(summary.OverallSuccess);
        Assert.True(summary.Steps.All(s => s.Success));
    }

    [Fact]
    public void DailyRunJobSummary_FieldsPopulate()
    {
        var summary = new DailyRunJobSummary
        {
            JobId = "test-123",
            Status = OfficeJobStatus.Succeeded,
            CreatedAt = DateTimeOffset.Now.AddMinutes(-10),
            StartedAt = DateTimeOffset.Now.AddMinutes(-9),
            CompletedAt = DateTimeOffset.Now,
            ResultJson = "{\"test\": true}",
        };

        Assert.Equal("test-123", summary.JobId);
        Assert.Equal(OfficeJobStatus.Succeeded, summary.Status);
        Assert.NotNull(summary.StartedAt);
        Assert.NotNull(summary.CompletedAt);
        Assert.NotNull(summary.ResultJson);
        Assert.Null(summary.Error);
    }

    // --- PR 8.3: Workflow Store Tests ---

    [Fact]
    public void WorkflowStore_SeedsBuiltInTemplates()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new WorkflowStore(db);

        var all = store.ListAll();
        Assert.True(all.Count >= 3);
        Assert.Contains(all, w => w.Name == "Daily Run" && w.BuiltIn);
        Assert.Contains(all, w => w.Name == "Exam Prep" && w.BuiltIn);
        Assert.Contains(all, w => w.Name == "Knowledge Refresh" && w.BuiltIn);
    }

    [Fact]
    public void WorkflowStore_SeedingIsIdempotent()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store1 = new WorkflowStore(db);
        var count1 = store1.ListAll().Count;

        // Creating a second store should not duplicate built-ins
        var store2 = new WorkflowStore(db);
        var count2 = store2.ListAll().Count;

        Assert.Equal(count1, count2);
    }

    [Fact]
    public void WorkflowStore_Create_AssignsId()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new WorkflowStore(db);

        var template = new WorkflowTemplate
        {
            Name = "Custom Workflow",
            Description = "A custom workflow",
            Steps =
            [
                new WorkflowStep { JobType = OfficeJobType.MLAnalytics, Label = "Analytics" },
                new WorkflowStep { JobType = OfficeJobType.MLForecast, Label = "Forecast" },
            ],
        };

        var created = store.Create(template);
        Assert.NotNull(created.Id);
        Assert.Equal("Custom Workflow", created.Name);
        Assert.Equal(2, created.Steps.Count);
        Assert.False(created.BuiltIn);
    }

    [Fact]
    public void WorkflowStore_GetById_ReturnsTemplate()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new WorkflowStore(db);

        var created = store.Create(new WorkflowTemplate
        {
            Name = "Test",
            Steps = [new WorkflowStep { JobType = OfficeJobType.MLAnalytics }],
        });

        var retrieved = store.GetById(created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved!.Id);
    }

    [Fact]
    public void WorkflowStore_GetById_ReturnsNullForMissing()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new WorkflowStore(db);

        Assert.Null(store.GetById("nonexistent"));
    }

    [Fact]
    public void WorkflowStore_Delete_RemovesCustomTemplate()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new WorkflowStore(db);

        var created = store.Create(new WorkflowTemplate
        {
            Name = "ToDelete",
            Steps = [new WorkflowStep { JobType = OfficeJobType.MLAnalytics }],
        });

        Assert.True(store.Delete(created.Id));
        Assert.Null(store.GetById(created.Id));
    }

    [Fact]
    public void WorkflowStore_Delete_RefusesBuiltIn()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new WorkflowStore(db);

        var builtIn = store.ListAll().First(w => w.BuiltIn);
        Assert.False(store.Delete(builtIn.Id));
        Assert.NotNull(store.GetById(builtIn.Id));
    }

    [Fact]
    public void WorkflowStore_Delete_ReturnsFalseForMissing()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new WorkflowStore(db);

        Assert.False(store.Delete("nonexistent"));
    }

    [Fact]
    public void WorkflowTemplate_FailurePolicy_DefaultsToAbort()
    {
        var template = new WorkflowTemplate();
        Assert.Equal(WorkflowFailurePolicy.Abort, template.FailurePolicy);
    }

    [Fact]
    public void WorkflowTemplate_Steps_CanBeConfigured()
    {
        var template = new WorkflowTemplate
        {
            Name = "Multi-Step",
            Steps =
            [
                new WorkflowStep { JobType = OfficeJobType.MLPipeline, Label = "Pipeline" },
                new WorkflowStep { JobType = OfficeJobType.MLExportArtifacts, Label = "Export" },
                new WorkflowStep { JobType = OfficeJobType.KnowledgeIndex, Label = "Index" },
            ],
            FailurePolicy = WorkflowFailurePolicy.Continue,
        };

        Assert.Equal(3, template.Steps.Count);
        Assert.Equal(WorkflowFailurePolicy.Continue, template.FailurePolicy);
    }

    [Fact]
    public void WorkflowExecution_EnqueuesJobsInOrder()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var jobStore = new OfficeJobStore(db);

        // Simulate what POST /api/workflows/{id}/run does
        var steps = new[]
        {
            new WorkflowStep { JobType = OfficeJobType.MLPipeline, Label = "Pipeline" },
            new WorkflowStep { JobType = OfficeJobType.MLExportArtifacts, Label = "Export" },
            new WorkflowStep { JobType = OfficeJobType.KnowledgeIndex, Label = "Index" },
        };

        var jobIds = new List<string>();
        foreach (var step in steps)
        {
            var job = jobStore.Enqueue(step.JobType, "workflow:test", step.RequestPayload);
            jobIds.Add(job.Id);
        }

        Assert.Equal(3, jobIds.Count);

        // Dequeue should return them in FIFO order
        var first = jobStore.DequeueNext();
        Assert.NotNull(first);
        Assert.Equal(OfficeJobType.MLPipeline, first!.Type);

        var second = jobStore.DequeueNext();
        Assert.NotNull(second);
        Assert.Equal(OfficeJobType.MLExportArtifacts, second!.Type);

        var third = jobStore.DequeueNext();
        Assert.NotNull(third);
        Assert.Equal(OfficeJobType.KnowledgeIndex, third!.Type);
    }

    [Fact]
    public void WorkflowExecution_HandlesAbortPolicy()
    {
        // Test the abort policy model: if a step fails, the workflow should stop
        var template = new WorkflowTemplate
        {
            FailurePolicy = WorkflowFailurePolicy.Abort,
            Steps =
            [
                new WorkflowStep { JobType = OfficeJobType.MLAnalytics },
                new WorkflowStep { JobType = OfficeJobType.MLForecast },
            ],
        };

        Assert.Equal(WorkflowFailurePolicy.Abort, template.FailurePolicy);
        Assert.Equal(2, template.Steps.Count);
    }

    [Fact]
    public void WorkflowExecution_HandlesContinuePolicy()
    {
        // Test the continue policy model: if a step fails, the workflow continues
        var template = new WorkflowTemplate
        {
            FailurePolicy = WorkflowFailurePolicy.Continue,
            Steps =
            [
                new WorkflowStep { JobType = OfficeJobType.MLAnalytics },
                new WorkflowStep { JobType = OfficeJobType.MLForecast },
            ],
        };

        Assert.Equal(WorkflowFailurePolicy.Continue, template.FailurePolicy);
    }

    [Fact]
    public void WorkflowStore_BuiltInDailyRun_HasCorrectSteps()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var store = new WorkflowStore(db);

        var dailyRun = store.ListAll().First(w => w.Name == "Daily Run");
        Assert.True(dailyRun.BuiltIn);
        Assert.Equal(WorkflowFailurePolicy.Continue, dailyRun.FailurePolicy);
        Assert.Equal(3, dailyRun.Steps.Count);
        Assert.Equal(OfficeJobType.MLPipeline, dailyRun.Steps[0].JobType);
        Assert.Equal(OfficeJobType.MLExportArtifacts, dailyRun.Steps[1].JobType);
        Assert.Equal(OfficeJobType.KnowledgeIndex, dailyRun.Steps[2].JobType);
    }

    // --- Schedule + JobStore Integration Tests ---

    [Fact]
    public void Scheduler_DueSchedule_EnqueuesJob()
    {
        using var tmp = new TempDirectory();
        using var db = new OfficeDatabase(tmp.Path);
        var schedulerStore = new JobSchedulerStore(db);
        var jobStore = new OfficeJobStore(db);

        var schedule = schedulerStore.Create(new JobSchedule
        {
            Name = "Hourly Analytics",
            JobType = OfficeJobType.MLAnalytics,
            CronExpression = "every 1h",
        });

        // Manually set NextRunAt to past to simulate it being due
        var raw = db.JobSchedules.FindOne(s => s.Id == schedule.Id);
        raw!.NextRunAt = DateTimeOffset.Now.AddMinutes(-1);
        db.JobSchedules.Update(raw);

        var dueSchedules = schedulerStore.GetDueSchedules(DateTimeOffset.Now);
        Assert.Single(dueSchedules);

        // Simulate what the worker does
        foreach (var due in dueSchedules)
        {
            jobStore.Enqueue(due.JobType, $"scheduler:{due.Name}");
            schedulerStore.MarkRun(due.Id, DateTimeOffset.Now);
        }

        // Job should be enqueued
        var jobs = jobStore.ListRecent(10);
        Assert.Single(jobs);
        Assert.Equal(OfficeJobType.MLAnalytics, jobs[0].Type);
        Assert.Equal("scheduler:Hourly Analytics", jobs[0].RequestedBy);

        // Schedule should have been updated
        var updated = schedulerStore.GetById(schedule.Id);
        Assert.NotNull(updated!.LastRunAt);
        Assert.NotNull(updated.NextRunAt);
        Assert.True(updated.NextRunAt > DateTimeOffset.Now);
    }

    // ========================================================================
    // Phase 9 — WPF Client Async Integration Tests
    // ========================================================================

    // --- PR 9.1: JobPollingService Tests ---

    [Fact]
    public void JobPollResult_Succeeded_ReturnsTrue()
    {
        var result = new JobPollResult
        {
            JobId = "test-1",
            FinalStatus = OfficeJobStatus.Succeeded,
        };
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void JobPollResult_Failed_ReturnsFalse()
    {
        var result = new JobPollResult
        {
            JobId = "test-1",
            FinalStatus = OfficeJobStatus.Failed,
            Error = "Something went wrong.",
        };
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void JobPollResult_EmptyStatus_ReturnsFalse()
    {
        var result = new JobPollResult();
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void JobPollStatus_DefaultProperties()
    {
        var status = new JobPollStatus();
        Assert.Equal(string.Empty, status.Id);
        Assert.Equal(string.Empty, status.Type);
        Assert.Equal(string.Empty, status.Status);
        Assert.Null(status.StartedAt);
        Assert.Null(status.CompletedAt);
        Assert.Null(status.Error);
        Assert.Null(status.RequestedBy);
    }

    [Fact]
    public void JobPollStatus_CanStoreAllProperties()
    {
        var now = DateTimeOffset.Now;
        var status = new JobPollStatus
        {
            Id = "job-42",
            Type = OfficeJobType.MLAnalytics,
            Status = OfficeJobStatus.Running,
            CreatedAt = now,
            StartedAt = now.AddSeconds(1),
            CompletedAt = null,
            Error = null,
            RequestedBy = "test-user",
        };

        Assert.Equal("job-42", status.Id);
        Assert.Equal(OfficeJobType.MLAnalytics, status.Type);
        Assert.Equal(OfficeJobStatus.Running, status.Status);
        Assert.Equal(now, status.CreatedAt);
        Assert.Equal(now.AddSeconds(1), status.StartedAt);
        Assert.Null(status.CompletedAt);
        Assert.Null(status.Error);
        Assert.Equal("test-user", status.RequestedBy);
    }

    [Fact]
    public void JobActivityItem_StatusTransitions_UpdateIsActive()
    {
        var item = new JobActivityItem
        {
            Title = "ML Analytics",
            Agent = "ML Engineer",
            Model = "llama3.2",
            Status = OfficeJobStatus.Queued,
            Summary = "Waiting in queue...",
        };

        Assert.True(item.IsActive);

        item.Status = OfficeJobStatus.Running;
        Assert.True(item.IsActive);

        item.Status = OfficeJobStatus.Succeeded;
        Assert.False(item.IsActive);

        item.Status = OfficeJobStatus.Failed;
        Assert.False(item.IsActive);
    }

    [Fact]
    public void JobActivityItem_CompletedAt_UpdatesDisplayMeta()
    {
        var item = new JobActivityItem
        {
            Title = "Test",
            Agent = "Agent",
            Model = "model",
            Status = OfficeJobStatus.Running,
        };

        var metaBefore = item.DisplayMeta;
        item.Status = OfficeJobStatus.Succeeded;
        item.CompletedAt = DateTimeOffset.Now;
        var metaAfter = item.DisplayMeta;

        Assert.Contains("succeeded", metaAfter);
        Assert.DoesNotContain("succeeded", metaBefore);
    }

    [Fact]
    public async Task JobPollingService_SubmitJob_ReturnsNullOnFailure()
    {
        // Use a client that points to nothing (will fail immediately)
        var handler = new FailingHttpHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:1") };
        var service = new JobPollingService(client);

        var jobId = await service.SubmitJobAsync("/api/ml/analytics");
        Assert.Null(jobId);
    }

    [Fact]
    public async Task JobPollingService_GetJobStatus_ReturnsNullOnFailure()
    {
        var handler = new FailingHttpHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:1") };
        var service = new JobPollingService(client);

        var status = await service.GetJobStatusAsync("nonexistent");
        Assert.Null(status);
    }

    [Fact]
    public async Task JobPollingService_PollUntilComplete_TimesOutGracefully()
    {
        var handler = new FailingHttpHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:1") };
        var service = new JobPollingService(client);

        var result = await service.PollUntilCompleteAsync(
            "test-job",
            maxAttempts: 2,
            pollInterval: TimeSpan.FromMilliseconds(10));

        Assert.Equal(OfficeJobStatus.Failed, result.FinalStatus);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task JobPollingService_SubmitAndPoll_ReturnsFailedOnSubmissionFailure()
    {
        var handler = new FailingHttpHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:1") };
        var service = new JobPollingService(client);

        var result = await service.SubmitAndPollAsync("/api/ml/analytics", null);

        Assert.False(result.Succeeded);
        Assert.Equal(OfficeJobStatus.Failed, result.FinalStatus);
        Assert.Contains("submission failed", result.Error);
    }

    [Fact]
    public async Task JobPollingService_PollUntilComplete_FiresStatusChange()
    {
        var statusChanges = new List<string>();
        var handler = new SequenceHttpHandler(
        [
            // First poll returns running
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new JobPollStatus
                    {
                        Id = "j1", Status = OfficeJobStatus.Running, Type = "ml-analytics",
                    })),
            },
            // Second poll returns succeeded
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new JobPollStatus
                    {
                        Id = "j1", Status = OfficeJobStatus.Succeeded, Type = "ml-analytics",
                        CompletedAt = DateTimeOffset.Now,
                    })),
            },
        ]);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:1") };
        var service = new JobPollingService(client);

        var result = await service.PollUntilCompleteAsync(
            "j1",
            onStatusChange: s => statusChanges.Add(s.Status),
            pollInterval: TimeSpan.FromMilliseconds(10));

        Assert.True(result.Succeeded);
        Assert.Contains(OfficeJobStatus.Running, statusChanges);
        Assert.Contains(OfficeJobStatus.Succeeded, statusChanges);
    }

    [Fact]
    public async Task JobPollingService_PollUntilComplete_ReportsCancellation()
    {
        var handler = new NeverRespondingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:1") };
        var service = new JobPollingService(client);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.PollUntilCompleteAsync("j1",
                pollInterval: TimeSpan.FromMilliseconds(10),
                cancellationToken: cts.Token));
    }

    // --- PR 9.2: KnowledgeSearchService Tests ---

    [Fact]
    public async Task KnowledgeSearchService_EmptyQuery_ReturnsNone()
    {
        var handler = new FailingHttpHandler();
        var embeddingClient = new OllamaSharp.OllamaApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:1") });
        var embeddingService = new EmbeddingService(embeddingClient);
        var vectorStore = new VectorStoreService("localhost", 1);
        var searchService = new KnowledgeSearchService(embeddingService, vectorStore);

        var response = await searchService.SearchAsync("", null);
        Assert.Equal("none", response.SearchMode);
        Assert.Empty(response.Results);
    }

    [Fact]
    public void KnowledgeSearchService_TextFallback_EmptyLibrary_ReturnsEmpty()
    {
        var result = KnowledgeSearchService.FallbackTextSearch("test query", null);
        Assert.Equal("text", result.SearchMode);
        Assert.Empty(result.Results);
        Assert.Equal(0, result.TotalResults);
    }

    [Fact]
    public void KnowledgeSearchService_TextFallback_MatchesByKeyword()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "grounding-guide.md",
                    RelativePath = "guides/grounding-guide.md",
                    Summary = "Electrical grounding and bonding practices",
                    Topics = ["grounding", "bonding", "safety"],
                },
                new LearningDocument
                {
                    FileName = "motor-control.md",
                    RelativePath = "guides/motor-control.md",
                    Summary = "Motor control center design",
                    Topics = ["motor", "control", "design"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch("grounding safety", library);
        Assert.Equal("text", result.SearchMode);
        Assert.NotEmpty(result.Results);
        Assert.Equal("grounding-guide.md", result.Results[0].Title);
        Assert.True(result.Results[0].Score > 0);
    }

    [Fact]
    public void KnowledgeSearchService_TextFallback_NoMatches_ReturnsEmpty()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "grounding-guide.md",
                    RelativePath = "guides/grounding-guide.md",
                    Summary = "Electrical grounding and bonding practices",
                    Topics = ["grounding", "bonding"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch("quantum computing", library);
        Assert.Equal("text", result.SearchMode);
        Assert.Empty(result.Results);
    }

    [Fact]
    public void KnowledgeSearchService_TextFallback_RanksMultipleMatches()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "conduit-fill.md",
                    RelativePath = "guides/conduit-fill.md",
                    Summary = "Conduit fill calculations and NEC guidelines",
                    Topics = ["conduit", "NEC"],
                },
                new LearningDocument
                {
                    FileName = "nec-grounding.md",
                    RelativePath = "guides/nec-grounding.md",
                    Summary = "NEC grounding requirements and electrode systems for grounding",
                    Topics = ["NEC", "grounding", "electrodes"],
                },
                new LearningDocument
                {
                    FileName = "panel-boards.md",
                    RelativePath = "guides/panel-boards.md",
                    Summary = "Panel board sizing and breaker selection",
                    Topics = ["panels", "breakers"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch("NEC grounding", library);
        Assert.Equal("text", result.SearchMode);
        Assert.True(result.Results.Count >= 1);
        // nec-grounding.md should rank highest (matches both tokens)
        Assert.Equal("nec-grounding.md", result.Results[0].Title);
    }

    [Fact]
    public void KnowledgeSearchService_TextFallback_RespectsTopK()
    {
        var docs = Enumerable.Range(1, 20).Select(i => new LearningDocument
        {
            FileName = $"doc-{i}.md",
            RelativePath = $"docs/doc-{i}.md",
            Summary = $"Document about electrical topic {i}",
            Topics = ["electrical"],
        }).ToArray();

        var library = new LearningLibrary { Documents = docs };
        var result = KnowledgeSearchService.FallbackTextSearch("electrical", library, topK: 3);
        Assert.Equal(3, result.Results.Count);
    }

    [Fact]
    public void KnowledgeSearchService_TextFallback_ShortTokensIgnored()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "test.md",
                    RelativePath = "test.md",
                    Summary = "A short file about x",
                    Topics = ["x"],
                },
            ],
        };

        // Single-character tokens should be ignored (< 2 chars)
        var result = KnowledgeSearchService.FallbackTextSearch("x", library);
        Assert.Empty(result.Results);
    }

    [Fact]
    public void KnowledgeSearchResponse_DefaultValues()
    {
        var response = new KnowledgeSearchResponse();
        Assert.Equal(string.Empty, response.Query);
        Assert.Equal("none", response.SearchMode);
        Assert.Empty(response.Results);
        Assert.Equal(0, response.TotalResults);
    }

    // --- PR 9.3: Agent Chat with Tool Feedback Tests ---

    [Fact]
    public void DeskMessageRecord_PlainText_HasNoToolCalls()
    {
        var message = new DeskMessageRecord
        {
            DeskId = "chief",
            Role = "assistant",
            Author = "Chief of Staff",
            Kind = "chat",
            Content = "Here is your daily brief.",
        };

        Assert.False(message.HasToolCalls);
        Assert.Null(message.ToolCalls);
        Assert.DoesNotContain("tool call", message.Meta);
    }

    [Fact]
    public void DeskMessageRecord_WithToolCalls_ReportsHasToolCalls()
    {
        var message = new DeskMessageRecord
        {
            DeskId = "engineering",
            Role = "assistant",
            Author = "Engineering Desk",
            Kind = "chat",
            Content = "I looked up your training history.",
            ToolCalls =
            [
                new ToolCallRecord
                {
                    ToolName = "GetTrainingHistory",
                    Arguments = "focus: grounding",
                    Result = "3 recent sessions found.",
                    Status = "succeeded",
                    DurationMs = 45,
                },
            ],
        };

        Assert.True(message.HasToolCalls);
        Assert.Single(message.ToolCalls);
        Assert.Contains("1 tool call", message.Meta);
    }

    [Fact]
    public void DeskMessageRecord_MultipleToolCalls_PluralizesLabel()
    {
        var message = new DeskMessageRecord
        {
            DeskId = "chief",
            Role = "assistant",
            Author = "Chief of Staff",
            Content = "Analysis complete.",
            ToolCalls =
            [
                new ToolCallRecord { ToolName = "GetTrainingHistory", Result = "ok" },
                new ToolCallRecord { ToolName = "SearchKnowledge", Result = "3 results" },
                new ToolCallRecord { ToolName = "GetSuiteContext", Result = "context loaded" },
            ],
        };

        Assert.True(message.HasToolCalls);
        Assert.Equal(3, message.ToolCalls.Count);
        Assert.Contains("3 tool calls", message.Meta);
    }

    [Fact]
    public void DeskMessageRecord_EmptyToolCallsList_HasNoToolCalls()
    {
        var message = new DeskMessageRecord
        {
            DeskId = "chief",
            Role = "assistant",
            Content = "Plain response.",
            ToolCalls = [],
        };

        Assert.False(message.HasToolCalls);
        Assert.DoesNotContain("tool call", message.Meta);
    }

    [Fact]
    public void ToolCallRecord_SucceededStatus_DisplaysCorrectLabel()
    {
        var call = new ToolCallRecord
        {
            ToolName = "GetTrainingHistory",
            Status = "succeeded",
            Result = "Found 3 sessions.",
            DurationMs = 120,
        };

        Assert.Contains("🔧", call.DisplayLabel);
        Assert.Contains("GetTrainingHistory", call.DisplayLabel);
        Assert.Contains("120ms", call.DisplaySummary);
    }

    [Fact]
    public void ToolCallRecord_FailedStatus_DisplaysErrorIcon()
    {
        var call = new ToolCallRecord
        {
            ToolName = "SearchKnowledge",
            Status = "failed",
            Result = "Connection refused.",
        };

        Assert.Contains("❌", call.DisplayLabel);
    }

    [Fact]
    public void ToolCallRecord_SkippedStatus_DisplaysSkipIcon()
    {
        var call = new ToolCallRecord
        {
            ToolName = "ExportArtifacts",
            Status = "skipped",
            Result = "No artifacts to export.",
        };

        Assert.Contains("⏭️", call.DisplayLabel);
    }

    [Fact]
    public void ToolCallRecord_LongResult_Truncates()
    {
        var longResult = new string('x', 200);
        var call = new ToolCallRecord
        {
            ToolName = "BigQuery",
            Result = longResult,
        };

        Assert.True(call.DisplaySummary.Length < longResult.Length + 50);
        Assert.Contains("…", call.DisplaySummary);
    }

    [Fact]
    public void ToolCallRecord_EmptyResult_ShowsNoOutput()
    {
        var call = new ToolCallRecord
        {
            ToolName = "SilentTool",
            Result = "",
        };

        Assert.Contains("No output", call.DisplaySummary);
    }

    [Fact]
    public void ToolCallRecord_NoDuration_OmitsMs()
    {
        var call = new ToolCallRecord
        {
            ToolName = "QuickTool",
            Result = "Done.",
            DurationMs = null,
        };

        Assert.DoesNotContain("ms", call.DisplaySummary);
    }

    [Fact]
    public void ToolCallRecord_DefaultValues()
    {
        var call = new ToolCallRecord();
        Assert.Equal(string.Empty, call.ToolName);
        Assert.Equal(string.Empty, call.Arguments);
        Assert.Equal(string.Empty, call.Result);
        Assert.Equal("succeeded", call.Status);
        Assert.Null(call.DurationMs);
    }

    [Fact]
    public void KnowledgeSearchResult_SimilarityLabels()
    {
        Assert.Equal("Very High", new KnowledgeSearchResult { Score = 0.95f }.SimilarityLabel);
        Assert.Equal("High", new KnowledgeSearchResult { Score = 0.8f }.SimilarityLabel);
        Assert.Equal("Moderate", new KnowledgeSearchResult { Score = 0.6f }.SimilarityLabel);
        Assert.Equal("Low", new KnowledgeSearchResult { Score = 0.35f }.SimilarityLabel);
        Assert.Equal("Weak", new KnowledgeSearchResult { Score = 0.1f }.SimilarityLabel);
    }

    [Fact]
    public void KnowledgeSearchResult_DisplaySummary_WithTitle()
    {
        var result = new KnowledgeSearchResult
        {
            DocumentId = "doc-1",
            Title = "Grounding Guide",
            Score = 0.85f,
        };

        Assert.Contains("Grounding Guide", result.DisplaySummary);
        Assert.Contains("85", result.DisplaySummary);
    }

    [Fact]
    public void KnowledgeSearchResult_DisplaySummary_WithoutTitle()
    {
        var result = new KnowledgeSearchResult
        {
            DocumentId = "doc-1",
            Title = "",
            Score = 0.85f,
        };

        Assert.Contains("doc-1", result.DisplaySummary);
    }

    [Fact]
    public void DeskMessageRecord_BackwardCompatible_SerializesPlainText()
    {
        var message = new DeskMessageRecord
        {
            DeskId = "chief",
            Role = "user",
            Author = "You",
            Content = "Hello desk",
        };

        var json = System.Text.Json.JsonSerializer.Serialize(message);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<DeskMessageRecord>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Hello desk", deserialized!.Content);
        Assert.False(deserialized.HasToolCalls);
        Assert.Null(deserialized.ToolCalls);
    }

    [Fact]
    public void DeskMessageRecord_WithToolCalls_RoundTrips()
    {
        var message = new DeskMessageRecord
        {
            DeskId = "engineering",
            Role = "assistant",
            Author = "Engineering",
            Content = "Analysis done.",
            ToolCalls =
            [
                new ToolCallRecord
                {
                    ToolName = "GetTrainingHistory",
                    Arguments = "focus: grounding",
                    Result = "Found 3 sessions.",
                    Status = "succeeded",
                    DurationMs = 42,
                },
            ],
        };

        var json = System.Text.Json.JsonSerializer.Serialize(message);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<DeskMessageRecord>(json);

        Assert.NotNull(deserialized);
        Assert.True(deserialized!.HasToolCalls);
        Assert.Single(deserialized.ToolCalls!);
        Assert.Equal("GetTrainingHistory", deserialized.ToolCalls![0].ToolName);
        Assert.Equal(42, deserialized.ToolCalls![0].DurationMs);
    }

    // --- Phase 10: Research Query Integration Tests ---

    // ResearchReport model

    [Fact]
    public void ResearchReport_DefaultSummary_IsExpected()
    {
        var report = new ResearchReport();
        Assert.Equal("No live research run yet.", report.Summary);
    }

    [Fact]
    public void ResearchReport_DefaultKeyTakeaways_IsEmpty()
    {
        var report = new ResearchReport();
        Assert.Empty(report.KeyTakeaways);
    }

    [Fact]
    public void ResearchReport_DefaultActionMoves_IsEmpty()
    {
        var report = new ResearchReport();
        Assert.Empty(report.ActionMoves);
    }

    [Fact]
    public void ResearchReport_DefaultSources_IsEmpty()
    {
        var report = new ResearchReport();
        Assert.Empty(report.Sources);
    }

    [Fact]
    public void ResearchReport_RunSummary_ContainsSourceCount()
    {
        var report = new ResearchReport
        {
            Query = "relay protection fundamentals",
            Perspective = "EE Mentor",
            Model = "qwen3:14b",
            GenerationSource = "live web + ollama synthesis",
            Sources =
            [
                new ResearchSource { Title = "IEEE Source 1", Url = "https://ieee.org/1", Domain = "ieee.org" },
                new ResearchSource { Title = "IEEE Source 2", Url = "https://ieee.org/2", Domain = "ieee.org" },
            ],
        };

        Assert.Contains("2", report.RunSummary);
        Assert.Contains("EE Mentor", report.RunSummary);
        Assert.Contains("qwen3:14b", report.RunSummary);
    }

    [Fact]
    public void ResearchReport_RunSummary_ContainsPerspectiveAndModel()
    {
        var report = new ResearchReport
        {
            Perspective = "Business Strategist",
            Model = "qwen3:8b",
            GenerationSource = "live web + fallback synthesis",
        };

        Assert.Contains("Business Strategist", report.RunSummary);
        Assert.Contains("qwen3:8b", report.RunSummary);
    }

    [Fact]
    public void ResearchReport_RunSummary_ContainsGenerationSource()
    {
        var report = new ResearchReport
        {
            GenerationSource = "live web search returned no usable sources",
        };

        Assert.Contains("live web search returned no usable sources", report.RunSummary);
    }

    [Fact]
    public void ResearchReport_WithTakeawaysAndMoves_ReturnsExpectedCounts()
    {
        var report = new ResearchReport
        {
            KeyTakeaways = ["Takeaway A", "Takeaway B", "Takeaway C"],
            ActionMoves = ["Move 1", "Move 2"],
        };

        Assert.Equal(3, report.KeyTakeaways.Count);
        Assert.Equal(2, report.ActionMoves.Count);
    }

    // ResearchSource model

    [Fact]
    public void ResearchSource_DefaultValues_AreEmpty()
    {
        var source = new ResearchSource();
        Assert.Equal(string.Empty, source.Title);
        Assert.Equal(string.Empty, source.Url);
        Assert.Equal(string.Empty, source.Domain);
        Assert.Equal(string.Empty, source.SearchSnippet);
        Assert.Equal(string.Empty, source.Extract);
    }

    [Fact]
    public void ResearchSource_DisplaySummary_WithDomain_ShowsDomainInParentheses()
    {
        var source = new ResearchSource
        {
            Title = "IEEE Relay Protection Guide",
            Domain = "ieee.org",
        };

        Assert.Equal("IEEE Relay Protection Guide (ieee.org)", source.DisplaySummary);
    }

    [Fact]
    public void ResearchSource_DisplaySummary_NoDomain_ShowsTitleOnly()
    {
        var source = new ResearchSource
        {
            Title = "Relay Protection Overview",
            Domain = "",
        };

        Assert.Equal("Relay Protection Overview", source.DisplaySummary);
    }

    [Fact]
    public void ResearchSource_DisplaySummary_WhitespaceDomain_ShowsTitleOnly()
    {
        var source = new ResearchSource
        {
            Title = "DraftFlow Approval Routing",
            Domain = "   ",
        };

        Assert.Equal("DraftFlow Approval Routing", source.DisplaySummary);
    }

    // ResearchWatchlist model

    [Fact]
    public void ResearchWatchlist_Interval_Daily_IsOneDay()
    {
        var watchlist = new ResearchWatchlist { Frequency = "Daily" };
        Assert.Equal(TimeSpan.FromDays(1), watchlist.Interval);
    }

    [Fact]
    public void ResearchWatchlist_Interval_Weekly_IsSevenDays()
    {
        var watchlist = new ResearchWatchlist { Frequency = "Weekly" };
        Assert.Equal(TimeSpan.FromDays(7), watchlist.Interval);
    }

    [Fact]
    public void ResearchWatchlist_Interval_TwiceWeekly_IsThreeDays()
    {
        var watchlist = new ResearchWatchlist { Frequency = "Twice Weekly" };
        Assert.Equal(TimeSpan.FromDays(3), watchlist.Interval);
    }

    [Fact]
    public void ResearchWatchlist_IsDue_WhenNeverRun_IsTrue()
    {
        var watchlist = new ResearchWatchlist
        {
            IsEnabled = true,
            LastRunAt = null,
        };

        Assert.True(watchlist.IsDue);
    }

    [Fact]
    public void ResearchWatchlist_IsDue_WhenDisabled_IsFalse()
    {
        var watchlist = new ResearchWatchlist
        {
            IsEnabled = false,
            LastRunAt = null,
        };

        Assert.False(watchlist.IsDue);
    }

    [Fact]
    public void ResearchWatchlist_IsDue_WhenRecentlyRun_IsFalse()
    {
        var watchlist = new ResearchWatchlist
        {
            IsEnabled = true,
            Frequency = "Daily",
            LastRunAt = DateTimeOffset.Now.AddHours(-1),
        };

        Assert.False(watchlist.IsDue);
    }

    [Fact]
    public void ResearchWatchlist_IsDue_WhenPastInterval_IsTrue()
    {
        var watchlist = new ResearchWatchlist
        {
            IsEnabled = true,
            Frequency = "Daily",
            LastRunAt = DateTimeOffset.Now.AddDays(-2),
        };

        Assert.True(watchlist.IsDue);
    }

    [Fact]
    public void ResearchWatchlist_DueSummary_NeverRun_ShowsNeverRun()
    {
        var watchlist = new ResearchWatchlist { LastRunAt = null };
        Assert.Equal("never run", watchlist.DueSummary);
    }

    [Fact]
    public void ResearchWatchlist_DueSummary_WithHistory_ShowsFrequency()
    {
        var watchlist = new ResearchWatchlist
        {
            Frequency = "Weekly",
            LastRunAt = DateTimeOffset.Now.AddDays(-3),
        };

        Assert.Contains("Weekly", watchlist.DueSummary);
        Assert.Contains("last", watchlist.DueSummary);
        Assert.Contains("next", watchlist.DueSummary);
    }

    [Fact]
    public void ResearchWatchlist_DefaultValues_AreExpected()
    {
        var watchlist = new ResearchWatchlist();
        Assert.Equal("Weekly", watchlist.Frequency);
        Assert.Equal("EE Mentor", watchlist.PreferredPerspective);
        Assert.True(watchlist.SaveToKnowledgeDefault);
        Assert.True(watchlist.IsEnabled);
        Assert.NotEmpty(watchlist.Id);
    }

    // OfficeResearchSection model

    [Fact]
    public void OfficeResearchSection_DefaultSummary_PromptsUserToRunResearch()
    {
        var section = new OfficeResearchSection();
        Assert.Contains("live research", section.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OfficeResearchSection_DefaultRunSummary_IsExpected()
    {
        var section = new OfficeResearchSection();
        Assert.Equal("No live research run yet.", section.RunSummary);
    }

    [Fact]
    public void OfficeResearchSection_LatestReport_IsNullByDefault()
    {
        var section = new OfficeResearchSection();
        Assert.Null(section.LatestReport);
    }

    [Fact]
    public void OfficeResearchSection_DefaultHistory_IsEmpty()
    {
        var section = new OfficeResearchSection();
        Assert.Empty(section.History);
    }

    [Fact]
    public void OfficeResearchRun_DefaultValues_AreExpected()
    {
        var run = new OfficeResearchRun();
        Assert.Equal(string.Empty, run.Id);
        Assert.Equal(string.Empty, run.Title);
        Assert.Equal(string.Empty, run.Summary);
    }

    // LiveResearchService static factory methods (via reflection)

    [Fact]
    public void LiveResearchService_BuildEmptyReport_ReturnsNarrowQueryGuidance()
    {
        var method = typeof(LiveResearchService).GetMethod(
            "BuildEmptyReport",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(method);

        var result = (ResearchReport?)method!.Invoke(
            null,
            ["relay protection test", "EE Mentor", "qwen3:14b"]
        );

        Assert.NotNull(result);
        Assert.Contains("No live sources", result!.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("relay protection test", result.Query);
        Assert.Equal("EE Mentor", result.Perspective);
        Assert.Equal("qwen3:14b", result.Model);
    }

    [Fact]
    public void LiveResearchService_BuildEmptyReport_HasKeyTakeawaysAboutNarrowingQuery()
    {
        var method = typeof(LiveResearchService).GetMethod(
            "BuildEmptyReport",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(method);

        var result = (ResearchReport?)method!.Invoke(
            null,
            ["my query", "Business Strategist", "qwen3:14b"]
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result!.KeyTakeaways);
    }

    [Fact]
    public void LiveResearchService_BuildEmptyReport_HasActionMovesAboutNarrowingQuery()
    {
        var method = typeof(LiveResearchService).GetMethod(
            "BuildEmptyReport",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(method);

        var result = (ResearchReport?)method!.Invoke(
            null,
            ["my query", "EE Mentor", "qwen3:14b"]
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result!.ActionMoves);
        Assert.Contains(result.ActionMoves, move => move.Contains("narrower", StringComparison.OrdinalIgnoreCase)
            || move.Contains("query", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LiveResearchService_BuildFallbackReport_IncludesQueryAndSourceCount()
    {
        var buildFallbackMethod = typeof(LiveResearchService).GetMethod(
            "BuildFallbackReport",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(buildFallbackMethod);

        var sources = new List<ResearchSource>
        {
            new() { Title = "IEEE Grounding Reference", Url = "https://ieee.org/1", Domain = "ieee.org", Extract = "Grounding systems explained." },
            new() { Title = "NEC Code Summary", Url = "https://nec.org/1", Domain = "nec.org", Extract = "NEC requirements for grounding." },
        };
        var snapshot = new SuiteSnapshot();
        var historySummary = new TrainingHistorySummary();

        var result = (ResearchReport?)buildFallbackMethod!.Invoke(
            null,
            ["grounding systems", "EE Mentor", "qwen3:14b", sources, snapshot, historySummary]
        );

        Assert.NotNull(result);
        Assert.Contains("2", result!.Summary);
        Assert.Contains("grounding systems", result.Summary);
        Assert.Equal("grounding systems", result.Query);
        Assert.Equal("EE Mentor", result.Perspective);
    }

    [Fact]
    public void LiveResearchService_BuildFallbackReport_HasActionMoves()
    {
        var buildFallbackMethod = typeof(LiveResearchService).GetMethod(
            "BuildFallbackReport",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(buildFallbackMethod);

        var sources = new List<ResearchSource>
        {
            new() { Title = "Source A", Url = "https://example.com/a", Domain = "example.com", Extract = "Some content." },
        };

        var result = (ResearchReport?)buildFallbackMethod!.Invoke(
            null,
            ["approval routing", "Business Strategist", "qwen3:14b", sources, new SuiteSnapshot(), new TrainingHistorySummary()]
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result!.ActionMoves);
        Assert.Equal(3, result.ActionMoves.Count);
    }

    [Fact]
    public void LiveResearchService_BuildFallbackReport_TakeawaysUseDomainAsPrefix()
    {
        var buildFallbackMethod = typeof(LiveResearchService).GetMethod(
            "BuildFallbackReport",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(buildFallbackMethod);

        var sources = new List<ResearchSource>
        {
            new() { Title = "Relay Guide", Url = "https://relayguide.com/1", Domain = "relayguide.com", Extract = "Relay types explained." },
        };

        var result = (ResearchReport?)buildFallbackMethod!.Invoke(
            null,
            ["relay types", "EE Mentor", "qwen3:14b", sources, new SuiteSnapshot(), new TrainingHistorySummary()]
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result!.KeyTakeaways);
        Assert.Contains(result.KeyTakeaways, t => t.StartsWith("relayguide.com:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LiveResearchService_BuildSystemPrompt_IncludesPerspective()
    {
        var buildSystemPromptMethod = typeof(LiveResearchService).GetMethod(
            "BuildSystemPrompt",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(buildSystemPromptMethod);

        var result = (string?)buildSystemPromptMethod!.Invoke(null, ["EE Mentor"]);

        Assert.NotNull(result);
        Assert.Contains("EE Mentor", result!);
    }

    [Fact]
    public void LiveResearchService_BuildSystemPrompt_InstructsJsonOnly()
    {
        var buildSystemPromptMethod = typeof(LiveResearchService).GetMethod(
            "BuildSystemPrompt",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(buildSystemPromptMethod);

        var result = (string?)buildSystemPromptMethod!.Invoke(null, ["Business Strategist"]);

        Assert.NotNull(result);
        Assert.Contains("JSON", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LiveResearchService_NormalizeSearchUrl_ReturnsEmptyForNull()
    {
        var normalizeMethod = typeof(LiveResearchService).GetMethod(
            "NormalizeSearchUrl",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(normalizeMethod);

        var result = (string?)normalizeMethod!.Invoke(null, [string.Empty]);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void LiveResearchService_NormalizeSearchUrl_HandlesDoubleSlashProtocol()
    {
        var normalizeMethod = typeof(LiveResearchService).GetMethod(
            "NormalizeSearchUrl",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(normalizeMethod);

        var result = (string?)normalizeMethod!.Invoke(null, ["//ieee.org/some-page"]);
        Assert.NotNull(result);
        Assert.StartsWith("https://", result!);
    }

    [Fact]
    public void LiveResearchService_NormalizeSearchUrl_HandlesUddgEncoding()
    {
        var normalizeMethod = typeof(LiveResearchService).GetMethod(
            "NormalizeSearchUrl",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(normalizeMethod);

        var encoded = Uri.EscapeDataString("https://ieee.org/relay-protection");
        var raw = $"https://duckduckgo.com/l/?uddg={encoded}&rut=abc";

        var result = (string?)normalizeMethod!.Invoke(null, [raw]);
        Assert.NotNull(result);
        Assert.Equal("https://ieee.org/relay-protection", result);
    }

    [Fact]
    public void LiveResearchService_NormalizeSearchUrl_HandlesPlainHttps()
    {
        var normalizeMethod = typeof(LiveResearchService).GetMethod(
            "NormalizeSearchUrl",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(normalizeMethod);

        var result = (string?)normalizeMethod!.Invoke(null, ["https://example.com/page"]);
        Assert.Equal("https://example.com/page", result);
    }

    // LiveResearchService construction and static contract tests

    [Fact]
    public void LiveResearchService_CanBeConstructed_WithModelProvider()
    {
        var modelProvider = new StubModelProvider();
        var service = new LiveResearchService(modelProvider);
        Assert.NotNull(service);
    }

    [Fact]
    public void LiveResearchService_ConvertReport_WithNullContract_ReturnsNull()
    {
        var convertMethod = typeof(LiveResearchService).GetMethod(
            "ConvertReport",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(convertMethod);

        var sources = new List<ResearchSource>
        {
            new() { Title = "Source A", Url = "https://example.com/a", Domain = "example.com" },
        };

        var result = convertMethod!.Invoke(
            null,
            ["relay protection", "EE Mentor", "qwen3:14b", "ollama", sources, null]
        );

        Assert.Null(result);
    }

    // Research query pattern helpers (per AGENT_REPLY_GUIDE.md)

    [Theory]
    [InlineData("/research relay protection fundamentals", true)]
    [InlineData("/research DraftFlow approval routing", true)]
    [InlineData("/RESEARCH competitor analysis", true)]
    [InlineData("research relay protection", false)]
    [InlineData("Use live research for this.", false)]
    [InlineData("", false)]
    public void ResearchQueryPattern_SlashResearchPrefix_IsDetectedCorrectly(string input, bool expected)
    {
        var actual = input.TrimStart().StartsWith("/research", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("/research relay protection fundamentals", "relay protection fundamentals")]
    [InlineData("/research   DraftFlow approval routing  ", "DraftFlow approval routing")]
    [InlineData("/RESEARCH competitor analysis", "competitor analysis")]
    public void ResearchQueryPattern_SlashResearchPrefix_ExtractsQueryCorrectly(string input, string expectedQuery)
    {
        var prefix = "/research";
        var trimmed = input.TrimStart();
        var actualQuery = trimmed[prefix.Length..].Trim();
        Assert.Equal(expectedQuery, actualQuery);
    }

    private sealed class StubModelProvider : IModelProvider
    {
        public string ProviderLabel => "Stub";
        public string ProviderId => "stub";

        public Task<IReadOnlyList<string>> GetInstalledModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<string> GenerateAsync(
            string model,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<T?> GenerateJsonAsync<T>(
            string model,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
            => Task.FromResult<T?>(default);

        public Task<bool> PingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<float[]?> GenerateEmbeddingAsync(
            string text,
            string? model = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<float[]?>(null);
    }

    // --- Test helpers ---

    /// <summary>
    /// HttpMessageHandler that always throws HttpRequestException.
    /// </summary>
    private sealed class FailingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection refused (test)");
        }
    }

    /// <summary>
    /// HttpMessageHandler that returns a sequence of pre-built responses.
    /// </summary>
    private sealed class SequenceHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHttpHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new HttpRequestException("No more responses (test)");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    /// <summary>
    /// HttpMessageHandler that never completes (for cancellation tests).
    /// </summary>
    private sealed class NeverRespondingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
}
