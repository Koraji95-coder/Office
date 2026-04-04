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
