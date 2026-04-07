using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

// ============================================================================
// Section 3 — Electrical QA/QC: Switchboard, Distribution Centre, and
// Control Centre Integration Tests
// (Watercare QA/QC Templates – mandatory tests per section 1.13 item 3)
// Mandatory checks: termination checks · protection relay settings ·
//                   interlocking verification · FAT/SAT records
// ============================================================================

public sealed class SwitchboardControlCentreQaQcIntegrationTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static OralDefenseService MakeService() =>
        new(new ThrowingModelProvider(), "test-model");

    // =========================================================================
    // Group 1 – OralDefenseScenario Model Construction
    // =========================================================================

    [Fact]
    public void Section3_SwitchboardScenario_CanBeCreatedWithMandatoryChecks()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "switchboard QA/QC",
            Title = "Switchboard Commissioning: Mandatory QA/QC Checks",
            Prompt =
                "Describe the mandatory QA/QC checks for a switchboard before energisation, "
                + "including termination checks, protection relay settings, "
                + "interlocking verification, and FAT/SAT records.",
            WhatGoodLooksLike =
                "A strong answer covers all four mandatory check categories, "
                + "names the risk eliminated by each check, and references the QA sign-off record.",
        };

        Assert.Equal("switchboard QA/QC", scenario.Topic);
        Assert.Contains("termination checks", scenario.Prompt);
        Assert.Contains("protection relay settings", scenario.Prompt);
        Assert.Contains("interlocking verification", scenario.Prompt);
        Assert.Contains("FAT/SAT records", scenario.Prompt);
    }

    [Fact]
    public void Section3_DistributionCentreScenario_CanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "distribution centre QA/QC",
            Title = "Distribution Centre Commissioning: Mandatory QA/QC Checks",
            Prompt =
                "What mandatory QA/QC checks must be completed before energising a distribution "
                + "centre? Include termination checks, protection relay settings, "
                + "interlocking verification, and FAT/SAT records.",
            WhatGoodLooksLike =
                "A strong answer applies all four mandatory checks to the distribution centre "
                + "context and references the relevant sign-off records.",
        };

        Assert.Equal("distribution centre QA/QC", scenario.Topic);
        Assert.Contains("Distribution Centre", scenario.Title);
        Assert.Contains("termination checks", scenario.Prompt);
        Assert.Contains("FAT/SAT records", scenario.Prompt);
    }

    [Fact]
    public void Section3_ControlCentreScenario_CanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "control centre QA/QC",
            Title = "Control Centre Commissioning: Mandatory QA/QC Checks",
            Prompt =
                "Explain the mandatory QA/QC checks required before a control centre is energised, "
                + "with specific reference to interlocking verification, protection relay settings, "
                + "termination checks, and FAT/SAT acceptance records.",
            WhatGoodLooksLike =
                "A strong answer demonstrates understanding of all four mandatory checks "
                + "as they apply to a control centre environment.",
        };

        Assert.Equal("control centre QA/QC", scenario.Topic);
        Assert.Contains("Control Centre", scenario.Title);
        Assert.Contains("interlocking verification", scenario.Prompt);
        Assert.Contains("protection relay settings", scenario.Prompt);
    }

    [Fact]
    public void Section3_OralDefenseScenario_DefaultTopic_IsElectricalProductionJudgment()
    {
        var scenario = new OralDefenseScenario();

        Assert.Equal("electrical production judgment", scenario.Topic);
        Assert.False(string.IsNullOrWhiteSpace(scenario.Title));
        Assert.False(string.IsNullOrWhiteSpace(scenario.Prompt));
        Assert.False(string.IsNullOrWhiteSpace(scenario.WhatGoodLooksLike));
        Assert.False(string.IsNullOrWhiteSpace(scenario.SuiteConnection));
    }

    [Fact]
    public void Section3_Scenario_WhatGoodLooksLike_CoversFourMandatoryChecks()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "switchboard QA/QC",
            WhatGoodLooksLike =
                "A strong answer covers all four mandatory QA/QC checks: "
                + "termination checks, protection relay settings, "
                + "interlocking verification, and FAT/SAT records. "
                + "The response should name the risk eliminated by each check.",
        };

        Assert.Contains("termination", scenario.WhatGoodLooksLike);
        Assert.Contains("protection relay", scenario.WhatGoodLooksLike);
        Assert.Contains("interlocking", scenario.WhatGoodLooksLike);
        Assert.Contains("FAT/SAT", scenario.WhatGoodLooksLike);
    }

    // =========================================================================
    // Group 2 – DefenseEvaluation and DefenseRubricItem Model Tests
    // =========================================================================

    [Fact]
    public void Section3_DefenseEvaluation_ScoreRatio_CalculatesCorrectly()
    {
        var evaluation = new DefenseEvaluation { TotalScore = 14, MaxScore = 20 };

        Assert.Equal(0.7, evaluation.ScoreRatio, precision: 5);
    }

    [Fact]
    public void Section3_DefenseEvaluation_ScoreRatio_ReturnsZero_WhenTotalScoreIsZero()
    {
        var evaluation = new DefenseEvaluation { TotalScore = 0, MaxScore = 20 };

        Assert.Equal(0.0, evaluation.ScoreRatio);
    }

    [Fact]
    public void Section3_DefenseEvaluation_ScoreRatio_ReturnsZero_WhenMaxScoreIsZero()
    {
        var evaluation = new DefenseEvaluation { TotalScore = 0, MaxScore = 0 };

        Assert.Equal(0.0, evaluation.ScoreRatio);
    }

    [Fact]
    public void Section3_DefenseEvaluation_DisplaySummary_ContainsScoreAndSummaryText()
    {
        var evaluation = new DefenseEvaluation
        {
            TotalScore = 14,
            MaxScore = 20,
            Summary = "Switchboard QA/QC answer evaluated.",
        };

        Assert.Contains("14/20", evaluation.DisplaySummary);
        Assert.Contains("Switchboard QA/QC answer evaluated.", evaluation.DisplaySummary);
    }

    [Fact]
    public void Section3_DefenseRubricItem_DisplaySummary_ContainsNameScoreAndFeedback()
    {
        var item = new DefenseRubricItem
        {
            Name = "Technical Correctness",
            Score = 3,
            Feedback = "Protection relay standards referenced correctly.",
        };

        Assert.Contains("Technical Correctness", item.DisplaySummary);
        Assert.Contains("3/4", item.DisplaySummary);
        Assert.Contains("Protection relay standards referenced correctly.", item.DisplaySummary);
    }

    [Fact]
    public void Section3_DefenseRubricItem_DefaultMaxScore_IsFour()
    {
        var item = new DefenseRubricItem { Name = "Clarity", Score = 2 };

        Assert.Equal(4, item.MaxScore);
    }

    [Fact]
    public void Section3_DefenseEvaluation_RubricItemScoreSum_EqualsTotalScore()
    {
        var items = new[]
        {
            new DefenseRubricItem { Name = "Technical Correctness",  Score = 3 },
            new DefenseRubricItem { Name = "Tradeoff Reasoning",     Score = 2 },
            new DefenseRubricItem { Name = "Failure-Mode Awareness", Score = 3 },
            new DefenseRubricItem { Name = "Validation Thinking",    Score = 3 },
            new DefenseRubricItem { Name = "Clarity",                Score = 3 },
        };

        var evaluation = new DefenseEvaluation
        {
            TotalScore = items.Sum(i => i.Score),
            MaxScore = items.Sum(i => i.MaxScore),
            RubricItems = items,
        };

        Assert.Equal(14, evaluation.TotalScore);
        Assert.Equal(20, evaluation.MaxScore);
        Assert.Equal(evaluation.RubricItems.Sum(i => i.Score), evaluation.TotalScore);
    }

    // =========================================================================
    // Group 3 – Fallback Scoring Tests
    // =========================================================================

    [Fact]
    public async Task Section3_FallbackScoring_ShortAnswer_ClarityScore_IsTwo()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "switchboard QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Check everything before turning on the power.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var clarity = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Clarity");
        Assert.NotNull(clarity);
        Assert.Equal(2, clarity!.Score);
    }

    [Fact]
    public async Task Section3_FallbackScoring_LongAnswer_ClarityScore_IsThree()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "switchboard QA/QC" };

        // Answer is more than 220 characters to trigger the "longEnough" path
        var longAnswer =
            "Before energising a switchboard you must complete all four mandatory "
            + "QA/QC checks: termination checks, relay settings, interlocking, and "
            + "FAT/SAT records. Each check is documented and signed by the responsible "
            + "engineer to confirm readiness for energisation.";

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            longAnswer,
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var clarity = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Clarity");
        Assert.NotNull(clarity);
        Assert.Equal(3, clarity!.Score);
    }

    [Fact]
    public async Task Section3_FallbackScoring_TradeoffKeyword_ScoresTradeoffReasoningThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "switchboard QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "The tradeoff between thorough relay testing and commissioning schedule pressure "
            + "must be managed through documented hold-point sign-offs.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var tradeoff = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Tradeoff Reasoning");
        Assert.NotNull(tradeoff);
        Assert.True(tradeoff!.Score >= 3, $"Expected Tradeoff Reasoning >= 3 but was {tradeoff.Score}");
    }

    [Fact]
    public async Task Section3_FallbackScoring_CompromiseKeyword_ScoresTradeoffReasoningThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "distribution centre QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "The compromise between testing depth and available commissioning time requires "
            + "prioritising safety-critical relay checks before schedule-driven checks.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var tradeoff = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Tradeoff Reasoning");
        Assert.NotNull(tradeoff);
        Assert.True(tradeoff!.Score >= 3, $"Expected Tradeoff Reasoning >= 3 but was {tradeoff.Score}");
    }

    [Fact]
    public async Task Section3_FallbackScoring_GroundKeyword_ScoresTechnicalCorrectnessThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "switchboard QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "The earth ground continuity of the switchboard enclosure must be verified "
            + "with a low-resistance ohmmeter before energisation.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Section3_FallbackScoring_VoltageKeyword_ScoresTechnicalCorrectnessThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "distribution centre QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Voltage levels across distribution centre bus sections must be measured "
            + "after interlocking verification is completed and before the load is connected.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Section3_FallbackScoring_NoKeywords_ScoresAllRubricItemsLow()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "switchboard QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Everything should be done carefully before starting.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        // Without domain keywords, tradeoff and failure-mode both score 1
        var tradeoff = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Tradeoff Reasoning");
        var failureMode = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Failure-Mode Awareness");
        Assert.NotNull(tradeoff);
        Assert.NotNull(failureMode);
        Assert.Equal(1, tradeoff!.Score);
        Assert.Equal(1, failureMode!.Score);
    }

    [Fact]
    public async Task Section3_FallbackScoring_MaxScoreIsAlwaysTwenty()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "switchboard QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Some answer.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.Equal(20, evaluation.MaxScore);
    }

    [Fact]
    public async Task Section3_FallbackScoring_HasExactlyFiveRubricItems()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "control centre QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Control centre interlocking circuits were tested and verified.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.Equal(5, evaluation.RubricItems.Count);
    }

    [Fact]
    public async Task Section3_FallbackScoring_HasThreeRecommendedFollowUps()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "switchboard QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Some answer.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.Equal(3, evaluation.RecommendedFollowUps.Count);
    }

    [Fact]
    public async Task Section3_FallbackScoring_TotalScoreWithinValidRange_ZeroToTwenty()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "switchboard QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Before energising the switchboard, protection relay settings must be verified "
            + "against the design standard to ensure correct overcurrent and earth fault pickup "
            + "values, tradeoff between testing depth and commissioning schedule must be documented.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.InRange(evaluation.TotalScore, 0, 20);
    }

    [Fact]
    public async Task Section3_FallbackScoring_DistributionCentreAnswer_ScoresTechnical()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario
        {
            Topic = "distribution centre QA/QC",
            Title = "Distribution Centre Protection Relay Verification",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Distribution centre protection relay settings must be checked against the approved "
            + "relay co-ordination study, and the standard overcurrent pickup values confirmed "
            + "before energising any bus section.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Section3_FallbackScoring_ControlCentreAnswer_ScoresValidation()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario
        {
            Topic = "control centre QA/QC",
            Title = "Control Centre Interlocking and FAT/SAT Verification",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Control centre interlocking circuits must be tested and verified to confirm that "
            + "incompatible switching combinations are prevented. FAT and SAT records are "
            + "then checked and signed off before handover.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(validation!.Score >= 3, $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    // =========================================================================
    // Group 4 – Fallback Scenario Creation Tests
    // =========================================================================

    [Fact]
    public async Task Section3_FallbackScenario_DistributionCentreTopic_IsPreservedAsScenarioTopic()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "distribution centre QA/QC"
        );

        Assert.Equal("distribution centre QA/QC", scenario.Topic);
    }

    [Fact]
    public async Task Section3_FallbackScenario_ControlCentreTopic_IsPreservedAsScenarioTopic()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "control centre QA/QC"
        );

        Assert.Equal("control centre QA/QC", scenario.Topic);
    }

    [Fact]
    public async Task Section3_FallbackScenario_Prompt_ContainsPreferredTopic()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "switchboard QA/QC"
        );

        Assert.Contains("switchboard QA/QC", scenario.Prompt);
    }

    [Fact]
    public async Task Section3_FallbackScenario_GenerationSource_IsFallbackOralDrill()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "switchboard QA/QC"
        );

        Assert.Equal("fallback oral drill", scenario.GenerationSource);
    }

    [Fact]
    public async Task Section3_FallbackScenario_SuiteSnapshotHotArea_AppearsInScenarioTitle()
    {
        var service = MakeService();
        var snapshot = new SuiteSnapshot { HotAreas = ["commissioning-switchboard-portal"] };

        var scenario = await service.CreateScenarioAsync(
            snapshot,
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "switchboard QA/QC"
        );

        Assert.Contains("commissioning-switchboard-portal", scenario.Title);
    }

    [Fact]
    public async Task Section3_FallbackScenario_WeakTopic_UsedWhenNoPreferredTopic()
    {
        var service = MakeService();
        var historySummary = new TrainingHistorySummary
        {
            WeakTopics =
            [
                new TopicMasterySummary { Topic = "distribution centre earthing", Attempted = 5, Correct = 1 },
            ],
        };

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            historySummary,
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>()
        );

        Assert.Equal("distribution centre earthing", scenario.Topic);
    }

    [Fact]
    public async Task Section3_FallbackScenario_ActiveTopic_UsedWhenNoOtherSource()
    {
        var service = MakeService();
        var learningProfile = new LearningProfile
        {
            ActiveTopics = ["control centre SCADA integration"],
        };

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            learningProfile,
            new LearningLibrary(),
            Array.Empty<StudyTrack>()
        );

        Assert.Equal("control centre SCADA integration", scenario.Topic);
    }

    // =========================================================================
    // Group 5 – Knowledge Search Tests
    // =========================================================================

    [Fact]
    public void Section3_KnowledgeSearch_DistributionCentreQuery_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "distribution-centre-qaqc.md",
                    RelativePath = "Knowledge/distribution-centre-qaqc.md",
                    Summary =
                        "Mandatory QA/QC tests for distribution centres: termination checks, "
                        + "protection relay settings, interlocking verification, FAT/SAT records.",
                    Topics = ["distribution centre", "QA/QC", "protection relay", "termination"],
                },
                new LearningDocument
                {
                    FileName = "lighting-circuit-test.md",
                    RelativePath = "Knowledge/lighting-circuit-test.md",
                    Summary = "RCD trip-time tests and lux verification for lighting circuits.",
                    Topics = ["lighting", "RCD", "lux"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch(
            "distribution centre protection relay termination",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("distribution-centre-qaqc.md", result.Results[0].Title);
    }

    [Fact]
    public void Section3_KnowledgeSearch_ControlCentreQuery_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "motor-installation.md",
                    RelativePath = "Knowledge/motor-installation.md",
                    Summary = "Motor rotation checks and thermal overload settings.",
                    Topics = ["motor", "rotation", "overload"],
                },
                new LearningDocument
                {
                    FileName = "control-centre-commissioning.md",
                    RelativePath = "Knowledge/control-centre-commissioning.md",
                    Summary =
                        "Control centre commissioning: interlocking verification, "
                        + "protection relay settings, FAT/SAT acceptance records.",
                    Topics = ["control centre", "interlocking", "protection relay", "FAT", "SAT"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch(
            "control centre interlocking verification FAT",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("control-centre-commissioning.md", result.Results[0].Title);
    }

    [Fact]
    public void Section3_KnowledgeSearch_AllThreeComponentTypes_InLibrary_ReturnsBestMatch()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "switchboard-qaqc.md",
                    RelativePath = "Knowledge/switchboard-qaqc.md",
                    Summary =
                        "Switchboard mandatory QA/QC: termination, protection relay, interlocking, FAT/SAT.",
                    Topics = ["switchboard", "termination", "protection relay", "interlocking", "FAT", "SAT"],
                },
                new LearningDocument
                {
                    FileName = "distribution-centre-qaqc.md",
                    RelativePath = "Knowledge/distribution-centre-qaqc.md",
                    Summary =
                        "Distribution centre mandatory QA/QC: termination, protection relay, interlocking, FAT/SAT.",
                    Topics = ["distribution centre", "termination", "protection relay", "interlocking"],
                },
                new LearningDocument
                {
                    FileName = "control-centre-qaqc.md",
                    RelativePath = "Knowledge/control-centre-qaqc.md",
                    Summary =
                        "Control centre mandatory QA/QC: termination, protection relay, interlocking, FAT/SAT.",
                    Topics = ["control centre", "termination", "protection relay", "interlocking"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch(
            "switchboard termination interlocking",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("switchboard-qaqc.md", result.Results[0].Title);
        Assert.Equal(3, library.Documents.Count);
    }

    // =========================================================================
    // Group 6 – LearningDocument and Section 3 Checklist Compliance Tests
    // =========================================================================

    [Fact]
    public void Section3_LearningDocument_Topics_IncludeAllFourMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "switchboard-section3-checklist.md",
            RelativePath = "Knowledge/switchboard-section3-checklist.md",
            Summary =
                "Watercare QA/QC section 3: switchboard, distribution centre, and control centre "
                + "mandatory tests. Covers termination checks, protection relay settings, "
                + "interlocking verification, and FAT/SAT records.",
            Topics =
            [
                "switchboard",
                "distribution centre",
                "control centre",
                "termination checks",
                "protection relay settings",
                "interlocking verification",
                "FAT/SAT records",
            ],
        };

        Assert.Contains("termination checks", document.Topics);
        Assert.Contains("protection relay settings", document.Topics);
        Assert.Contains("interlocking verification", document.Topics);
        Assert.Contains("FAT/SAT records", document.Topics);
    }

    [Fact]
    public void Section3_ChecklistCompliance_AllMandatoryChecks_PresentInLearningLibraryDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "qa-templates-electrical-section3.md",
                    RelativePath = "Knowledge/qa-templates-electrical-section3.md",
                    Summary =
                        "Section 3 mandatory QA/QC checks for switchboards, distribution centres, "
                        + "and control centres as specified in the Watercare electrical standards: "
                        + "termination checks, protection relay settings, interlocking verification, "
                        + "FAT/SAT records.",
                    Topics =
                    [
                        "switchboard",
                        "distribution centre",
                        "control centre",
                        "termination",
                        "protection relay",
                        "interlocking",
                        "FAT",
                        "SAT",
                    ],
                },
            ],
        };

        var document = library.Documents.FirstOrDefault(
            d => d.FileName == "qa-templates-electrical-section3.md"
        );

        Assert.NotNull(document);
        Assert.Contains("switchboard", document!.Topics);
        Assert.Contains("distribution centre", document.Topics);
        Assert.Contains("control centre", document.Topics);
        Assert.Contains("termination", document.Topics);
        Assert.Contains("protection relay", document.Topics);
        Assert.Contains("interlocking", document.Topics);
        Assert.Contains("FAT", document.Topics);
        Assert.Contains("SAT", document.Topics);
    }

    [Fact]
    public async Task Section3_FallbackEvaluation_NextReviewRecommendation_IsNonEmpty()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "switchboard QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Termination checks and relay verification should be completed before energisation.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.False(string.IsNullOrWhiteSpace(evaluation.NextReviewRecommendation));
    }

    // -------------------------------------------------------------------------
    // Private stub – forces OralDefenseService into deterministic fallback mode
    // -------------------------------------------------------------------------

    private sealed class ThrowingModelProvider : IModelProvider
    {
        public string ProviderId => "throwing-stub";
        public string ProviderLabel => "Throwing Stub";

        public Task<IReadOnlyList<string>> GetInstalledModelsAsync(
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Stub provider unavailable.");

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
