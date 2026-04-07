using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify the Electrical Construction QA/QC template
/// documentation added to AGENT_REPLY_GUIDE.md (chunk8 – "Electrical Construction
/// QA/QC Templates" section).
///
/// The tests are structured in four groups:
///   1. Section presence — verify the section header, template URL, and section 1.13 reference.
///   2. Section 1.13 mandatory categories — verify all seven categories are documented in one pass.
///   3. Workflow phase table — verify all five workflow phases appear.
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
    /// from AGENT_REPLY_GUIDE.md (up to the next top-level heading).
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
        Assert.True(File.Exists(path),
            $"AGENT_REPLY_GUIDE.md must exist at DailyDesk/AGENT_REPLY_GUIDE.md; not found at: {path}");
    }

    [Fact]
    public void AgentReplyGuide_ContainsElectricalConstructionQaQcTemplatesSection()
    {
        Assert.Contains("## Electrical Construction QA/QC Templates", ReadGuide());
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsWatercareUrl()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains(
            "wslpwstoreprd.blob.core.windows.net/kentico-media-libraries-prod/watercarepublicweb/"
            + "media/watercare-media-library/electrical-standards/"
            + "qa_templates_for_electrical_construction_standards.pdf",
            section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ReferencesSection113WithMandatoryCategoriesSubheading()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("1.13", section);
        Assert.Contains("Section 1.13 Mandatory Test Categories", section);
    }

    // =========================================================================
    // Group 2 – Section 1.13 mandatory categories (all seven in one pass)
    // =========================================================================

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ListsAllSevenMandatoryCategories()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());

        var requiredCategories = new[]
        {
            "General Electrical Installation",
            "Cables and Conduit",
            "Switchboards",
            "Distribution Centres",
            "Control Centres",
            "Motors and Drives",
            "Lighting and Small Power",
            "Instrumentation and Control Wiring",
            "Earthing and Bonding",
        };

        foreach (var category in requiredCategories)
            Assert.Contains(category, section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsKeyMandatoryTestTerms()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());

        // Representative technical terms that must appear in the category table rows.
        var requiredTerms = new[]
        {
            "earthing continuity",
            "insulation resistance",
            "sign-off record",
        };

        foreach (var term in requiredTerms)
            Assert.Contains(term, section, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // Group 3 – Workflow phase table (all five phases in one pass)
    // =========================================================================

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_WorkflowIntegration_ListsAllFivePhases()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Workflow Integration", section);

        var requiredPhases = new[]
        {
            "Schematic Design",
            "Design Development",
            "Issued-for-Construction",
            "Construction",
            "Commissioning",
        };

        foreach (var phase in requiredPhases)
            Assert.Contains(phase, section);
    }

    // =========================================================================
    // Group 4 – Prompt patterns
    // =========================================================================

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_ContainsBothPromptPatterns()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());
        Assert.Contains("Prompt Pattern For 1.13 QA/QC Review", section);
        Assert.Contains("Category-Specific Review", section);
    }

    [Fact]
    public void AgentReplyGuide_ElectricalQaQcSection_FullReviewPrompt_NamesAllSevenCategoryGroups()
    {
        var section = ExtractElectricalQaQcSection(ReadGuide());

        // The full-review prompt must enumerate all seven categories so the agent
        // generates a complete pass/fail checklist without ambiguity.
        var promptCategories = new[]
        {
            "general electrical installation",
            "cables and conduit",
            "switchboards",
            "motors and drives",
            "lighting and small power",
            "instrumentation and control wiring",
            "earthing and bonding",
        };

        foreach (var cat in promptCategories)
            Assert.Contains(cat, section, StringComparison.OrdinalIgnoreCase);
    }
}
