using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

// ============================================================================
// Section 1.13 — Electrical QA/QC: Full Mandatory-Test Coverage
// (Watercare QA/QC Templates – all 7 mandatory-test categories)
//
// Category 1: General Electrical Installation
//             – earthing continuity, insulation resistance, polarity, functional tests
// Category 2: Cables and Conduit
//             – installation inspection, cable pulling records, megger test results
// Category 3: Switchboards, Distribution Centres, and Control Centres
//             – covered by SwitchboardControlCentreQaQcIntegrationTests.cs
// Category 4: Motors and Drives
//             – rotation checks, no-load/full-load current, thermal overload settings
// Category 5: Lighting and Small Power
//             – circuit continuity, RCD trip-time testing, lux level verification
// Category 6: Instrumentation and Control Wiring
//             – loop checks, signal calibration records, PLC I/O verification
// Category 7: Earthing and Bonding Systems
//             – earth resistance measurements, bonding continuity records
// ============================================================================

public sealed class ElectricalQaQcSection113IntegrationTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static OralDefenseService MakeService() =>
        new(new ThrowingModelProvider(), "test-model");

    // =========================================================================
    // Group 1 – OralDefenseScenario Model Construction
    //           (one scenario per section-1.13 category)
    // =========================================================================

    // --- Category 1: General Electrical Installation -------------------------

    [Fact]
    public void Section113_Cat1_GeneralInstallation_ScenarioCanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "general electrical installation QA/QC",
            Title = "General Electrical Installation: Mandatory QA/QC Checks",
            Prompt =
                "Describe the mandatory QA/QC checks for a general electrical installation "
                + "before energisation, including earthing continuity, insulation resistance, "
                + "polarity verification, and functional tests.",
            WhatGoodLooksLike =
                "A strong answer covers all four checks, names the risk eliminated by each, "
                + "and references the required QA sign-off record.",
        };

        Assert.Equal("general electrical installation QA/QC", scenario.Topic);
        Assert.Contains("earthing continuity", scenario.Prompt);
        Assert.Contains("insulation resistance", scenario.Prompt);
        Assert.Contains("polarity", scenario.Prompt);
        Assert.Contains("functional tests", scenario.Prompt);
    }

    [Fact]
    public void Section113_Cat1_GeneralInstallation_WhatGoodLooksLike_MentionsRisk()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "general electrical installation QA/QC",
            WhatGoodLooksLike =
                "A strong answer explains the risk eliminated by earthing continuity failures, "
                + "insulation breakdown, reversed polarity, and incomplete functional tests.",
        };

        Assert.Contains("risk", scenario.WhatGoodLooksLike);
        Assert.Contains("earthing continuity", scenario.WhatGoodLooksLike);
        Assert.Contains("insulation", scenario.WhatGoodLooksLike);
        Assert.Contains("polarity", scenario.WhatGoodLooksLike);
    }

    // --- Category 2: Cables and Conduit --------------------------------------

    [Fact]
    public void Section113_Cat2_CablesAndConduit_ScenarioCanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "cables and conduit QA/QC",
            Title = "Cables and Conduit: Mandatory QA/QC Checks",
            Prompt =
                "What mandatory QA/QC checks must be completed for cables and conduit installations? "
                + "Include installation inspection, cable pulling records, and megger test results.",
            WhatGoodLooksLike =
                "A strong answer covers installation inspection, pulling-tension records, "
                + "and megger insulation test results, with sign-off requirements for each.",
        };

        Assert.Equal("cables and conduit QA/QC", scenario.Topic);
        Assert.Contains("installation inspection", scenario.Prompt);
        Assert.Contains("cable pulling records", scenario.Prompt);
        Assert.Contains("megger test results", scenario.Prompt);
    }

    [Fact]
    public void Section113_Cat2_CablesAndConduit_WhatGoodLooksLike_MentionsSignOff()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "cables and conduit QA/QC",
            WhatGoodLooksLike =
                "A strong answer references sign-off sheets for installation inspection, "
                + "cable pulling records, and megger test results.",
        };

        Assert.Contains("sign-off", scenario.WhatGoodLooksLike);
        Assert.Contains("megger", scenario.WhatGoodLooksLike);
    }

    // --- Category 4: Motors and Drives ---------------------------------------

    [Fact]
    public void Section113_Cat4_MotorsAndDrives_ScenarioCanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "motors and drives QA/QC",
            Title = "Motors and Drives: Mandatory QA/QC Checks",
            Prompt =
                "Explain the mandatory QA/QC checks required before a motor or drive system "
                + "is commissioned, including rotation checks, no-load and full-load current "
                + "measurements, and thermal overload settings.",
            WhatGoodLooksLike =
                "A strong answer covers rotation direction verification, current measurements "
                + "at no-load and full-load, and correct thermal overload relay settings.",
        };

        Assert.Equal("motors and drives QA/QC", scenario.Topic);
        Assert.Contains("rotation checks", scenario.Prompt);
        Assert.Contains("no-load", scenario.Prompt);
        Assert.Contains("full-load current", scenario.Prompt);
        Assert.Contains("thermal overload settings", scenario.Prompt);
    }

    [Fact]
    public void Section113_Cat4_MotorsAndDrives_WhatGoodLooksLike_CoversMandatoryChecks()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "motors and drives QA/QC",
            WhatGoodLooksLike =
                "A strong answer covers rotation direction, no-load current, full-load current, "
                + "and thermal overload settings as four separate mandatory checks.",
        };

        Assert.Contains("rotation", scenario.WhatGoodLooksLike);
        Assert.Contains("no-load", scenario.WhatGoodLooksLike);
        Assert.Contains("full-load", scenario.WhatGoodLooksLike);
        Assert.Contains("thermal overload", scenario.WhatGoodLooksLike);
    }

    // --- Category 5: Lighting and Small Power --------------------------------

    [Fact]
    public void Section113_Cat5_LightingAndSmallPower_ScenarioCanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "lighting and small power QA/QC",
            Title = "Lighting and Small Power: Mandatory QA/QC Checks",
            Prompt =
                "Describe the mandatory QA/QC checks for lighting and small power circuits, "
                + "including circuit continuity, RCD trip-time testing, and lux level verification.",
            WhatGoodLooksLike =
                "A strong answer confirms circuit continuity, records RCD trip-time results "
                + "against the acceptable threshold, and documents lux level measurements.",
        };

        Assert.Equal("lighting and small power QA/QC", scenario.Topic);
        Assert.Contains("circuit continuity", scenario.Prompt);
        Assert.Contains("RCD trip-time testing", scenario.Prompt);
        Assert.Contains("lux level verification", scenario.Prompt);
    }

    [Fact]
    public void Section113_Cat5_LightingAndSmallPower_WhatGoodLooksLike_MentionsRCD()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "lighting and small power QA/QC",
            WhatGoodLooksLike =
                "A strong answer includes RCD trip-time results against AS/NZS 3760 thresholds "
                + "and lux level measurements compared to design requirements.",
        };

        Assert.Contains("RCD", scenario.WhatGoodLooksLike);
        Assert.Contains("lux", scenario.WhatGoodLooksLike);
    }

    // --- Category 6: Instrumentation and Control Wiring ----------------------

    [Fact]
    public void Section113_Cat6_InstrumentationAndControlWiring_ScenarioCanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "instrumentation and control wiring QA/QC",
            Title = "Instrumentation and Control Wiring: Mandatory QA/QC Checks",
            Prompt =
                "What mandatory QA/QC checks are required for instrumentation and control wiring? "
                + "Include loop checks, signal calibration records, and PLC I/O verification.",
            WhatGoodLooksLike =
                "A strong answer covers completed loop check sheets, calibration certificates "
                + "for each instrument, and PLC I/O point-by-point verification records.",
        };

        Assert.Equal("instrumentation and control wiring QA/QC", scenario.Topic);
        Assert.Contains("loop checks", scenario.Prompt);
        Assert.Contains("signal calibration records", scenario.Prompt);
        Assert.Contains("PLC I/O verification", scenario.Prompt);
    }

    [Fact]
    public void Section113_Cat6_InstrumentationAndControlWiring_WhatGoodLooksLike_MentionsCalibration()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "instrumentation and control wiring QA/QC",
            WhatGoodLooksLike =
                "A strong answer references calibration certificates, loop check sheets, "
                + "and PLC I/O verification records as mandatory sign-off deliverables.",
        };

        Assert.Contains("calibration", scenario.WhatGoodLooksLike);
        Assert.Contains("loop check", scenario.WhatGoodLooksLike);
        Assert.Contains("PLC", scenario.WhatGoodLooksLike);
    }

    // --- Category 7: Earthing and Bonding Systems ----------------------------

    [Fact]
    public void Section113_Cat7_EarthingAndBonding_ScenarioCanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "earthing and bonding systems QA/QC",
            Title = "Earthing and Bonding Systems: Mandatory QA/QC Checks",
            Prompt =
                "Explain the mandatory QA/QC checks for earthing and bonding systems, "
                + "including earth resistance measurements and bonding continuity records.",
            WhatGoodLooksLike =
                "A strong answer covers fall-of-potential earth resistance test results, "
                + "bonding continuity measurements, and required sign-off records.",
        };

        Assert.Equal("earthing and bonding systems QA/QC", scenario.Topic);
        Assert.Contains("earth resistance measurements", scenario.Prompt);
        Assert.Contains("bonding continuity records", scenario.Prompt);
    }

    [Fact]
    public void Section113_Cat7_EarthingAndBonding_WhatGoodLooksLike_MentionsResistance()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "earthing and bonding systems QA/QC",
            WhatGoodLooksLike =
                "A strong answer references earth resistance test results against the design "
                + "limit and documents bonding continuity for all metallic enclosures.",
        };

        Assert.Contains("earth resistance", scenario.WhatGoodLooksLike);
        Assert.Contains("bonding continuity", scenario.WhatGoodLooksLike);
    }

    // =========================================================================
    // Group 2 – DefenseEvaluation and DefenseRubricItem Model Tests
    // =========================================================================

    [Fact]
    public void Section113_DefenseEvaluation_ScoreRatio_CalculatesCorrectly()
    {
        var evaluation = new DefenseEvaluation { TotalScore = 16, MaxScore = 20 };

        Assert.Equal(0.8, evaluation.ScoreRatio, precision: 5);
    }

    [Fact]
    public void Section113_DefenseEvaluation_ScoreRatio_ReturnsZero_WhenMaxScoreIsZero()
    {
        var evaluation = new DefenseEvaluation { TotalScore = 0, MaxScore = 0 };

        Assert.Equal(0.0, evaluation.ScoreRatio);
    }

    [Fact]
    public void Section113_DefenseEvaluation_DisplaySummary_ContainsScoreAndSummary()
    {
        var evaluation = new DefenseEvaluation
        {
            TotalScore = 16,
            MaxScore = 20,
            Summary = "General electrical installation QA/QC answer evaluated.",
        };

        Assert.Contains("16/20", evaluation.DisplaySummary);
        Assert.Contains("General electrical installation QA/QC answer evaluated.", evaluation.DisplaySummary);
    }

    [Fact]
    public void Section113_DefenseRubricItem_DisplaySummary_ContainsNameScoreAndFeedback()
    {
        var item = new DefenseRubricItem
        {
            Name = "Technical Correctness",
            Score = 4,
            Feedback = "Insulation resistance and polarity correctly referenced.",
        };

        Assert.Contains("Technical Correctness", item.DisplaySummary);
        Assert.Contains("4/4", item.DisplaySummary);
        Assert.Contains("Insulation resistance and polarity correctly referenced.", item.DisplaySummary);
    }

    [Fact]
    public void Section113_DefenseRubricItem_DefaultMaxScore_IsFour()
    {
        var item = new DefenseRubricItem { Name = "Validation Thinking", Score = 3 };

        Assert.Equal(4, item.MaxScore);
    }

    [Fact]
    public void Section113_DefenseEvaluation_RubricItemScoreSum_EqualsTotalScore()
    {
        var items = new[]
        {
            new DefenseRubricItem { Name = "Technical Correctness",  Score = 4 },
            new DefenseRubricItem { Name = "Tradeoff Reasoning",     Score = 3 },
            new DefenseRubricItem { Name = "Failure-Mode Awareness", Score = 3 },
            new DefenseRubricItem { Name = "Validation Thinking",    Score = 3 },
            new DefenseRubricItem { Name = "Clarity",                Score = 3 },
        };

        var evaluation = new DefenseEvaluation
        {
            TotalScore = items.Sum(i => i.Score),
            MaxScore   = items.Sum(i => i.MaxScore),
            RubricItems = items,
        };

        Assert.Equal(16, evaluation.TotalScore);
        Assert.Equal(20, evaluation.MaxScore);
        Assert.Equal(evaluation.RubricItems.Sum(i => i.Score), evaluation.TotalScore);
    }

    // =========================================================================
    // Group 3 – Fallback Scoring Tests (one per relevant section-1.13 topic)
    // =========================================================================

    [Fact]
    public async Task Section113_FallbackScoring_Cat1_InsulationKeyword_ScoresTechnicalCorrectness()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "general electrical installation QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Insulation resistance of all cables must be measured with a 500 V megger "
            + "before energisation to confirm no short circuits to earth.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Section113_FallbackScoring_Cat1_GroundKeyword_ScoresTechnicalCorrectness()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "general electrical installation QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Earth ground continuity of the installation must be verified before energising "
            + "any panel or distribution board.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Section113_FallbackScoring_Cat2_MeggerKeyword_ScoresTechnicalCorrectness()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "cables and conduit QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "All cable runs must pass a megger insulation resistance test after installation "
            + "and before connection to equipment, with results recorded on the QA sheet.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Section113_FallbackScoring_Cat4_MotorAnswer_ScoresValidation()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "motors and drives QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Motor rotation direction must be checked with the drive uncoupled before connecting "
            + "the load. No-load current is then measured and verified against the nameplate value.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(validation!.Score >= 3, $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public async Task Section113_FallbackScoring_Cat4_VoltageKeyword_ScoresTechnicalCorrectness()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "motors and drives QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Voltage supply to the drive must be within ±10 % of nameplate rating before "
            + "the motor rotation check is performed.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Section113_FallbackScoring_Cat5_LightingAnswer_ScoresValidation()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "lighting and small power QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "All RCD devices must be tested to verify that they trip within the required "
            + "time limit. Lux levels are then checked against the design specification.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(validation!.Score >= 3, $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public async Task Section113_FallbackScoring_Cat6_InstrumentationAnswer_ScoresValidation()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "instrumentation and control wiring QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Loop checks must verify that each 4–20 mA signal is correctly mapped from "
            + "the field instrument to the PLC input. Signal calibration records are checked "
            + "to confirm instrument accuracy before commissioning.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(validation!.Score >= 3, $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public async Task Section113_FallbackScoring_Cat7_EarthingAnswer_ScoresTechnical()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "earthing and bonding systems QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Earth electrode resistance must be measured using the fall-of-potential method "
            + "and must not exceed the design-specified maximum. Bonding conductor continuity "
            + "is verified with a low-resistance ohmmeter before energisation.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Section113_FallbackScoring_TradeoffKeyword_ScoresTradeoffReasoning()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "cables and conduit QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "The tradeoff between comprehensive megger testing of every cable and commissioning "
            + "schedule pressure must be managed through documented hold-point sign-offs.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var tradeoff = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Tradeoff Reasoning");
        Assert.NotNull(tradeoff);
        Assert.True(tradeoff!.Score >= 3, $"Expected Tradeoff Reasoning >= 3 but was {tradeoff.Score}");
    }

    [Fact]
    public async Task Section113_FallbackScoring_NoKeywords_TradeoffAndFailureModeBothScoreLow()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "earthing and bonding systems QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "All checks should be done properly before energisation.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var tradeoff = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Tradeoff Reasoning");
        var failureMode = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Failure-Mode Awareness");
        Assert.NotNull(tradeoff);
        Assert.NotNull(failureMode);
        Assert.Equal(1, tradeoff!.Score);
        Assert.Equal(1, failureMode!.Score);
    }

    [Fact]
    public async Task Section113_FallbackScoring_MaxScoreIsAlwaysTwenty()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "motors and drives QA/QC" };

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
    public async Task Section113_FallbackScoring_HasExactlyFiveRubricItems()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "lighting and small power QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "RCD trip times and lux levels were tested and verified on all circuits.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.Equal(5, evaluation.RubricItems.Count);
    }

    [Fact]
    public async Task Section113_FallbackScoring_HasThreeRecommendedFollowUps()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "instrumentation and control wiring QA/QC" };

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
    public async Task Section113_FallbackScoring_TotalScoreWithinValidRange()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "general electrical installation QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Earth ground continuity and voltage polarity must be verified before energisation. "
            + "The tradeoff between test depth and schedule must be managed with signed hold-points.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.InRange(evaluation.TotalScore, 0, 20);
    }

    [Fact]
    public async Task Section113_FallbackScoring_NextReviewRecommendation_IsNonEmpty()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "earthing and bonding systems QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Earth electrode resistance was measured and bonding continuity was confirmed.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.False(string.IsNullOrWhiteSpace(evaluation.NextReviewRecommendation));
    }

    // =========================================================================
    // Group 4 – Fallback Scenario Creation Tests
    // =========================================================================

    [Fact]
    public async Task Section113_FallbackScenario_Cat1_TopicPreserved()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "general electrical installation QA/QC"
        );

        Assert.Equal("general electrical installation QA/QC", scenario.Topic);
    }

    [Fact]
    public async Task Section113_FallbackScenario_Cat2_TopicPreserved()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "cables and conduit QA/QC"
        );

        Assert.Equal("cables and conduit QA/QC", scenario.Topic);
    }

    [Fact]
    public async Task Section113_FallbackScenario_Cat4_TopicPreserved()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "motors and drives QA/QC"
        );

        Assert.Equal("motors and drives QA/QC", scenario.Topic);
    }

    [Fact]
    public async Task Section113_FallbackScenario_Cat5_TopicPreserved()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "lighting and small power QA/QC"
        );

        Assert.Equal("lighting and small power QA/QC", scenario.Topic);
    }

    [Fact]
    public async Task Section113_FallbackScenario_Cat6_TopicPreserved()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "instrumentation and control wiring QA/QC"
        );

        Assert.Equal("instrumentation and control wiring QA/QC", scenario.Topic);
    }

    [Fact]
    public async Task Section113_FallbackScenario_Cat7_TopicPreserved()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "earthing and bonding systems QA/QC"
        );

        Assert.Equal("earthing and bonding systems QA/QC", scenario.Topic);
    }

    [Fact]
    public async Task Section113_FallbackScenario_Prompt_ContainsPreferredTopic()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "motors and drives QA/QC"
        );

        Assert.Contains("motors and drives QA/QC", scenario.Prompt);
    }

    [Fact]
    public async Task Section113_FallbackScenario_GenerationSource_IsFallbackOralDrill()
    {
        var service = MakeService();

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "cables and conduit QA/QC"
        );

        Assert.Equal("fallback oral drill", scenario.GenerationSource);
    }

    [Fact]
    public async Task Section113_FallbackScenario_HotArea_AppearsInTitle()
    {
        var service = MakeService();
        var snapshot = new SuiteSnapshot { HotAreas = ["commissioning-earthing-portal"] };

        var scenario = await service.CreateScenarioAsync(
            snapshot,
            new TrainingHistorySummary(),
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>(),
            preferredTopic: "earthing and bonding systems QA/QC"
        );

        Assert.Contains("commissioning-earthing-portal", scenario.Title);
    }

    [Fact]
    public async Task Section113_FallbackScenario_WeakTopic_UsedWhenNoPreferredTopic()
    {
        var service = MakeService();
        var historySummary = new TrainingHistorySummary
        {
            WeakTopics =
            [
                new TopicMasterySummary { Topic = "motor thermal overload settings", Attempted = 5, Correct = 1 },
            ],
        };

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            historySummary,
            new LearningProfile(),
            new LearningLibrary(),
            Array.Empty<StudyTrack>()
        );

        Assert.Equal("motor thermal overload settings", scenario.Topic);
    }

    [Fact]
    public async Task Section113_FallbackScenario_ActiveTopic_UsedWhenNoOtherSource()
    {
        var service = MakeService();
        var learningProfile = new LearningProfile
        {
            ActiveTopics = ["PLC I/O verification and loop checks"],
        };

        var scenario = await service.CreateScenarioAsync(
            new SuiteSnapshot(),
            new TrainingHistorySummary(),
            learningProfile,
            new LearningLibrary(),
            Array.Empty<StudyTrack>()
        );

        Assert.Equal("PLC I/O verification and loop checks", scenario.Topic);
    }

    // =========================================================================
    // Group 5 – Knowledge Search Tests
    // =========================================================================

    [Fact]
    public void Section113_KnowledgeSearch_GeneralInstallationQuery_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "general-installation-qaqc.md",
                    RelativePath = "Knowledge/general-installation-qaqc.md",
                    Summary =
                        "General electrical installation mandatory QA/QC: earthing continuity, "
                        + "insulation resistance, polarity verification, functional tests.",
                    Topics = ["general installation", "earthing", "insulation", "polarity", "functional test"],
                },
                new LearningDocument
                {
                    FileName = "motor-commissioning.md",
                    RelativePath = "Knowledge/motor-commissioning.md",
                    Summary = "Motor rotation checks and no-load current measurements.",
                    Topics = ["motor", "rotation", "current"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch(
            "general installation earthing continuity insulation resistance",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("general-installation-qaqc.md", result.Results[0].Title);
    }

    [Fact]
    public void Section113_KnowledgeSearch_CablesAndConduitQuery_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "lighting-circuit-test.md",
                    RelativePath = "Knowledge/lighting-circuit-test.md",
                    Summary = "RCD trip-time tests and lux verification for lighting circuits.",
                    Topics = ["lighting", "RCD", "lux"],
                },
                new LearningDocument
                {
                    FileName = "cables-and-conduit-qaqc.md",
                    RelativePath = "Knowledge/cables-and-conduit-qaqc.md",
                    Summary =
                        "Cables and conduit mandatory QA/QC: installation inspection, "
                        + "cable pulling records, megger insulation test results.",
                    Topics = ["cable", "conduit", "megger", "installation inspection", "pulling records"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch(
            "cable conduit megger installation inspection",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("cables-and-conduit-qaqc.md", result.Results[0].Title);
    }

    [Fact]
    public void Section113_KnowledgeSearch_MotorsAndDrivesQuery_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "instrumentation-loop-check.md",
                    RelativePath = "Knowledge/instrumentation-loop-check.md",
                    Summary = "Loop checks and signal calibration for instruments.",
                    Topics = ["loop check", "calibration", "PLC"],
                },
                new LearningDocument
                {
                    FileName = "motors-and-drives-qaqc.md",
                    RelativePath = "Knowledge/motors-and-drives-qaqc.md",
                    Summary =
                        "Motors and drives mandatory QA/QC: rotation checks, no-load and "
                        + "full-load current measurements, thermal overload settings.",
                    Topics = ["motor", "drive", "rotation", "overload", "current"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch(
            "motor rotation overload current",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("motors-and-drives-qaqc.md", result.Results[0].Title);
    }

    [Fact]
    public void Section113_KnowledgeSearch_AllSevenCategories_InLibrary_ReturnsBestMatch()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "general-installation-qaqc.md",
                    RelativePath = "Knowledge/general-installation-qaqc.md",
                    Summary = "General electrical installation: earthing, insulation, polarity, functional.",
                    Topics = ["general installation", "earthing", "insulation", "polarity"],
                },
                new LearningDocument
                {
                    FileName = "cables-and-conduit-qaqc.md",
                    RelativePath = "Knowledge/cables-and-conduit-qaqc.md",
                    Summary = "Cables and conduit: installation inspection, pulling records, megger.",
                    Topics = ["cable", "conduit", "megger", "pulling records"],
                },
                new LearningDocument
                {
                    FileName = "switchboard-qaqc.md",
                    RelativePath = "Knowledge/switchboard-qaqc.md",
                    Summary = "Switchboard: termination, protection relay, interlocking, FAT/SAT.",
                    Topics = ["switchboard", "protection relay", "interlocking", "FAT", "SAT"],
                },
                new LearningDocument
                {
                    FileName = "motors-and-drives-qaqc.md",
                    RelativePath = "Knowledge/motors-and-drives-qaqc.md",
                    Summary = "Motors and drives: rotation, current, overload.",
                    Topics = ["motor", "drive", "rotation", "overload"],
                },
                new LearningDocument
                {
                    FileName = "lighting-and-small-power-qaqc.md",
                    RelativePath = "Knowledge/lighting-and-small-power-qaqc.md",
                    Summary = "Lighting and small power: circuit continuity, RCD, lux.",
                    Topics = ["lighting", "RCD", "lux", "circuit continuity"],
                },
                new LearningDocument
                {
                    FileName = "instrumentation-control-wiring-qaqc.md",
                    RelativePath = "Knowledge/instrumentation-control-wiring-qaqc.md",
                    Summary = "Instrumentation and control: loop checks, calibration, PLC I/O.",
                    Topics = ["instrumentation", "loop check", "calibration", "PLC"],
                },
                new LearningDocument
                {
                    FileName = "earthing-and-bonding-qaqc.md",
                    RelativePath = "Knowledge/earthing-and-bonding-qaqc.md",
                    Summary = "Earthing and bonding: earth resistance, bonding continuity.",
                    Topics = ["earthing", "bonding", "earth resistance", "continuity"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch(
            "earthing bonding earth resistance continuity",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("earthing-and-bonding-qaqc.md", result.Results[0].Title);
        Assert.Equal(7, library.Documents.Count);
    }

    // =========================================================================
    // Group 6 – LearningDocument Checklist Compliance Tests
    //           (section-1.13 items 1, 2, 4, 5, 6, 7)
    // =========================================================================

    [Fact]
    public void Section113_Cat1_LearningDocument_Topics_IncludeAllMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "general-installation-section113-cat1.md",
            RelativePath = "Knowledge/general-installation-section113-cat1.md",
            Summary =
                "Section 1.13 category 1: general electrical installation mandatory tests. "
                + "Covers earthing continuity, insulation resistance, polarity, functional tests.",
            Topics =
            [
                "general installation",
                "earthing continuity",
                "insulation resistance",
                "polarity",
                "functional tests",
            ],
        };

        Assert.Contains("earthing continuity", document.Topics);
        Assert.Contains("insulation resistance", document.Topics);
        Assert.Contains("polarity", document.Topics);
        Assert.Contains("functional tests", document.Topics);
    }

    [Fact]
    public void Section113_Cat2_LearningDocument_Topics_IncludeAllMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "cables-conduit-section113-cat2.md",
            RelativePath = "Knowledge/cables-conduit-section113-cat2.md",
            Summary =
                "Section 1.13 category 2: cables and conduit mandatory tests. "
                + "Covers installation inspection, cable pulling records, megger test results.",
            Topics =
            [
                "cables",
                "conduit",
                "installation inspection",
                "cable pulling records",
                "megger test results",
            ],
        };

        Assert.Contains("installation inspection", document.Topics);
        Assert.Contains("cable pulling records", document.Topics);
        Assert.Contains("megger test results", document.Topics);
    }

    [Fact]
    public void Section113_Cat4_LearningDocument_Topics_IncludeAllMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "motors-drives-section113-cat4.md",
            RelativePath = "Knowledge/motors-drives-section113-cat4.md",
            Summary =
                "Section 1.13 category 4: motors and drives mandatory tests. "
                + "Covers rotation checks, no-load current, full-load current, thermal overload settings.",
            Topics =
            [
                "motors",
                "drives",
                "rotation checks",
                "no-load current",
                "full-load current",
                "thermal overload settings",
            ],
        };

        Assert.Contains("rotation checks", document.Topics);
        Assert.Contains("no-load current", document.Topics);
        Assert.Contains("full-load current", document.Topics);
        Assert.Contains("thermal overload settings", document.Topics);
    }

    [Fact]
    public void Section113_Cat5_LearningDocument_Topics_IncludeAllMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "lighting-small-power-section113-cat5.md",
            RelativePath = "Knowledge/lighting-small-power-section113-cat5.md",
            Summary =
                "Section 1.13 category 5: lighting and small power mandatory tests. "
                + "Covers circuit continuity, RCD trip-time testing, lux level verification.",
            Topics =
            [
                "lighting",
                "small power",
                "circuit continuity",
                "RCD trip-time testing",
                "lux level verification",
            ],
        };

        Assert.Contains("circuit continuity", document.Topics);
        Assert.Contains("RCD trip-time testing", document.Topics);
        Assert.Contains("lux level verification", document.Topics);
    }

    [Fact]
    public void Section113_Cat6_LearningDocument_Topics_IncludeAllMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "instrumentation-control-section113-cat6.md",
            RelativePath = "Knowledge/instrumentation-control-section113-cat6.md",
            Summary =
                "Section 1.13 category 6: instrumentation and control wiring mandatory tests. "
                + "Covers loop checks, signal calibration records, PLC I/O verification.",
            Topics =
            [
                "instrumentation",
                "control wiring",
                "loop checks",
                "signal calibration records",
                "PLC I/O verification",
            ],
        };

        Assert.Contains("loop checks", document.Topics);
        Assert.Contains("signal calibration records", document.Topics);
        Assert.Contains("PLC I/O verification", document.Topics);
    }

    [Fact]
    public void Section113_Cat7_LearningDocument_Topics_IncludeAllMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "earthing-bonding-section113-cat7.md",
            RelativePath = "Knowledge/earthing-bonding-section113-cat7.md",
            Summary =
                "Section 1.13 category 7: earthing and bonding systems mandatory tests. "
                + "Covers earth resistance measurements and bonding continuity records.",
            Topics =
            [
                "earthing",
                "bonding",
                "earth resistance measurements",
                "bonding continuity records",
            ],
        };

        Assert.Contains("earth resistance measurements", document.Topics);
        Assert.Contains("bonding continuity records", document.Topics);
    }

    [Fact]
    public void Section113_FullChecklist_AllSevenCategories_PresentInLearningLibrary()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "qa-templates-section113-full.md",
                    RelativePath = "Knowledge/qa-templates-section113-full.md",
                    Summary =
                        "Section 1.13 all mandatory QA/QC categories: general installation, "
                        + "cables and conduit, switchboards, motors and drives, "
                        + "lighting and small power, instrumentation and control wiring, "
                        + "earthing and bonding systems.",
                    Topics =
                    [
                        "general installation",
                        "cables",
                        "conduit",
                        "switchboard",
                        "distribution centre",
                        "control centre",
                        "motor",
                        "drive",
                        "lighting",
                        "small power",
                        "instrumentation",
                        "control wiring",
                        "earthing",
                        "bonding",
                    ],
                },
            ],
        };

        var document = library.Documents.FirstOrDefault(
            d => d.FileName == "qa-templates-section113-full.md"
        );

        Assert.NotNull(document);
        Assert.Contains("general installation", document!.Topics);
        Assert.Contains("cables", document.Topics);
        Assert.Contains("switchboard", document.Topics);
        Assert.Contains("motor", document.Topics);
        Assert.Contains("lighting", document.Topics);
        Assert.Contains("instrumentation", document.Topics);
        Assert.Contains("earthing", document.Topics);
        Assert.Contains("bonding", document.Topics);
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
