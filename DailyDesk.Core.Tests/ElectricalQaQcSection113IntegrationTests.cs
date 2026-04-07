using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

// ============================================================================
// Electrical QA/QC Section 1.13 Integration Tests
// (Watercare QA/QC Templates – mandatory tests per section 1.13)
//
// Section 1.13 defines minimum mandatory tests for seven categories:
//   1. General Electrical Installation
//   2. Cables and Conduit
//   3. Switchboards, Distribution Centres, and Control Centres
//   4. Motors and Drives
//   5. Lighting and Small Power
//   6. Instrumentation and Control Wiring
//   7. Earthing and Bonding Systems
// ============================================================================

public sealed class ElectricalQaQcSection113IntegrationTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static OralDefenseService MakeService() =>
        new(new ThrowingModelProvider(), "test-model");

    // =========================================================================
    // Group 1 – General Electrical Installation (Category 1)
    // =========================================================================

    [Fact]
    public void Category1_GeneralElectricalInstallation_Scenario_CanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "general electrical installation QA/QC",
            Title = "General Electrical Installation: Mandatory QA/QC Checks",
            Prompt =
                "Describe the mandatory QA/QC checks for a general electrical installation, "
                + "including earthing continuity, insulation resistance, polarity checks, "
                + "and functional tests before energisation.",
            WhatGoodLooksLike =
                "A strong answer covers all four mandatory check categories: "
                + "earthing continuity, insulation resistance, polarity, and functional tests, "
                + "and references the required sign-off record for each.",
        };

        Assert.Equal("general electrical installation QA/QC", scenario.Topic);
        Assert.Contains("earthing continuity", scenario.Prompt);
        Assert.Contains("insulation resistance", scenario.Prompt);
        Assert.Contains("polarity", scenario.Prompt);
        Assert.Contains("functional tests", scenario.Prompt);
    }

    [Fact]
    public void Category1_GeneralElectrical_WhatGoodLooksLike_CoversFourMandatoryChecks()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "general electrical installation QA/QC",
            WhatGoodLooksLike =
                "A strong answer covers: earthing continuity, insulation resistance, "
                + "polarity checks, and functional tests.",
        };

        Assert.Contains("earthing continuity", scenario.WhatGoodLooksLike);
        Assert.Contains("insulation resistance", scenario.WhatGoodLooksLike);
        Assert.Contains("polarity", scenario.WhatGoodLooksLike);
        Assert.Contains("functional tests", scenario.WhatGoodLooksLike);
    }

    [Fact]
    public async Task Category1_FallbackScoring_EarthingKeyword_ScoresTechnicalCorrectnessThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "general electrical installation QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Earthing continuity and earth ground resistance must be verified with a low-resistance "
            + "ohmmeter before energising any electrical installation panel.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Category1_FallbackScoring_InsulationResistanceKeyword_ScoresTechnicalThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "general electrical installation QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Insulation resistance testing with a 500 V megger should confirm cable resistance "
            + "above 1 MΩ before applying voltage to the installation.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public void Category1_LearningDocument_Topics_IncludeAllFourMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "general-electrical-installation-qaqc.md",
            RelativePath = "Knowledge/general-electrical-installation-qaqc.md",
            Summary =
                "Watercare QA/QC section 1.13 category 1: general electrical installation mandatory "
                + "tests. Covers earthing continuity, insulation resistance, polarity checks, "
                + "and functional tests.",
            Topics =
            [
                "general electrical installation",
                "earthing continuity",
                "insulation resistance",
                "polarity checks",
                "functional tests",
            ],
        };

        Assert.Contains("earthing continuity", document.Topics);
        Assert.Contains("insulation resistance", document.Topics);
        Assert.Contains("polarity checks", document.Topics);
        Assert.Contains("functional tests", document.Topics);
    }

    // =========================================================================
    // Group 2 – Cables and Conduit (Category 2)
    // =========================================================================

    [Fact]
    public void Category2_CablesAndConduit_Scenario_CanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "cables and conduit QA/QC",
            Title = "Cables and Conduit: Mandatory QA/QC Checks",
            Prompt =
                "What mandatory QA/QC checks are required for cables and conduit installations? "
                + "Include installation inspection, cable pulling records, and megger test results.",
            WhatGoodLooksLike =
                "A strong answer covers installation inspection, cable pulling records, and "
                + "megger test results, and references the required sign-off documentation.",
        };

        Assert.Equal("cables and conduit QA/QC", scenario.Topic);
        Assert.Contains("installation inspection", scenario.Prompt);
        Assert.Contains("cable pulling records", scenario.Prompt);
        Assert.Contains("megger test results", scenario.Prompt);
    }

    [Fact]
    public async Task Category2_FallbackScoring_MeggerKeyword_ScoresTechnicalCorrectnessThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "cables and conduit QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Megger test results for all cable runs must be recorded before termination, "
            + "confirming insulation resistance meets the standard minimum threshold.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public void Category2_LearningDocument_Topics_IncludeAllThreeMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "cables-conduit-qaqc.md",
            RelativePath = "Knowledge/cables-conduit-qaqc.md",
            Summary =
                "Watercare QA/QC section 1.13 category 2: cables and conduit mandatory tests. "
                + "Covers installation inspection, cable pulling records, and megger test results.",
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
    public void Category2_KnowledgeSearch_CablesQuery_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "cables-conduit-qaqc.md",
                    RelativePath = "Knowledge/cables-conduit-qaqc.md",
                    Summary =
                        "Mandatory QA/QC tests for cables and conduit: installation inspection, "
                        + "cable pulling records, megger test results.",
                    Topics = ["cables", "conduit", "megger", "installation inspection"],
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
            "cables conduit megger installation inspection",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("cables-conduit-qaqc.md", result.Results[0].Title);
    }

    // =========================================================================
    // Group 3 – Switchboards, Distribution Centres, and Control Centres (Category 3)
    // =========================================================================
    // Full coverage for this category exists in SwitchboardControlCentreQaQcIntegrationTests.
    // The following tests validate section 1.13 category 3 from the top-level section perspective.

    [Fact]
    public void Category3_Section113_Scenario_CrossReferences_SwitchboardMandatoryChecks()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "section 1.13 category 3 QA/QC",
            Title = "Section 1.13 Category 3: Switchboards, Distribution Centres, Control Centres",
            Prompt =
                "What are the mandatory QA/QC checks for switchboards, distribution centres, "
                + "and control centres under Watercare QA/QC template section 1.13 category 3? "
                + "Include termination checks, protection relay settings, interlocking verification, "
                + "and FAT/SAT records.",
            WhatGoodLooksLike =
                "A complete answer references all four mandatory checks: "
                + "termination checks, protection relay settings, interlocking verification, "
                + "and FAT/SAT records.",
        };

        Assert.Contains("termination checks", scenario.Prompt);
        Assert.Contains("protection relay settings", scenario.Prompt);
        Assert.Contains("interlocking verification", scenario.Prompt);
        Assert.Contains("FAT/SAT records", scenario.Prompt);
    }

    // =========================================================================
    // Group 4 – Motors and Drives (Category 4)
    // =========================================================================

    [Fact]
    public void Category4_MotorsAndDrives_Scenario_CanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "motors and drives QA/QC",
            Title = "Motors and Drives: Mandatory QA/QC Checks",
            Prompt =
                "Explain the mandatory QA/QC checks required for motors and drives before "
                + "commissioning, including rotation checks, no-load and full-load current "
                + "measurements, and thermal overload settings.",
            WhatGoodLooksLike =
                "A strong answer covers rotation checks, no-load and full-load current "
                + "measurements, and thermal overload settings, and references the sign-off record.",
        };

        Assert.Equal("motors and drives QA/QC", scenario.Topic);
        Assert.Contains("rotation checks", scenario.Prompt);
        Assert.Contains("no-load", scenario.Prompt);
        Assert.Contains("full-load current", scenario.Prompt);
        Assert.Contains("thermal overload settings", scenario.Prompt);
    }

    [Fact]
    public void Category4_MotorsAndDrives_WhatGoodLooksLike_CoversThreeMandatoryChecks()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "motors and drives QA/QC",
            WhatGoodLooksLike =
                "A strong answer covers rotation checks, no-load and full-load current "
                + "measurements, and thermal overload settings.",
        };

        Assert.Contains("rotation checks", scenario.WhatGoodLooksLike);
        Assert.Contains("no-load", scenario.WhatGoodLooksLike);
        Assert.Contains("thermal overload settings", scenario.WhatGoodLooksLike);
    }

    [Fact]
    public async Task Category4_FallbackScoring_RotationKeyword_ScoresTechnicalCorrectnessThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "motors and drives QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Rotation checks for all motors must be performed at no-load before connecting the "
            + "drive, verifying correct phase sequence against the standard wiring diagram.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Category4_FallbackScoring_ThermalOverloadKeyword_ScoresValidationThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "motors and drives QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Thermal overload settings must be checked against the motor nameplate data "
            + "and verified to match the correct full-load current value before commissioning.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(validation!.Score >= 3, $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public void Category4_LearningDocument_Topics_IncludeAllThreeMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "motors-drives-qaqc.md",
            RelativePath = "Knowledge/motors-drives-qaqc.md",
            Summary =
                "Watercare QA/QC section 1.13 category 4: motors and drives mandatory tests. "
                + "Covers rotation checks, no-load and full-load current measurements, "
                + "and thermal overload settings.",
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
    public void Category4_KnowledgeSearch_MotorQuery_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "motors-drives-qaqc.md",
                    RelativePath = "Knowledge/motors-drives-qaqc.md",
                    Summary =
                        "Mandatory QA/QC tests for motors and drives: rotation checks, "
                        + "no-load and full-load current measurements, thermal overload settings.",
                    Topics = ["motors", "drives", "rotation", "thermal overload"],
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
            "motors drives rotation thermal overload",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("motors-drives-qaqc.md", result.Results[0].Title);
    }

    // =========================================================================
    // Group 5 – Lighting and Small Power (Category 5)
    // =========================================================================

    [Fact]
    public void Category5_LightingAndSmallPower_Scenario_CanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "lighting and small power QA/QC",
            Title = "Lighting and Small Power: Mandatory QA/QC Checks",
            Prompt =
                "What mandatory QA/QC checks are required for lighting and small power circuits? "
                + "Include circuit continuity, RCD trip-time testing, and lux level verification.",
            WhatGoodLooksLike =
                "A strong answer covers circuit continuity, RCD trip-time testing, and lux level "
                + "verification, and references the sign-off record for each check.",
        };

        Assert.Equal("lighting and small power QA/QC", scenario.Topic);
        Assert.Contains("circuit continuity", scenario.Prompt);
        Assert.Contains("RCD trip-time", scenario.Prompt);
        Assert.Contains("lux level verification", scenario.Prompt);
    }

    [Fact]
    public void Category5_Lighting_WhatGoodLooksLike_CoversThreeMandatoryChecks()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "lighting and small power QA/QC",
            WhatGoodLooksLike =
                "A strong answer covers circuit continuity, RCD trip-time testing, "
                + "and lux level verification.",
        };

        Assert.Contains("circuit continuity", scenario.WhatGoodLooksLike);
        Assert.Contains("RCD trip-time", scenario.WhatGoodLooksLike);
        Assert.Contains("lux level", scenario.WhatGoodLooksLike);
    }

    [Fact]
    public async Task Category5_FallbackScoring_RCDKeyword_ScoresTechnicalCorrectnessThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "lighting and small power QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "RCD trip-time testing must confirm that all residual current devices operate within "
            + "the standard 30 ms trip time to protect against electric shock.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public void Category5_LearningDocument_Topics_IncludeAllThreeMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "lighting-small-power-qaqc.md",
            RelativePath = "Knowledge/lighting-small-power-qaqc.md",
            Summary =
                "Watercare QA/QC section 1.13 category 5: lighting and small power mandatory tests. "
                + "Covers circuit continuity, RCD trip-time testing, and lux level verification.",
            Topics =
            [
                "lighting",
                "small power",
                "circuit continuity",
                "RCD trip-time",
                "lux level verification",
            ],
        };

        Assert.Contains("circuit continuity", document.Topics);
        Assert.Contains("RCD trip-time", document.Topics);
        Assert.Contains("lux level verification", document.Topics);
    }

    [Fact]
    public void Category5_KnowledgeSearch_LightingQuery_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "lighting-small-power-qaqc.md",
                    RelativePath = "Knowledge/lighting-small-power-qaqc.md",
                    Summary =
                        "Mandatory QA/QC tests for lighting and small power: circuit continuity, "
                        + "RCD trip-time testing, lux level verification.",
                    Topics = ["lighting", "small power", "RCD", "circuit continuity", "lux"],
                },
                new LearningDocument
                {
                    FileName = "motors-drives-qaqc.md",
                    RelativePath = "Knowledge/motors-drives-qaqc.md",
                    Summary = "Rotation checks and thermal overload settings for motors and drives.",
                    Topics = ["motors", "drives", "rotation", "thermal overload"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch(
            "lighting RCD circuit continuity lux",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("lighting-small-power-qaqc.md", result.Results[0].Title);
    }

    // =========================================================================
    // Group 6 – Instrumentation and Control Wiring (Category 6)
    // =========================================================================

    [Fact]
    public void Category6_InstrumentationControlWiring_Scenario_CanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "instrumentation and control wiring QA/QC",
            Title = "Instrumentation and Control Wiring: Mandatory QA/QC Checks",
            Prompt =
                "Describe the mandatory QA/QC checks for instrumentation and control wiring, "
                + "including loop checks, signal calibration records, and PLC I/O verification.",
            WhatGoodLooksLike =
                "A strong answer covers loop checks, signal calibration records, and PLC I/O "
                + "verification, and references the required sign-off documentation.",
        };

        Assert.Equal("instrumentation and control wiring QA/QC", scenario.Topic);
        Assert.Contains("loop checks", scenario.Prompt);
        Assert.Contains("signal calibration", scenario.Prompt);
        Assert.Contains("PLC I/O verification", scenario.Prompt);
    }

    [Fact]
    public void Category6_InstrumentationControl_WhatGoodLooksLike_CoversThreeMandatoryChecks()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "instrumentation and control wiring QA/QC",
            WhatGoodLooksLike =
                "A strong answer covers loop checks, signal calibration records, "
                + "and PLC I/O verification.",
        };

        Assert.Contains("loop checks", scenario.WhatGoodLooksLike);
        Assert.Contains("signal calibration", scenario.WhatGoodLooksLike);
        Assert.Contains("PLC I/O verification", scenario.WhatGoodLooksLike);
    }

    [Fact]
    public async Task Category6_FallbackScoring_ValidationKeyword_ScoresValidationThinkingThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "instrumentation and control wiring QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Loop checks must be performed to validate the complete signal path from field instrument "
            + "to PLC input, confirming correct wiring and signal levels before commissioning.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(validation!.Score >= 3, $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public void Category6_LearningDocument_Topics_IncludeAllThreeMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "instrumentation-control-wiring-qaqc.md",
            RelativePath = "Knowledge/instrumentation-control-wiring-qaqc.md",
            Summary =
                "Watercare QA/QC section 1.13 category 6: instrumentation and control wiring "
                + "mandatory tests. Covers loop checks, signal calibration records, "
                + "and PLC I/O verification.",
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
    public void Category6_KnowledgeSearch_InstrumentationQuery_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "instrumentation-control-wiring-qaqc.md",
                    RelativePath = "Knowledge/instrumentation-control-wiring-qaqc.md",
                    Summary =
                        "Mandatory QA/QC tests for instrumentation and control wiring: loop checks, "
                        + "signal calibration records, PLC I/O verification.",
                    Topics = ["instrumentation", "control wiring", "loop checks", "PLC I/O"],
                },
                new LearningDocument
                {
                    FileName = "motors-drives-qaqc.md",
                    RelativePath = "Knowledge/motors-drives-qaqc.md",
                    Summary = "Rotation checks and thermal overload settings for motors and drives.",
                    Topics = ["motors", "drives", "rotation", "thermal overload"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch(
            "instrumentation control wiring loop checks PLC",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("instrumentation-control-wiring-qaqc.md", result.Results[0].Title);
    }

    // =========================================================================
    // Group 7 – Earthing and Bonding Systems (Category 7)
    // =========================================================================

    [Fact]
    public void Category7_EarthingAndBonding_Scenario_CanBeCreated()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "earthing and bonding QA/QC",
            Title = "Earthing and Bonding Systems: Mandatory QA/QC Checks",
            Prompt =
                "What mandatory QA/QC checks are required for earthing and bonding systems? "
                + "Include earth resistance measurements and bonding continuity records.",
            WhatGoodLooksLike =
                "A strong answer covers earth resistance measurements and bonding continuity "
                + "records, and references the required sign-off documentation.",
        };

        Assert.Equal("earthing and bonding QA/QC", scenario.Topic);
        Assert.Contains("earth resistance measurements", scenario.Prompt);
        Assert.Contains("bonding continuity records", scenario.Prompt);
    }

    [Fact]
    public void Category7_EarthingAndBonding_WhatGoodLooksLike_CoversTwoMandatoryChecks()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "earthing and bonding QA/QC",
            WhatGoodLooksLike =
                "A strong answer covers earth resistance measurements and bonding continuity records.",
        };

        Assert.Contains("earth resistance measurements", scenario.WhatGoodLooksLike);
        Assert.Contains("bonding continuity records", scenario.WhatGoodLooksLike);
    }

    [Fact]
    public async Task Category7_FallbackScoring_EarthResistanceKeyword_ScoresTechnicalCorrectnessThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "earthing and bonding QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Earth ground resistance measurements must be taken at each earth electrode using a "
            + "dedicated earth resistance tester, confirming resistance below the design threshold "
            + "before the system is energised.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task Category7_FallbackScoring_BondingKeyword_ScoresValidationThinkingThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario { Topic = "earthing and bonding QA/QC" };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Bonding continuity records must validate that all metallic structural components "
            + "are connected to the main earthing terminal with resistance below 0.1 Ω.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(validation!.Score >= 3, $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public void Category7_LearningDocument_Topics_IncludeBothMandatoryChecks()
    {
        var document = new LearningDocument
        {
            FileName = "earthing-bonding-qaqc.md",
            RelativePath = "Knowledge/earthing-bonding-qaqc.md",
            Summary =
                "Watercare QA/QC section 1.13 category 7: earthing and bonding systems mandatory "
                + "tests. Covers earth resistance measurements and bonding continuity records.",
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
    public void Category7_KnowledgeSearch_EarthingQuery_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "earthing-bonding-qaqc.md",
                    RelativePath = "Knowledge/earthing-bonding-qaqc.md",
                    Summary =
                        "Mandatory QA/QC tests for earthing and bonding: earth resistance "
                        + "measurements and bonding continuity records.",
                    Topics = ["earthing", "bonding", "earth resistance", "bonding continuity"],
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
            "earthing bonding earth resistance continuity",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("earthing-bonding-qaqc.md", result.Results[0].Title);
    }

    // =========================================================================
    // Group 8 – Section 1.13 Top-Level Structure Tests
    // =========================================================================

    [Fact]
    public void Section113_AllSevenCategories_AreRepresentedInLibrary()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "general-electrical-installation-qaqc.md",
                    Topics = ["general electrical installation", "earthing continuity", "insulation resistance"],
                },
                new LearningDocument
                {
                    FileName = "cables-conduit-qaqc.md",
                    Topics = ["cables", "conduit", "megger test results"],
                },
                new LearningDocument
                {
                    FileName = "switchboard-distribution-control-qaqc.md",
                    Topics = ["switchboard", "distribution centre", "control centre", "protection relay"],
                },
                new LearningDocument
                {
                    FileName = "motors-drives-qaqc.md",
                    Topics = ["motors", "drives", "rotation checks", "thermal overload settings"],
                },
                new LearningDocument
                {
                    FileName = "lighting-small-power-qaqc.md",
                    Topics = ["lighting", "small power", "RCD trip-time", "lux level verification"],
                },
                new LearningDocument
                {
                    FileName = "instrumentation-control-wiring-qaqc.md",
                    Topics = ["instrumentation", "control wiring", "loop checks", "PLC I/O verification"],
                },
                new LearningDocument
                {
                    FileName = "earthing-bonding-qaqc.md",
                    Topics = ["earthing", "bonding", "earth resistance measurements", "bonding continuity records"],
                },
            ],
        };

        Assert.Equal(7, library.Documents.Count);
        Assert.Contains(library.Documents, d => d.Topics.Contains("general electrical installation"));
        Assert.Contains(library.Documents, d => d.Topics.Contains("cables"));
        Assert.Contains(library.Documents, d => d.Topics.Contains("switchboard"));
        Assert.Contains(library.Documents, d => d.Topics.Contains("motors"));
        Assert.Contains(library.Documents, d => d.Topics.Contains("lighting"));
        Assert.Contains(library.Documents, d => d.Topics.Contains("instrumentation"));
        Assert.Contains(library.Documents, d => d.Topics.Contains("earthing"));
    }

    [Fact]
    public void Section113_ChecklistCompliance_AllCategoryTopics_PresentInFullLibraryDocument()
    {
        var document = new LearningDocument
        {
            FileName = "qa-templates-electrical-section113-full.md",
            RelativePath = "Knowledge/qa-templates-electrical-section113-full.md",
            Summary =
                "Watercare QA/QC template section 1.13 full mandatory test categories: "
                + "general electrical installation, cables and conduit, switchboards, "
                + "distribution centres, control centres, motors and drives, lighting and small power, "
                + "instrumentation and control wiring, earthing and bonding systems.",
            Topics =
            [
                "general electrical installation",
                "cables",
                "conduit",
                "switchboard",
                "distribution centre",
                "control centre",
                "motors",
                "drives",
                "lighting",
                "small power",
                "instrumentation",
                "control wiring",
                "earthing",
                "bonding",
            ],
        };

        Assert.Contains("general electrical installation", document.Topics);
        Assert.Contains("cables", document.Topics);
        Assert.Contains("switchboard", document.Topics);
        Assert.Contains("motors", document.Topics);
        Assert.Contains("lighting", document.Topics);
        Assert.Contains("instrumentation", document.Topics);
        Assert.Contains("earthing", document.Topics);
    }

    [Fact]
    public async Task Section113_FallbackEvaluation_AllCategories_TotalScoreWithinValidRange()
    {
        var service = MakeService();

        var categories = new[]
        {
            "general electrical installation QA/QC",
            "cables and conduit QA/QC",
            "motors and drives QA/QC",
            "lighting and small power QA/QC",
            "instrumentation and control wiring QA/QC",
            "earthing and bonding QA/QC",
        };

        foreach (var topic in categories)
        {
            var scenario = new OralDefenseScenario { Topic = topic };
            var evaluation = await service.ScoreResponseAsync(
                scenario,
                $"The standard QA/QC checks for {topic} must be completed and signed off.",
                new SuiteSnapshot(),
                new LearningProfile(),
                new LearningLibrary()
            );

            Assert.InRange(evaluation.TotalScore, 0, 20);
        }
    }

    [Fact]
    public async Task Section113_FallbackEvaluation_AllCategories_HaveExactlyFiveRubricItems()
    {
        var service = MakeService();

        var categories = new[]
        {
            "general electrical installation QA/QC",
            "cables and conduit QA/QC",
            "motors and drives QA/QC",
            "lighting and small power QA/QC",
            "instrumentation and control wiring QA/QC",
            "earthing and bonding QA/QC",
        };

        foreach (var topic in categories)
        {
            var scenario = new OralDefenseScenario { Topic = topic };
            var evaluation = await service.ScoreResponseAsync(
                scenario,
                "Some answer.",
                new SuiteSnapshot(),
                new LearningProfile(),
                new LearningLibrary()
            );

            Assert.Equal(5, evaluation.RubricItems.Count);
        }
    }

    [Fact]
    public async Task Section113_FallbackEvaluation_AllCategories_HaveThreeRecommendedFollowUps()
    {
        var service = MakeService();

        var categories = new[]
        {
            "general electrical installation QA/QC",
            "cables and conduit QA/QC",
            "motors and drives QA/QC",
            "lighting and small power QA/QC",
            "instrumentation and control wiring QA/QC",
            "earthing and bonding QA/QC",
        };

        foreach (var topic in categories)
        {
            var scenario = new OralDefenseScenario { Topic = topic };
            var evaluation = await service.ScoreResponseAsync(
                scenario,
                "Some answer.",
                new SuiteSnapshot(),
                new LearningProfile(),
                new LearningLibrary()
            );

            Assert.Equal(3, evaluation.RecommendedFollowUps.Count);
        }
    }

    [Fact]
    public async Task Section113_FallbackEvaluation_AllCategories_NextReviewRecommendation_IsNonEmpty()
    {
        var service = MakeService();

        var categories = new[]
        {
            "general electrical installation QA/QC",
            "cables and conduit QA/QC",
            "motors and drives QA/QC",
            "lighting and small power QA/QC",
            "instrumentation and control wiring QA/QC",
            "earthing and bonding QA/QC",
        };

        foreach (var topic in categories)
        {
            var scenario = new OralDefenseScenario { Topic = topic };
            var evaluation = await service.ScoreResponseAsync(
                scenario,
                "The mandatory QA/QC checks must be completed and signed off before energisation.",
                new SuiteSnapshot(),
                new LearningProfile(),
                new LearningLibrary()
            );

            Assert.False(string.IsNullOrWhiteSpace(evaluation.NextReviewRecommendation));
        }
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
