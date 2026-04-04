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
    public void NormalizeRoute_ReturnsExpectedValue(string? route, string expected)
    {
        var actual = OfficeRouteCatalog.NormalizeRoute(route);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(OfficeRouteCatalog.BusinessRoute, "Growth Ops")]
    [InlineData(OfficeRouteCatalog.ChiefRoute, "Chief of Staff")]
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

        var baseDirectory = Path.Combine(
            @"C:\Users\DustinWard\Documents\GitHub\Office",
            "artifacts",
            "DailyDesk.Broker",
            "publish"
        );

        var actual = (string?)resolveMethod!.Invoke(null, [baseDirectory]);

        Assert.Equal(
            Path.GetFullPath(@"C:\Users\DustinWard\Documents\GitHub\Office"),
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
        var library = new LearningLibrary
        {
            RootPath = @"C:\Users\DustinWard\Documents\GitHub\Office\Knowledge",
            Documents = [],
        };

        var profile = service.Build(library, new TrainingHistorySummary(), new SuiteSnapshot());

        Assert.Contains("knowledge library", profile.CurrentNeed, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\Users\DustinWard", profile.CurrentNeed, StringComparison.OrdinalIgnoreCase);
    }
}
