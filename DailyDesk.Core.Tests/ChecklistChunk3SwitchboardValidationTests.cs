using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify switchboard QA/QC validation compliance as
/// specified in the electrical drawing QA/QC workflow standards review checklist
/// (Knowledge/Research/20260324-000659-electrical-drawing-qa-workflow-standards-review-checklist.md),
/// specifically chunk3 — section 1.13 item 3: Switchboards, Distribution Centres,
/// and Control Centres.
///
/// The tests are structured in four groups:
///   1. Checklist document structure — verify the research checklist document
///      exists and references section 1.13 chunk3 content for switchboards,
///      distribution centres, and control centres.
///   2. Integration standards document accuracy — verify that
///      20260406-electrical-qaqc-workflow-integration-standards.md lists all
///      four mandatory checks for chunk3 (termination checks, protection relay
///      settings, interlocking verification, FAT/SAT records).
///   3. AGENT_REPLY_GUIDE cross-validation — verify AGENT_REPLY_GUIDE.md
///      contains the "Electrical Construction QA/QC Templates" section and
///      references the Watercare template, section 1.13, and switchboards.
///   4. Model integration tests — verify OralDefenseScenario construction and
///      fallback scoring behaviour are consistent with chunk3 requirements.
/// </summary>
public sealed class ChecklistChunk3SwitchboardValidationTests
{
    // -----------------------------------------------------------------------
    // Shared constants – section 1.13 category 3 mandatory checks
    // -----------------------------------------------------------------------

    private static readonly string[] MandatoryChecks =
    [
        "termination checks",
        "protection relay settings",
        "interlocking verification",
        "FAT/SAT records",
    ];

    private static readonly string[] DeviceTypes =
    [
        "switchboard",
        "distribution centre",
        "control centre",
    ];

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

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

