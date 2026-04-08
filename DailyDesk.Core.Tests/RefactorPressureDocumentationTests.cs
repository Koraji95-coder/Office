using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify the structure and completeness of
/// <c>Docs/REFACTOR-PRESSURE.md</c> as specified in
/// <c>Mockups/Suite-Reboot-Storyboard/architecture-tools-guide.md</c> (Refactor pressure notes
/// section).
///
/// The tests are structured in four groups:
///   1. Document existence and top-level structure — verify the file is present and contains
///      the three priority sections and the Resolved Pressure archive.
///   2. Entry completeness — verify each of the eight numbered entries contains all six
///      required sections: File/Files, Phase introduced, What it does now, Why it is under
///      pressure, Refactor direction, and Prerequisite.
///   3. Action plan quality — verify that each High and Medium priority entry contains a
///      numbered action plan (lines starting with "1.", "2.", etc.) so that the cleanup
///      guidance is specific and actionable, not just a prose description.
///   4. Resolved Pressure archive — verify the archive table is present and contains at
///      least the entries recorded for previously resolved pressure areas.
/// </summary>
public sealed class RefactorPressureDocumentationTests
{
    // -----------------------------------------------------------------------
    // Shared state — cached once per test class instance to avoid repeated
    // filesystem access across the 80+ tests in this class.
    // -----------------------------------------------------------------------

    private static readonly string s_repoRoot = LocateRepoRoot();
    private static readonly string s_documentContent = LoadDocument(s_repoRoot);

