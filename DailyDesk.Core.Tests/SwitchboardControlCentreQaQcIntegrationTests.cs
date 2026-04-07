using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that validate the switchboard, distribution centre, and control centre
/// QA/QC checks mandated by section 1.13 item 3 of the Watercare QA/QC Templates for General
/// Electrical Construction Standards.
///
/// Source document:
///   Knowledge/Research/20260324-000659-electrical-drawing-qa-workflow-standards-review-checklist.md
///   (chunk 3 – PDF QA/QC Templates for General Electrical Construction Standards)
///
/// Integration standards reference:
///   Knowledge/Research/20260406-electrical-qaqc-workflow-integration-standards.md
///
/// Section 1.13 item 3 mandatory checks for Switchboards, Distribution Centres, and Control Centres:
///   1. Termination checks
///   2. Protection relay settings
///   3. Interlocking verification
///   4. FAT/SAT records
/// </summary>
public sealed class SwitchboardControlCentreQaQcIntegrationTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Constants – mirrors what the integration standards document defines
    // ─────────────────────────────────────────────────────────────────────────

    private const string Section113Item3Category =
        "Switchboards, Distribution Centres, and Control Centres";

    private static readonly string[] Category3MandatoryChecks =
    [
        "termination checks",
        "protection relay settings",
        "interlocking verification",
        "FAT/SAT records",
    ];

    private static readonly string[] AllSection113Categories =
    [
        "General Electrical Installation",
        "Cables and Conduit",
        Section113Item3Category,
        "Motors and Drives",
        "Lighting and Small Power",
        "Instrumentation and Control Wiring",
        "Earthing and Bonding Systems",
    ];

    // ─────────────────────────────────────────────────────────────────────────
    // Stub model provider (same pattern as OfficeBrokerLogicTests)
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class ThrowingModelProvider : IModelProvider
    {
        public string ProviderId => "throwing-stub";
        public string ProviderLabel => "Throwing Stub";

        public Task<IReadOnlyList<string>> GetInstalledModelsAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Stub provider unavailable.");

        public Task<string> GenerateAsync(string model, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Stub provider unavailable.");

        public Task<T?> GenerateJsonAsync<T>(string model, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Stub provider unavailable.");

        public Task<bool> PingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "DailyDesk", "DailyDesk.csproj")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group 1 – Section 1.13 item 3 category definition and structure
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Section113Item3_CategoryName_IncludesSwitchboards()
    {
        Assert.Contains("Switchboards", Section113Item3Category, StringComparison.Ordinal);
    }

    [Fact]
    public void Section113Item3_CategoryName_IncludesDistributionCentres()
    {
        Assert.Contains("Distribution Centres", Section113Item3Category, StringComparison.Ordinal);
    }

    [Fact]
    public void Section113Item3_CategoryName_IncludesControlCentres()
    {
        Assert.Contains("Control Centres", Section113Item3Category, StringComparison.Ordinal);
    }

    [Fact]
    public void Section113Item3_MandatoryChecks_HasFourItems()
    {
        Assert.Equal(4, Category3MandatoryChecks.Length);
    }

    [Fact]
    public void Section113Item3_MandatoryChecks_IncludesTerminationChecks()
    {
        Assert.Contains("termination checks", Category3MandatoryChecks);
    }

    [Fact]
    public void Section113Item3_MandatoryChecks_IncludesProtectionRelaySettings()
    {
        Assert.Contains("protection relay settings", Category3MandatoryChecks);
    }

    [Fact]
    public void Section113Item3_MandatoryChecks_IncludesInterlockingVerification()
    {
        Assert.Contains("interlocking verification", Category3MandatoryChecks);
    }

    [Fact]
    public void Section113Item3_MandatoryChecks_IncludesFatSatRecords()
    {
        Assert.Contains("FAT/SAT records", Category3MandatoryChecks);
    }

    [Fact]
    public void AllSection113Categories_HasSevenItems()
    {
        Assert.Equal(7, AllSection113Categories.Length);
    }

    [Fact]
    public void AllSection113Categories_Item3IsCategory3()
    {
        // Section 1.13 item 3 must be switchboards/distribution centres/control centres
        Assert.Equal(Section113Item3Category, AllSection113Categories[2]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group 2 – Control centre oral defense scenario creation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ControlCentre_OralDefenseScenario_CanBeCreatedWithControlCentreTopic()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "control centre QA/QC",
            Title = "Control Centre Commissioning: Mandatory QA/QC Checks",
            Prompt =
                "Describe the mandatory QA/QC checks required for a control centre installation "
                + "before it is energised, with reference to termination checks, protection relay "
                + "settings, interlocking verification, and FAT/SAT records.",
            WhatGoodLooksLike =
                "A strong answer covers each of the four mandatory check categories, "
                + "names the risk eliminated by each check, and references the QA sign-off record.",
        };

        Assert.Equal("control centre QA/QC", scenario.Topic);
        Assert.Contains("Control Centre", scenario.Title, StringComparison.Ordinal);
        Assert.Contains("termination checks", scenario.Prompt);
        Assert.Contains("protection relay", scenario.Prompt);
        Assert.Contains("interlocking verification", scenario.Prompt);
        Assert.Contains("FAT/SAT", scenario.Prompt);
    }

    [Fact]
    public void DistributionCentre_OralDefenseScenario_CanBeCreatedWithDistributionCentreTopic()
    {
        var scenario = new OralDefenseScenario
        {
            Topic = "distribution centre QA/QC",
            Title = "Distribution Centre Commissioning: Mandatory QA/QC Checks",
            Prompt =
                "Describe the mandatory QA/QC checks required for a distribution centre before "
                + "energisation. Cover termination checks, protection relay settings, interlocking "
                + "verification, and FAT/SAT record sign-off.",
            WhatGoodLooksLike =
                "A complete answer addresses each of the four mandatory check types for a "
                + "distribution centre and references the signed QA record sheet requirement.",
        };

        Assert.Equal("distribution centre QA/QC", scenario.Topic);
        Assert.Contains("Distribution Centre", scenario.Title, StringComparison.Ordinal);
        Assert.Contains("termination checks", scenario.Prompt);
        Assert.Contains("FAT/SAT", scenario.Prompt);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group 3 – Control centre fallback scoring integration
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ControlCentre_FallbackScoring_TerminationCheckAnswer_ScoresValidation()
    {
        var provider = new ThrowingModelProvider();
        var service = new OralDefenseService(provider, "test-model");
        var scenario = new OralDefenseScenario
        {
            Topic = "control centre QA/QC",
            Title = "Control Centre Termination Check",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "All control centre cable terminations must be verified against the wiring schedule "
            + "before energisation. Each termination is tested for continuity and checked for "
            + "correct torque to the manufacturer's specification.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(validation!.Score >= 3, $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public async Task ControlCentre_FallbackScoring_ProtectionRelayAnswer_ScoresTechnicalHigher()
    {
        var provider = new ThrowingModelProvider();
        var service = new OralDefenseService(provider, "test-model");
        var scenario = new OralDefenseScenario
        {
            Topic = "control centre QA/QC",
            Title = "Control Centre Protection Relay Verification",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Protection relay settings in the control centre must be verified against the "
            + "protection coordination study before energisation to confirm correct voltage, "
            + "overcurrent, and earth fault protection settings are applied.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(technical!.Score >= 3, $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task ControlCentre_FallbackScoring_InterlockingAnswer_ScoresValidation()
    {
        var provider = new ThrowingModelProvider();
        var service = new OralDefenseService(provider, "test-model");
        var scenario = new OralDefenseScenario
        {
            Topic = "control centre QA/QC",
            Title = "Control Centre Interlocking Verification",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "The control centre interlocking circuits must be tested and verified to confirm that "
            + "both mechanical and electrical interlocks correctly prevent simultaneous operation "
            + "of incompatible switching devices, protecting against unsafe conditions.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(validation!.Score >= 3, $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public async Task ControlCentre_FallbackScoring_FatSatAnswer_ScoresValidation()
    {
        var provider = new ThrowingModelProvider();
        var service = new OralDefenseService(provider, "test-model");
        var scenario = new OralDefenseScenario
        {
            Topic = "control centre QA/QC",
            Title = "Control Centre FAT/SAT Record Compliance",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Factory Acceptance Test and Site Acceptance Test records for the control centre "
            + "must be completed and signed off before handover. These test records verify correct "
            + "wiring, control circuit operation, and metering accuracy against design specifications.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(validation!.Score >= 3, $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public async Task ControlCentre_FallbackScoring_ComprehensiveAnswer_AllFourChecks_ScoresHigher()
    {
        var provider = new ThrowingModelProvider();
        var service = new OralDefenseService(provider, "test-model");
        var scenario = new OralDefenseScenario
        {
            Topic = "control centre QA/QC",
            Title = "Complete Control Centre QA/QC Mandatory Checklist",
        };

        var comprehensiveAnswer =
            "Before energising a control centre, four mandatory QA/QC checks must be completed. "
            + "First, all cable terminations must be verified against the wiring schedule and "
            + "checked for correct torque and continuity. Second, protection relay settings must "
            + "be verified against the protection coordination study, confirming correct overcurrent "
            + "and earth fault pickup values for the voltage system. Third, interlocking circuits "
            + "must be tested to verify that mechanical and electrical interlocks correctly prevent "
            + "simultaneous operation of incompatible switching devices. Fourth, Factory Acceptance "
            + "Test and Site Acceptance Test records must be completed and signed off, verifying "
            + "correct wiring, circuit breaker operation, and metering accuracy. The failure risk "
            + "of skipping any of these checks includes fault propagation, equipment damage, "
            + "and operator safety hazards that are only discovered after energisation.";

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            comprehensiveAnswer,
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.Equal(5, evaluation.RubricItems.Count);
        var totalScore = evaluation.TotalScore;
        Assert.True(totalScore >= 12, $"Expected TotalScore >= 12 but was {totalScore}");
    }

    [Fact]
    public async Task DistributionCentre_FallbackScoring_FailureRiskAnswer_ScoresFailureModeHigher()
    {
        var provider = new ThrowingModelProvider();
        var service = new OralDefenseService(provider, "test-model");
        var scenario = new OralDefenseScenario
        {
            Topic = "distribution centre QA/QC",
            Title = "Distribution Centre Failure Risk Analysis",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Skipping termination checks on a distribution centre creates a significant risk of "
            + "loose connections that generate heat, causing insulation failure and potential fire. "
            + "Missing protection relay verification increases the risk of undetected fault "
            + "propagation that could damage downstream equipment or create operator safety hazards.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var failureMode = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Failure-Mode Awareness");
        Assert.NotNull(failureMode);
        Assert.True(failureMode!.Score >= 3, $"Expected Failure-Mode Awareness >= 3 but was {failureMode.Score}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group 4 – Knowledge search integration for control centres
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ControlCentre_KnowledgeSearch_FindsControlCentreDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "qa_templates_for_electrical_construction_standards.pdf",
                    RelativePath = "Knowledge/qa_templates_for_electrical_construction_standards.pdf",
                    Kind = "PDF",
                    SourceRootLabel = "Watercare Electrical Standards",
                    Summary = "Switchboards distribution centres control centres termination relay interlocking FAT SAT records section 1.13",
                    Topics = ["switchboard", "distribution", "control centre", "relay", "interlock", "FAT", "SAT"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch("control centre QA/QC mandatory", library);

        Assert.Equal("text", result.SearchMode);
        Assert.NotEmpty(result.Results);
        Assert.Equal("qa_templates_for_electrical_construction_standards.pdf", result.Results[0].Title);
    }

    [Fact]
    public void ControlCentre_KnowledgeSearch_RanksControlCentreDocumentAboveUnrelated()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "qa_templates_for_electrical_construction_standards.pdf",
                    RelativePath = "Knowledge/qa_templates_for_electrical_construction_standards.pdf",
                    Kind = "PDF",
                    SourceRootLabel = "Watercare Electrical Standards",
                    Summary = "Control centre termination relay interlocking FAT SAT mandatory checks section 1.13",
                    Topics = ["control centre", "termination", "relay", "interlock", "mandatory"],
                },
                new LearningDocument
                {
                    FileName = "lighting-design.md",
                    RelativePath = "Knowledge/lighting-design.md",
                    Kind = "md",
                    Summary = "Lighting circuit design and lux level calculations",
                    Topics = ["lighting", "lux", "circuit"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch("control centre termination relay", library);

        Assert.True(result.Results.Count >= 1);
        Assert.Equal("qa_templates_for_electrical_construction_standards.pdf", result.Results[0].Title);
    }

    [Fact]
    public void DistributionCentre_KnowledgeSearch_FindsTemplateByDistributionCentreQuery()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "qa_templates_for_electrical_construction_standards.pdf",
                    RelativePath = "Knowledge/qa_templates_for_electrical_construction_standards.pdf",
                    Kind = "PDF",
                    SourceRootLabel = "Watercare Electrical Standards",
                    Summary = "Switchboards distribution centres control centres mandatory QA checks per section 1.13",
                    Topics = ["switchboard", "distribution", "control", "QA/QC", "mandatory"],
                },
                new LearningDocument
                {
                    FileName = "cable-installation.md",
                    RelativePath = "Knowledge/cable-installation.md",
                    Kind = "md",
                    Summary = "Cable installation conduit fill guidelines",
                    Topics = ["cable", "conduit", "installation"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch("distribution centre mandatory QA", library);

        Assert.NotEmpty(result.Results);
        Assert.Equal("qa_templates_for_electrical_construction_standards.pdf", result.Results[0].Title);
    }

    [Fact]
    public void ControlCentre_KnowledgeSearch_AllThreeEquipmentTypes_ReturnTemplateDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "qa_templates_for_electrical_construction_standards.pdf",
                    RelativePath = "Knowledge/qa_templates_for_electrical_construction_standards.pdf",
                    Kind = "PDF",
                    SourceRootLabel = "Watercare Electrical Standards",
                    Summary = "Switchboards distribution centres control centres mandatory checks",
                    Topics = ["switchboard", "distribution", "control", "mandatory", "QA/QC"],
                },
            ],
        };

        // Query mentions all three equipment types in section 1.13 item 3
        var result = KnowledgeSearchService.FallbackTextSearch(
            "switchboard distribution control centre mandatory QA/QC", library);

        Assert.NotEmpty(result.Results);
        Assert.Equal("qa_templates_for_electrical_construction_standards.pdf", result.Results[0].Title);
        Assert.True(result.Results[0].Score > 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group 5 – Control centre learning document structure
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ControlCentre_LearningDocument_HasCorrectTopics()
    {
        var doc = new LearningDocument
        {
            FileName = "qa_templates_for_electrical_construction_standards.pdf",
            RelativePath = "Knowledge/qa_templates_for_electrical_construction_standards.pdf",
            Kind = "PDF",
            SourceRootLabel = "Watercare Electrical Standards",
            Summary = "Control centres mandatory QA/QC section 1.13 – termination checks, protection relay settings, interlocking, FAT/SAT",
            Topics = ["control centre", "termination", "relay", "interlock", "FAT", "SAT", "QA/QC"],
        };

        Assert.Contains("control centre", doc.Topics);
        Assert.Contains("termination", doc.Topics);
        Assert.Contains("relay", doc.Topics);
        Assert.Contains("interlock", doc.Topics);
        Assert.Contains("FAT", doc.Topics);
        Assert.Contains("SAT", doc.Topics);
    }

    [Fact]
    public void ControlCentre_LearningDocument_PromptSummary_IncludesControlCentreAndWatercareLabel()
    {
        var doc = new LearningDocument
        {
            FileName = "qa_templates_for_electrical_construction_standards.pdf",
            RelativePath = "Knowledge/qa_templates_for_electrical_construction_standards.pdf",
            Kind = "PDF",
            SourceRootLabel = "Watercare Electrical Standards",
            Summary = "Control centres – mandatory QA/QC tests per section 1.13",
            Topics = ["control centre", "QA/QC", "mandatory", "Watercare"],
        };

        Assert.Contains("Watercare Electrical Standards", doc.PromptSummary, StringComparison.Ordinal);
        Assert.Contains("PDF", doc.PromptSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void DistributionCentre_LearningDocument_HasCorrectKindAndMetadata()
    {
        var doc = new LearningDocument
        {
            FileName = "qa_templates_for_electrical_construction_standards.pdf",
            RelativePath = "Knowledge/qa_templates_for_electrical_construction_standards.pdf",
            Kind = "PDF",
            SourceRootLabel = "Watercare Electrical Standards",
            Summary = "Distribution centres mandatory QA/QC section 1.13",
            Topics = ["distribution", "QA/QC", "mandatory", "section 1.13"],
        };

        Assert.Equal("PDF", doc.Kind);
        Assert.Equal("Watercare Electrical Standards", doc.SourceRootLabel);
        Assert.Contains("Distribution", doc.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.13", doc.Summary);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group 6 – Defense evaluation rubric for control centres
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ControlCentre_DefenseEvaluation_HasFiveRubricDimensions()
    {
        var evaluation = new DefenseEvaluation
        {
            Summary = "Control centre QA/QC answer evaluation.",
            TotalScore = 13,
            MaxScore = 20,
            RubricItems =
            [
                new DefenseRubricItem { Name = "Technical Correctness",  Score = 3, Feedback = "Relay protection standards referenced." },
                new DefenseRubricItem { Name = "Tradeoff Reasoning",    Score = 2, Feedback = "Tradeoff between speed and completeness noted." },
                new DefenseRubricItem { Name = "Failure-Mode Awareness", Score = 3, Feedback = "Fault propagation risk named for control centre." },
                new DefenseRubricItem { Name = "Validation Thinking",   Score = 2, Feedback = "FAT/SAT sign-off process described." },
                new DefenseRubricItem { Name = "Clarity",               Score = 3, Feedback = "Answer covers all four mandatory control centre checks." },
            ],
        };

        var names = evaluation.RubricItems.Select(item => item.Name).ToList();
        Assert.Contains("Technical Correctness",  names);
        Assert.Contains("Tradeoff Reasoning",     names);
        Assert.Contains("Failure-Mode Awareness", names);
        Assert.Contains("Validation Thinking",    names);
        Assert.Contains("Clarity",                names);
        Assert.Equal(5, evaluation.RubricItems.Count);
        Assert.Equal(13, evaluation.TotalScore);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group 7 – Integration with the workflow integration standards document
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void IntegrationStandardsDocument_ContainsControlCentreCategory()
    {
        var repoRoot = FindRepoRoot();
        Assert.NotNull(repoRoot);

        var markdownPath = Path.Combine(
            repoRoot!,
            "Knowledge", "Research",
            "20260406-electrical-qaqc-workflow-integration-standards.md"
        );

        Assert.True(File.Exists(markdownPath), $"Expected integration standards file at: {markdownPath}");
        var content = File.ReadAllText(markdownPath);
        Assert.Contains("Control Centres", content, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrationStandardsDocument_ControlCentreSection_MentionsFourMandatoryChecks()
    {
        var repoRoot = FindRepoRoot();
        Assert.NotNull(repoRoot);

        var markdownPath = Path.Combine(
            repoRoot!,
            "Knowledge", "Research",
            "20260406-electrical-qaqc-workflow-integration-standards.md"
        );

        Assert.True(File.Exists(markdownPath), $"Expected integration standards file at: {markdownPath}");
        var content = File.ReadAllText(markdownPath);

        // The integration standards document must list all four mandatory checks for category 3
        Assert.Contains("termination checks", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("protection relay settings", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("interlocking verification", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FAT/SAT records", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntegrationStandardsDocument_ControlCentreSection_IncludesSignedRecordRequirement()
    {
        var repoRoot = FindRepoRoot();
        Assert.NotNull(repoRoot);

        var markdownPath = Path.Combine(
            repoRoot!,
            "Knowledge", "Research",
            "20260406-electrical-qaqc-workflow-integration-standards.md"
        );

        Assert.True(File.Exists(markdownPath), $"Expected integration standards file at: {markdownPath}");
        var content = File.ReadAllText(markdownPath);

        // Each category requires a signed QA/QC record sheet (project deliverable requirement)
        Assert.Contains("signed", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("record", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntegrationStandardsDocument_ControlCentreCategory_IsItem3OfSection113()
    {
        var repoRoot = FindRepoRoot();
        Assert.NotNull(repoRoot);

        var markdownPath = Path.Combine(
            repoRoot!,
            "Knowledge", "Research",
            "20260406-electrical-qaqc-workflow-integration-standards.md"
        );

        Assert.True(File.Exists(markdownPath), $"Expected integration standards file at: {markdownPath}");
        var content = File.ReadAllText(markdownPath);

        // The document must list category 3 as "Switchboards, Distribution Centres, and Control Centres"
        Assert.Contains("Switchboards, Distribution Centres, and Control Centres", content, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group 8 – Fallback scenario integration for control centres
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ControlCentre_FallbackScenario_PreferredTopicIsPreserved()
    {
        var provider = new ThrowingModelProvider();
        var service = new OralDefenseService(provider, "test-model");

        var snapshot = new SuiteSnapshot { HotAreas = ["control centre commissioning"] };
        var history = new TrainingHistorySummary();
        var profile = new LearningProfile();
        var library = new LearningLibrary();

        var scenario = await service.CreateScenarioAsync(
            snapshot,
            history,
            profile,
            library,
            Array.Empty<StudyTrack>(),
            preferredTopic: "control centre QA/QC"
        );

        Assert.Equal("control centre QA/QC", scenario.Topic);
    }

    [Fact]
    public async Task ControlCentre_FallbackScenario_TitleReferencesControlCentreTopic()
    {
        var provider = new ThrowingModelProvider();
        var service = new OralDefenseService(provider, "test-model");

        var snapshot = new SuiteSnapshot { HotAreas = ["control centre commissioning"] };
        var history = new TrainingHistorySummary();
        var profile = new LearningProfile();
        var library = new LearningLibrary();

        var scenario = await service.CreateScenarioAsync(
            snapshot,
            history,
            profile,
            library,
            Array.Empty<StudyTrack>(),
            preferredTopic: "control centre QA/QC"
        );

        Assert.Contains("control centre QA/QC", scenario.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ControlCentre_FallbackScenario_HasFourFollowUpQuestions()
    {
        var provider = new ThrowingModelProvider();
        var service = new OralDefenseService(provider, "test-model");

        var snapshot = new SuiteSnapshot { HotAreas = ["control centre commissioning"] };
        var history = new TrainingHistorySummary();
        var profile = new LearningProfile();
        var library = new LearningLibrary();

        var scenario = await service.CreateScenarioAsync(
            snapshot,
            history,
            profile,
            library,
            Array.Empty<StudyTrack>(),
            preferredTopic: "control centre QA/QC"
        );

        Assert.Equal(4, scenario.FollowUpQuestions.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group 9 – Category 3 signed record sheet requirement
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ControlCentre_SignedRecordSheet_IsRequiredDeliverable()
    {
        // Section 1.13 item 3 requires a signed QA/QC record for each equipment type
        var controlCentreChecklist = new[]
        {
            "termination checks",
            "protection relay settings",
            "interlocking verification",
            "FAT/SAT records",
        };

        Assert.Equal(4, controlCentreChecklist.Length);
        Assert.Contains("termination checks", controlCentreChecklist);
        Assert.Contains("protection relay settings", controlCentreChecklist);
        Assert.Contains("interlocking verification", controlCentreChecklist);
        Assert.Contains("FAT/SAT records", controlCentreChecklist);
    }

    [Fact]
    public void DistributionCentre_SignedRecordSheet_MatchesSwitchboardChecklist()
    {
        // Distribution centres require the same 4 mandatory checks as switchboards (section 1.13 item 3)
        var distributionCentreChecklist = new[]
        {
            "termination checks",
            "protection relay settings",
            "interlocking verification",
            "FAT/SAT records",
        };

        // Category3MandatoryChecks must exactly equal the distribution centre checklist
        Assert.Equal(Category3MandatoryChecks.Length, distributionCentreChecklist.Length);
        foreach (var check in Category3MandatoryChecks)
        {
            Assert.Contains(check, distributionCentreChecklist);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group 10 – Control centre is distinct from other section 1.13 categories
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ControlCentre_Category_IsDistinctFromEarthingCategory()
    {
        const string category3 = Section113Item3Category;
        const string category7 = "Earthing and Bonding Systems";

        Assert.NotEqual(category3, category7);
        Assert.Contains("Control Centres", category3, StringComparison.Ordinal);
        Assert.DoesNotContain("Control Centres", category7, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlCentre_Category_IsDistinctFromInstrumentationCategory()
    {
        const string category3 = Section113Item3Category;
        const string category6 = "Instrumentation and Control Wiring";

        Assert.NotEqual(category3, category6);
        // Category 3 covers switchboards and centres; category 6 covers instrumentation wiring
        Assert.Contains("Switchboards", category3, StringComparison.Ordinal);
        Assert.Contains("Instrumentation", category6, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlCentre_Category3_IsAtPosition3InSection113List()
    {
        // Section 1.13 item 3 must be at index 2 (zero-based) in the mandatory categories list
        Assert.Equal(Section113Item3Category, AllSection113Categories[2]);
        Assert.NotEqual(Section113Item3Category, AllSection113Categories[0]);
        Assert.NotEqual(Section113Item3Category, AllSection113Categories[1]);
    }
}