    private static string GetRepoRoot()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        return root!;
    }

    private static string GetChecklistPath()
        => Path.Combine(
            GetRepoRoot(),
            "Knowledge",
            "Research",
            "20260324-000659-electrical-drawing-qa-workflow-standards-review-checklist.md");

    private static string GetIntegrationStandardsPath()
        => Path.Combine(
            GetRepoRoot(),
            "Knowledge",
            "Research",
            "20260406-electrical-qaqc-workflow-integration-standards.md");

    private static string GetAgentReplyGuidePath()
        => Path.Combine(GetRepoRoot(), "DailyDesk", "AGENT_REPLY_GUIDE.md");

    private static string ReadChecklist()
    {
        var path = GetChecklistPath();
        Assert.True(File.Exists(path),
            $"Checklist document not found at: {path}");
        return File.ReadAllText(path);
    }

    private static string ReadIntegrationStandards()
    {
        var path = GetIntegrationStandardsPath();
        Assert.True(File.Exists(path),
            $"Integration standards document not found at: {path}");
        return File.ReadAllText(path);
    }

    private static string ReadAgentReplyGuide()
    {
        var path = GetAgentReplyGuidePath();
        Assert.True(File.Exists(path),
            $"AGENT_REPLY_GUIDE.md not found at: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Extracts the "Mandatory Test Categories" section from the integration
    /// standards document up to the next top-level heading.
    /// </summary>
    private static string ExtractMandatoryTestCategoriesSection(string doc)
    {
        const string sectionHeader = "## Mandatory Test Categories";
        var start = doc.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var nextSection = doc.IndexOf("\n## ", start + sectionHeader.Length, StringComparison.Ordinal);
        return nextSection >= 0 ? doc[start..nextSection] : doc[start..];
    }

    /// <summary>
    /// Extracts the "Electrical Construction QA/QC Templates" section from
    /// AGENT_REPLY_GUIDE.md up to the next top-level heading.
    /// </summary>
    private static string ExtractElectricalQaQcSection(string guide)
    {
        const string sectionHeader = "## Electrical Construction QA/QC Templates";
        var start = guide.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var nextSection = guide.IndexOf("\n## ", start + sectionHeader.Length, StringComparison.Ordinal);
        return nextSection >= 0 ? guide[start..nextSection] : guide[start..];
    }

    private static OralDefenseService MakeService() =>
        new(new ThrowingModelProvider(), "test-model");

    // =========================================================================
    // Group 1 – Checklist Document Structure
    // =========================================================================

    [Fact]
    public void ChecklistDocument_FileExists()
    {
        var path = GetChecklistPath();
        Assert.True(File.Exists(path),
            $"Research checklist document must exist at: {path}");
    }

    [Fact]
    public void ChecklistDocument_ContainsSection113Reference()
    {
        var checklist = ReadChecklist();
        Assert.Contains("1.13", checklist, StringComparison.Ordinal);
    }

    [Fact]
    public void ChecklistDocument_ContainsSwitchboardReference()
    {
        var checklist = ReadChecklist();
        Assert.Contains("Switchboard", checklist, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChecklistDocument_ContainsDistributionCentreReference()
    {
        var checklist = ReadChecklist();
        Assert.Contains("distribution centre", checklist, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChecklistDocument_ContainsControlCentreReference()
    {
        var checklist = ReadChecklist();
        Assert.Contains("control centre", checklist, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChecklistDocument_ContainsWatercareSourceSnippet()
    {
        var checklist = ReadChecklist();
        // The Watercare QA/QC PDF is the source that specifies section 1.13 mandatory tests.
        Assert.True(
            checklist.Contains("Watercare", StringComparison.OrdinalIgnoreCase)
            || checklist.Contains("wslpwstoreprd", StringComparison.OrdinalIgnoreCase),
            "Checklist document must reference the Watercare QA/QC template source.");
    }

    [Fact]
    public void ChecklistDocument_CrossReferences_IntegrationStandardsDocument()
    {
        var checklist = ReadChecklist();
        Assert.Contains(
            "20260406-electrical-qaqc-workflow-integration-standards",
            checklist,
            StringComparison.Ordinal);
    }

    // =========================================================================
    // Group 2 – Integration Standards Document Chunk3 Accuracy
    // =========================================================================

    [Fact]
    public void IntegrationStandardsDocument_FileExists()
    {
        var path = GetIntegrationStandardsPath();
        Assert.True(File.Exists(path),
            $"Integration standards document must exist at: {path}");
    }

    [Fact]
    public void IntegrationStandardsDocument_ContainsMandatoryTestCategoriesSection()
    {
        var doc = ReadIntegrationStandards();
        Assert.Contains("Mandatory Test Categories", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrationStandardsDocument_Chunk3_ListsSwitchboardsDistributionCentresControlCentres()
    {
        var section = ExtractMandatoryTestCategoriesSection(ReadIntegrationStandards());
        Assert.False(string.IsNullOrWhiteSpace(section),
            "Mandatory Test Categories section must not be empty.");
        Assert.Contains("Switchboards, Distribution Centres, and Control Centres", section, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrationStandardsDocument_Chunk3_ListsTerminationChecks()
    {
        var section = ExtractMandatoryTestCategoriesSection(ReadIntegrationStandards());
        Assert.Contains("termination checks", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntegrationStandardsDocument_Chunk3_ListsProtectionRelaySettings()
    {
        var section = ExtractMandatoryTestCategoriesSection(ReadIntegrationStandards());
        Assert.Contains("protection relay settings", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntegrationStandardsDocument_Chunk3_ListsInterlockingVerification()
    {
        var section = ExtractMandatoryTestCategoriesSection(ReadIntegrationStandards());
        Assert.Contains("interlocking verification", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntegrationStandardsDocument_Chunk3_ListsFatSatRecords()
    {
        var section = ExtractMandatoryTestCategoriesSection(ReadIntegrationStandards());
        Assert.Contains("FAT/SAT records", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntegrationStandardsDocument_Chunk3_IsThirdItemInList()
    {
        // Verify that "Switchboards, Distribution Centres, and Control Centres" is
        // listed as item 3 in the mandatory test categories.
        var section = ExtractMandatoryTestCategoriesSection(ReadIntegrationStandards());
        Assert.False(string.IsNullOrWhiteSpace(section));
        Assert.Contains("3.", section, StringComparison.Ordinal);
        var item3Index = section.IndexOf("3.", StringComparison.Ordinal);
        var item3Text = section[item3Index..];
        Assert.True(
            item3Text.Contains("Switchboard", StringComparison.OrdinalIgnoreCase)
            || item3Text.Contains("distribution centre", StringComparison.OrdinalIgnoreCase),
            "Item 3 in the mandatory test categories list must reference switchboards or distribution centres.");
    }

    [Fact]
    public void IntegrationStandardsDocument_AllFourMandatoryChecks_ArePresent()
    {
        var section = ExtractMandatoryTestCategoriesSection(ReadIntegrationStandards());
        foreach (var check in MandatoryChecks)
        {
            Assert.True(
                section.Contains(check, StringComparison.OrdinalIgnoreCase),
                $"Mandatory test categories section must contain '{check}'.");
        }
    }

    // =========================================================================
    // Group 3 – AGENT_REPLY_GUIDE Cross-Validation
    // =========================================================================

    [Fact]
    public void AgentReplyGuide_ContainsElectricalQaQcTemplatesSection()
    {
        var guide = ReadAgentReplyGuide();
        Assert.Contains("## Electrical Construction QA/QC Templates", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ReferencesWatercareTemplate()
    {
        var section = ExtractElectricalQaQcSection(ReadAgentReplyGuide());
        Assert.False(string.IsNullOrWhiteSpace(section),
            "Electrical Construction QA/QC Templates section must not be empty.");
        Assert.Contains("Watercare", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ReferencesSection113()
    {
        var section = ExtractElectricalQaQcSection(ReadAgentReplyGuide());
        Assert.Contains("1.13", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ReferencesSwitchboards()
    {
        var section = ExtractElectricalQaQcSection(ReadAgentReplyGuide());
        Assert.Contains("switchboard", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ReferencesDistributionCentres()
    {
        var section = ExtractElectricalQaQcSection(ReadAgentReplyGuide());
        Assert.Contains("distribution centre", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ReferencesControlCentres()
    {
        var section = ExtractElectricalQaQcSection(ReadAgentReplyGuide());
        Assert.Contains("control centre", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_IncludesPromptPattern()
    {
        var section = ExtractElectricalQaQcSection(ReadAgentReplyGuide());
        // The guide must include a prompt pattern for 1.13 QA/QC review.
        Assert.True(
            section.Contains("Prompt Pattern", StringComparison.OrdinalIgnoreCase)
            || section.Contains("prompt", StringComparison.OrdinalIgnoreCase),
            "Electrical Construction QA/QC Templates section must include a prompt pattern.");
    }

    // =========================================================================
    // Group 4 – Model Integration Tests
    // =========================================================================

    [Fact]
    public void ChecklistChunk3_OralDefenseScenarios_CanBeCreated_ForAllThreeDeviceTypes()
    {
        foreach (var device in DeviceTypes)
        {
            var scenario = new OralDefenseScenario
            {
                Topic = $"{device} QA/QC",
                Title = $"{device} Commissioning: Mandatory QA/QC Checks (Section 1.13 Chunk3)",
                Prompt =
                    $"Describe the mandatory QA/QC checks for a {device} before energisation, "
                    + "including termination checks, protection relay settings, "
                    + "interlocking verification, and FAT/SAT records.",
                WhatGoodLooksLike =
                    $"A strong answer covers all four mandatory checks for a {device} "
                    + "per Watercare QA/QC template section 1.13 chunk3.",
            };

            Assert.Equal($"{device} QA/QC", scenario.Topic);
            Assert.Contains("termination checks", scenario.Prompt);
            Assert.Contains("protection relay settings", scenario.Prompt);
            Assert.Contains("interlocking verification", scenario.Prompt);
            Assert.Contains("FAT/SAT records", scenario.Prompt);
        }
    }

    [Fact]
    public void ChecklistChunk3_LearningDocument_Topics_IncludeAllFourMandatoryChecksAndThreeDeviceTypes()
    {
        var document = new LearningDocument
        {
            FileName = "checklist-chunk3-switchboard-dc-cc-qaqc.md",
            RelativePath = "Knowledge/Research/checklist-chunk3-switchboard-dc-cc-qaqc.md",
            Summary =
                "Watercare QA/QC template section 1.13 chunk3: mandatory tests for "
                + "switchboards, distribution centres, and control centres. "
                + "Covers termination checks, protection relay settings, "
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
                "section 1.13",
            ],
        };

        // All three device types must appear as topics.
        foreach (var device in DeviceTypes)
        {
            Assert.Contains(device, document.Topics);
        }

        // All four mandatory checks must appear as topics.
        foreach (var check in MandatoryChecks)
        {
            Assert.Contains(check, document.Topics);
        }
    }

    [Fact]
    public void ChecklistChunk3_KnowledgeSearch_SwitchboardChunk3Query_FindsRelevantDocument()
    {
        var library = new LearningLibrary
        {
            Documents =
            [
                new LearningDocument
                {
                    FileName = "checklist-chunk3-switchboard-dc-cc-qaqc.md",
                    RelativePath = "Knowledge/Research/checklist-chunk3-switchboard-dc-cc-qaqc.md",
                    Summary =
                        "Watercare QA/QC section 1.13 chunk3: switchboards, distribution centres, "
                        + "control centres — termination checks, protection relay settings, "
                        + "interlocking verification, FAT/SAT records.",
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
                },
                new LearningDocument
                {
                    FileName = "general-electrical-installation-qaqc.md",
                    RelativePath = "Knowledge/general-electrical-installation-qaqc.md",
                    Summary = "Section 1.13 category 1: general electrical installation QA/QC.",
                    Topics = ["earthing continuity", "insulation resistance", "polarity", "functional tests"],
                },
            ],
        };

        var result = KnowledgeSearchService.FallbackTextSearch(
            "switchboard distribution centre control centre termination interlocking FAT SAT",
            library
        );

        Assert.NotEmpty(result.Results);
        Assert.Equal("checklist-chunk3-switchboard-dc-cc-qaqc.md", result.Results[0].Title);
    }

    [Fact]
    public async Task ChecklistChunk3_FallbackScoring_TerminationKeyword_ScoresTechnicalCorrectnessThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario
        {
            Topic = "switchboard QA/QC section 1.13 chunk3",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "All termination checks must be completed and signed off against the approved "
            + "protection standard before the switchboard or distribution centre is energised. "
            + "Termination torque values must be verified and recorded.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(
            technical!.Score >= 3,
            $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task ChecklistChunk3_FallbackScoring_ProtectionRelayKeyword_ScoresTechnicalCorrectnessThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario
        {
            Topic = "switchboard QA/QC section 1.13 chunk3",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Protection relay settings for the switchboard must be verified against the approved "
            + "relay co-ordination study and set point schedule before the protection relay "
            + "is brought into service.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var technical = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Technical Correctness");
        Assert.NotNull(technical);
        Assert.True(
            technical!.Score >= 3,
            $"Expected Technical Correctness >= 3 but was {technical.Score}");
    }

    [Fact]
    public async Task ChecklistChunk3_FallbackScoring_InterlockingKeyword_ScoresValidationThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario
        {
            Topic = "distribution centre QA/QC section 1.13 chunk3",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Interlocking must be verified: the distribution centre bus interlocks "
            + "must be tested to confirm they function correctly and prevent simultaneous "
            + "closing of incompatible breakers before energisation.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(
            validation!.Score >= 3,
            $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public async Task ChecklistChunk3_FallbackScoring_FatSatKeyword_ScoresValidationThreeOrMore()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario
        {
            Topic = "control centre QA/QC section 1.13 chunk3",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "FAT/SAT records for the control centre must be completed before energisation. "
            + "The factory acceptance test (FAT) record confirms shop-testing, and the site "
            + "acceptance test (SAT) record confirms installed performance.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        var validation = evaluation.RubricItems.FirstOrDefault(r => r.Name == "Validation Thinking");
        Assert.NotNull(validation);
        Assert.True(
            validation!.Score >= 3,
            $"Expected Validation Thinking >= 3 but was {validation.Score}");
    }

    [Fact]
    public async Task ChecklistChunk3_FallbackScoring_MaxScoreIsAlwaysTwenty()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario
        {
            Topic = "switchboard QA/QC section 1.13 chunk3",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Termination checks, protection relay settings, interlocking verification, "
            + "and FAT/SAT records are all mandatory QA/QC steps for switchboards.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.Equal(20, evaluation.MaxScore);
    }

    [Fact]
    public async Task ChecklistChunk3_FallbackScoring_HasExactlyFiveRubricItems()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario
        {
            Topic = "switchboard QA/QC section 1.13 chunk3",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Switchboard commissioning requires termination checks and protection relay verification.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.Equal(5, evaluation.RubricItems.Count);
    }

    [Fact]
    public async Task ChecklistChunk3_FallbackScoring_TotalScoreWithinValidRange()
    {
        var service = MakeService();
        var scenario = new OralDefenseScenario
        {
            Topic = "switchboard QA/QC section 1.13 chunk3",
        };

        var evaluation = await service.ScoreResponseAsync(
            scenario,
            "Termination checks, relay settings, interlocking verification, and FAT/SAT "
            + "records are mandatory before energising any switchboard, distribution centre, "
            + "or control centre under Watercare section 1.13.",
            new SuiteSnapshot(),
            new LearningProfile(),
            new LearningLibrary()
        );

        Assert.True(
            evaluation.TotalScore >= 0 && evaluation.TotalScore <= 20,
            $"Total score {evaluation.TotalScore} must be between 0 and 20.");
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