    private static string LocateRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "DailyDesk", "DailyDesk.csproj")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException(
            "Could not locate repo root (expected to find DailyDesk/DailyDesk.csproj in an ancestor directory).");
    }

    private static string LoadDocument(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "Docs", "REFACTOR-PRESSURE.md");
        if (!File.Exists(path))
            return string.Empty; // Document existence is validated in Group 1.
        return File.ReadAllText(path);
    }

    private static string GetDocumentPath() =>
        Path.Combine(s_repoRoot, "Docs", "REFACTOR-PRESSURE.md");

    /// <summary>
    /// Extracts the text of a numbered entry section (e.g., "### 1.") from the document.
    /// Returns an empty string if the entry header is not found.
    /// </summary>
    private static string ExtractEntry(string document, int entryNumber)
    {
        var header = $"### {entryNumber}.";
        var start = document.IndexOf(header, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        // Find the next entry or top-level section heading after this one.
        var nextEntry = document.IndexOf("\n### ", start + header.Length, StringComparison.Ordinal);
        var nextSection = document.IndexOf("\n## ", start + header.Length, StringComparison.Ordinal);

        int end;
        if (nextEntry >= 0 && nextSection >= 0)
            end = Math.Min(nextEntry, nextSection);
        else if (nextEntry >= 0)
            end = nextEntry;
        else if (nextSection >= 0)
            end = nextSection;
        else
            end = document.Length;

        return document[start..end];
    }

    // -----------------------------------------------------------------------
    // Group 1: Document existence and top-level structure
    // -----------------------------------------------------------------------

    [Fact]
    public void Document_Exists_AtExpectedPath()
    {
        var path = GetDocumentPath();
        Assert.True(File.Exists(path),
            $"REFACTOR-PRESSURE.md was not found at the expected location: {path}. " +
            "The file is the canonical source for refactor pressure notes and must exist in Docs/.");
    }

    [Fact]
    public void Document_Contains_HighPressureSection()
    {
        Assert.Contains("## High Pressure", s_documentContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_Contains_MediumPressureSection()
    {
        Assert.Contains("## Medium Pressure", s_documentContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_Contains_LowPressureSection()
    {
        Assert.Contains("## Low Pressure", s_documentContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_Contains_ResolvedPressureArchive()
    {
        Assert.Contains("## Resolved Pressure", s_documentContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Document_Contains_NumberedEntry(int entryNumber)
    {
        var entry = ExtractEntry(s_documentContent, entryNumber);
        Assert.False(string.IsNullOrEmpty(entry),
            $"Entry #{entryNumber} was not found in REFACTOR-PRESSURE.md. " +
            "All eight pressure areas must be documented.");
    }

    // -----------------------------------------------------------------------
    // Group 2: Entry completeness — each entry must contain all six required
    // sections defined in architecture-tools-guide.md.
    // -----------------------------------------------------------------------

    public static IEnumerable<object[]> AllEntryNumbers =>
        Enumerable.Range(1, 8).Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(AllEntryNumbers))]
    public void Entry_Contains_PhaseIntroduced(int entryNumber)
    {
        var entry = ExtractEntry(s_documentContent, entryNumber);
        Assert.False(string.IsNullOrEmpty(entry), $"Entry #{entryNumber} not found.");
        Assert.Contains("Phase introduced", entry, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AllEntryNumbers))]
    public void Entry_Contains_WhatItDoesNow(int entryNumber)
    {
        var entry = ExtractEntry(s_documentContent, entryNumber);
        Assert.False(string.IsNullOrEmpty(entry), $"Entry #{entryNumber} not found.");
        Assert.Contains("What it does now", entry, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AllEntryNumbers))]
    public void Entry_Contains_WhyItIsUnderPressure(int entryNumber)
    {
        var entry = ExtractEntry(s_documentContent, entryNumber);
        Assert.False(string.IsNullOrEmpty(entry), $"Entry #{entryNumber} not found.");
        Assert.Contains("Why it is under pressure", entry, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AllEntryNumbers))]
    public void Entry_Contains_RefactorDirection(int entryNumber)
    {
        var entry = ExtractEntry(s_documentContent, entryNumber);
        Assert.False(string.IsNullOrEmpty(entry), $"Entry #{entryNumber} not found.");
        Assert.Contains("Refactor direction", entry, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AllEntryNumbers))]
    public void Entry_Contains_Prerequisite(int entryNumber)
    {
        var entry = ExtractEntry(s_documentContent, entryNumber);
        Assert.False(string.IsNullOrEmpty(entry), $"Entry #{entryNumber} not found.");
        Assert.Contains("Prerequisite", entry, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AllEntryNumbers))]
    public void Entry_Contains_FileOrFiles(int entryNumber)
    {
        var entry = ExtractEntry(s_documentContent, entryNumber);
        Assert.False(string.IsNullOrEmpty(entry), $"Entry #{entryNumber} not found.");

        // Either "**File**" or "**Files**" must appear in the entry header table.
        var hasFile = entry.Contains("**File**", StringComparison.Ordinal)
                   || entry.Contains("**Files**", StringComparison.Ordinal);
        Assert.True(hasFile,
            $"Entry #{entryNumber} must contain a '**File**' or '**Files**' row in its header table " +
            "per the architecture-tools-guide.md note format specification.");
    }

    // -----------------------------------------------------------------------
    // Group 3: Action plan quality — High and Medium entries must contain at
    // least three numbered action steps so cleanup guidance is actionable.
    // Entries 1–5 are High/Medium; entries 6–8 are Low (exempt from this check).
    // -----------------------------------------------------------------------

    public static IEnumerable<object[]> HighAndMediumEntryNumbers =>
        Enumerable.Range(1, 5).Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(HighAndMediumEntryNumbers))]
    public void HighAndMediumEntry_Contains_NumberedActionSteps(int entryNumber)
    {
        var entry = ExtractEntry(s_documentContent, entryNumber);
        Assert.False(string.IsNullOrEmpty(entry), $"Entry #{entryNumber} not found.");

        // Count lines that start a numbered list item (e.g., "1. ", "2. ", "3. ").
        var lines = entry.Split('\n');
        var numberedStepCount = lines
            .Select(l => l.Trim())
            .Count(l => l.Length > 3
                     && char.IsDigit(l[0])
                     && l[1] == '.'
                     && l[2] == ' ');

        Assert.True(numberedStepCount >= 3,
            $"Entry #{entryNumber} (High/Medium priority) must contain at least 3 numbered " +
            $"action steps in its Refactor direction section to meet the 'detailed action plan' " +
            $"requirement. Found {numberedStepCount} step(s). " +
            "Add a numbered list (1. step, 2. step, …) to the Refactor direction section.");
    }

    // -----------------------------------------------------------------------
    // Group 4: Resolved Pressure archive — verify known resolved areas are
    // recorded so contributors understand why certain patterns were adopted.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Manual JSON parsing for Ollama API")]
    [InlineData("Regex-based HTML parsing in LiveResearchService")]
    [InlineData("No retry logic on external calls")]
    [InlineData("JSON file persistence with no migration")]
    [InlineData("Blocking ML endpoints with no status visibility")]
    [InlineData("No job cleanup")]
    [InlineData("TF-IDF keyword search only for knowledge retrieval")]
    [InlineData("Direct LLM calls without agent structure")]
    [InlineData("Text-only document extraction")]
    [InlineData("No scheduled automation")]
    [InlineData("WPF client blocking on ML calls")]
    public void ResolvedPressureArchive_Contains_KnownResolvedArea(string areaFragment)
    {
        // Locate the Resolved Pressure section to scope the search.
        var archiveStart = s_documentContent.IndexOf("## Resolved Pressure", StringComparison.Ordinal);
        Assert.True(archiveStart >= 0,
            "Resolved Pressure section not found. The archive table is required.");

        var archiveSection = s_documentContent[archiveStart..];
        Assert.Contains(areaFragment, archiveSection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvedPressureArchive_IsTable_WithAreaColumn()
    {
        var archiveStart = s_documentContent.IndexOf("## Resolved Pressure", StringComparison.Ordinal);
        Assert.True(archiveStart >= 0, "Resolved Pressure section not found.");

        var archiveSection = s_documentContent[archiveStart..];

        // The archive must be a markdown table with an "Area" column header.
        Assert.Contains("| Area", archiveSection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvedPressureArchive_IsTable_WithResolvedInColumn()
    {
        var archiveStart = s_documentContent.IndexOf("## Resolved Pressure", StringComparison.Ordinal);
        Assert.True(archiveStart >= 0, "Resolved Pressure section not found.");

        var archiveSection = s_documentContent[archiveStart..];

        // The archive table must have a "Resolved In" column.
        Assert.Contains("Resolved In", archiveSection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvedPressureArchive_IsTable_WithResolutionColumn()
    {
        var archiveStart = s_documentContent.IndexOf("## Resolved Pressure", StringComparison.Ordinal);
        Assert.True(archiveStart >= 0, "Resolved Pressure section not found.");

        var archiveSection = s_documentContent[archiveStart..];

        // The archive table must have a "Resolution" column.
        Assert.Contains("Resolution", archiveSection, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Additional quality checks
    // -----------------------------------------------------------------------

    [Fact]
    public void Entry1_HighPressure_IsUnderHighPressureSection()
    {
        var highStart = s_documentContent.IndexOf("## High Pressure", StringComparison.Ordinal);
        var mediumStart = s_documentContent.IndexOf("## Medium Pressure", StringComparison.Ordinal);

        Assert.True(highStart >= 0, "High Pressure section not found.");
        Assert.True(mediumStart >= 0, "Medium Pressure section not found.");

        var entry1Pos = s_documentContent.IndexOf("### 1.", StringComparison.Ordinal);
        Assert.True(entry1Pos >= 0, "Entry #1 not found.");

        Assert.True(entry1Pos > highStart && entry1Pos < mediumStart,
            "Entry #1 must appear under the '## High Pressure' section, not under Medium or Low.");
    }

    [Fact]
    public void Entry1_HighPressure_Contains_ActionStepsHeading()
    {
        var entry = ExtractEntry(s_documentContent, 1);
        Assert.False(string.IsNullOrEmpty(entry), "Entry #1 not found.");

        // Entry #1 (High priority) must have an explicit "Action steps:" heading as it is
        // the highest-priority item and benefits from the extra discoverability.
        Assert.Contains("**Action steps:**", entry, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_Purpose_Statement_IsPresent()
    {
        Assert.Contains("**Purpose:**", s_documentContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_HowToUse_Section_IsPresent()
    {
        Assert.Contains("## How to Use This Document", s_documentContent, StringComparison.Ordinal);
    }
}
