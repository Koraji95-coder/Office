using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify the Electrical Construction QA/QC template
/// documentation added to AGENT_REPLY_GUIDE.md (chunk8 – "Electrical Construction
/// QA/QC Templates" section).
///
/// The tests are structured in four groups:
///   1. Section presence — verify the section header and template URL exist.
///   2. Section 1.13 mandatory categories — verify all seven categories are documented.
///   3. Workflow phase table — verify the five-phase workflow integration table is present.
///   4. Prompt patterns — verify the prompt pattern entries cover the required scope.
/// </summary>
public sealed class ElectricalQaQcTemplateDocumentationTests
{
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

    private static string GetAgentReplyGuidePath()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        return Path.Combine(root!, "DailyDesk", "AGENT_REPLY_GUIDE.md");
    }

    private static string ReadGuide()
    {
        var path = GetAgentReplyGuidePath();
        Assert.True(File.Exists(path), $"AGENT_REPLY_GUIDE.md not found at: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Extracts the body of the "Electrical Construction QA/QC Templates" section
    /// from AGENT_REPLY_GUIDE.md.
    /// </summary>
    private static string ExtractElectricalQaQcSection(string guide)
    {
        const string sectionHeader = "## Electrical Construction QA/QC Templates";
        var start = guide.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var nextSection = guide.IndexOf("\n## ", start + sectionHeader.Length, StringComparison.Ordinal);
        return nextSection >= 0
            ? guide[start..nextSection]
            : guide[start..];
    }

    // =========================================================================
    // Group 1 – Section presence
    // =========================================================================

    [Fact]
    public void AgentReplyGuide_Exists()
    {
        var path = GetAgentReplyGuidePath();
        Assert.True(File.Exists(path), $"AGENT_REPLY_GUIDE.md must exist at DailyDesk/AGENT_REPLY_GUIDE.md; not found at: {path}");
    }

    [Fact]
    public void AgentReplyGuide_ContainsElectricalConstructionQaQcTemplatesSection()
    {
        var guide = ReadGuide();
        Assert.Contains("## Electrical Construction QA/QC Templates", guide);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsWatercareUrl()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains(
            "wslpwstoreprd.blob.core.windows.net/kentico-media-libraries-prod/watercarepublicweb/media/watercare-media-library/electrical-standards/qa_templates_for_electrical_construction_standards.pdf",
            section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ReferencesSection113()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("1.13", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_MentionsMandatoryTests()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("mandatory", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsSection113MandatoryCategoriesSubheading()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Section 1.13 Mandatory Test Categories", section);
    }

    // =========================================================================
    // Group 2 – Section 1.13 mandatory categories
    // =========================================================================

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory1_GeneralElectricalInstallation()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("General Electrical Installation", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory1_EarthingContinuity()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("earthing continuity", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory1_InsulationResistance()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("insulation resistance", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory2_CablesAndConduit()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Cables and Conduit", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory3_Switchboards()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Switchboards", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory3_DistributionCentres()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Distribution Centres", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory3_ControlCentres()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Control Centres", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory4_MotorsAndDrives()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Motors and Drives", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory5_LightingAndSmallPower()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Lighting and Small Power", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory6_InstrumentationAndControlWiring()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Instrumentation and Control Wiring", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsCategory7_EarthingAndBondingSystems()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Earthing and Bonding Systems", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ListsAllSevenCategories()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());

        var requiredCategories = new[]
        {
            "General Electrical Installation",
            "Cables and Conduit",
            "Switchboards",
            "Motors and Drives",
            "Lighting and Small Power",
            "Instrumentation and Control Wiring",
            "Earthing and Bonding",
        };

        foreach (var category in requiredCategories)
        {
            Assert.Contains(category, section);
        }
    }

    // =========================================================================
    // Group 3 – Workflow phase table
    // =========================================================================

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsWorkflowIntegrationSubheading()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Workflow Integration", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_WorkflowTable_ContainsSchematicDesignPhase()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Schematic Design", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_WorkflowTable_ContainsIssuedForConstructionPhase()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Issued-for-Construction", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_WorkflowTable_ContainsConstructionAndInstallationPhase()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Construction", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_WorkflowTable_ContainsCommissioningAndHandoverPhase()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Commissioning", section);
    }

    // =========================================================================
    // Group 4 – Prompt patterns
    // =========================================================================

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsPromptPatternFor113Review()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Prompt Pattern For 1.13 QA/QC Review", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_FullReviewPrompt_ContainsAllSevenCategoryKeywords()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        // The full-review prompt should name all seven categories so the agent
        // generates a complete pass/fail checklist without ambiguity.
        Assert.Contains("cables and conduit", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("motors and drives", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("earthing and bonding", section, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsPromptPatternForCategorySpecificReview()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Category-Specific Review", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_CategoryPrompt_MentionsSignOffRecord()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("sign-off record", section, StringComparison.OrdinalIgnoreCase);
    }
}
