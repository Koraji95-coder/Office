using System.Text.RegularExpressions;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests for the "Refactor pressure notes" section (chunk 3) of
/// Mockups/Suite-Reboot-Storyboard/architecture-tools-guide.md.
///
/// The tests are organised in three groups:
///   1. Guide document structure — verify the guide file exists and contains
///      the three expected architecture tool sections.
///   2. Chunk-3 accuracy — verify the Refactor pressure notes section itself
///      accurately describes the three required note attributes (source-file
///      linkage, pressure description, suggested next action).
///   3. Cross-validation — verify that every active entry in
///      Docs/REFACTOR-PRESSURE.md satisfies the requirements stated in chunk 3
///      of the guide: a source-file link, a meaningful pressure description,
///      and a non-empty suggested next action.
/// </summary>
public sealed class ArchitectureToolsGuideChunk3Tests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Walks up from the test assembly's base directory to find the repository
    /// root (identified by the presence of DailyDesk/DailyDesk.csproj).
    /// </summary>
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

    private static string GetGuideRoot()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        return root!;
    }

    private static string GetGuidePath()
        => Path.Combine(
            GetGuideRoot(),
            "Mockups",
            "Suite-Reboot-Storyboard",
            "architecture-tools-guide.md");

    private static string ReadGuide()
    {
        var path = GetGuidePath();
        Assert.True(File.Exists(path), $"architecture-tools-guide.md not found at: {path}");
        return File.ReadAllText(path);
    }

    private static string ReadRefactorPressureDoc()
    {
        var path = Path.Combine(GetGuideRoot(), "Docs", "REFACTOR-PRESSURE.md");
        Assert.True(File.Exists(path), $"REFACTOR-PRESSURE.md not found at: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Returns the text of the Refactor pressure notes section from
    /// architecture-tools-guide.md (chunk 3).
    /// </summary>
    private static string ExtractChunk3(string guide)
    {
        var idx = guide.IndexOf("## Refactor pressure notes", StringComparison.OrdinalIgnoreCase);
        Assert.True(idx >= 0, "architecture-tools-guide.md must contain a '## Refactor pressure notes' section");
        return guide[idx..];
    }

    /// <summary>
    /// Returns one (number, title, body) tuple per active (non-archived) pressure
    /// note in REFACTOR-PRESSURE.md.
    /// </summary>
    private static List<(int Number, string Title, string Body)> ParseActiveEntries(string document)
    {
        var resolvedIndex = document.IndexOf("## Resolved Pressure", StringComparison.Ordinal);
        var activePart = resolvedIndex >= 0 ? document[..resolvedIndex] : document;

        var entries = new List<(int Number, string Title, string Body)>();
        var headerPattern = new Regex(@"^### (\d+)\.\s+(.+)$", RegexOptions.Multiline);
        var matches = headerPattern.Matches(activePart);

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var number = int.Parse(match.Groups[1].Value);
            var title = match.Groups[2].Value.Trim();
            var bodyStart = match.Index + match.Length;
            var bodyEnd = i + 1 < matches.Count ? matches[i + 1].Index : activePart.Length;
            entries.Add((number, title, activePart[bodyStart..bodyEnd]));
        }

        return entries;
    }

    /// <summary>
    /// Extracts all backtick-wrapped file paths from a File/Files metadata row.
    /// </summary>
    private static List<string> ExtractFilePaths(string entryBody)
    {
        var paths = new List<string>();
        var rowPattern = new Regex(@"\|\s*\*\*Files?\*\*\s*\|\s*(.+?)\s*\|", RegexOptions.IgnoreCase);
        var backtickPattern = new Regex(@"`([^`]+)`");

        foreach (Match row in rowPattern.Matches(entryBody))
            foreach (Match bt in backtickPattern.Matches(row.Groups[1].Value))
                paths.Add(bt.Groups[1].Value.Trim());

        return paths;
    }

    /// <summary>
    /// Extracts the text that follows a given section marker up to the next
    /// bold-marker section or end of body, and returns it trimmed.
    /// </summary>
    private static string ExtractSectionContent(string body, string sectionMarker, string? endMarker = null)
    {
        var start = body.IndexOf(sectionMarker, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var contentStart = start + sectionMarker.Length;

        var end = endMarker is not null
            ? body.IndexOf(endMarker, contentStart, StringComparison.Ordinal)
            : -1;

        return (end > contentStart ? body[contentStart..end] : body[contentStart..]).Trim();
    }

    // -----------------------------------------------------------------------
    // Group 1: Guide document structure
    // -----------------------------------------------------------------------

    [Fact]
    public void ArchitectureToolsGuide_FileExists()
    {
        var path = GetGuidePath();
        Assert.True(File.Exists(path),
            $"architecture-tools-guide.md must exist at: {path}");
    }

    [Fact]
    public void ArchitectureToolsGuide_ContainsArchitectureMapSection()
    {
        var guide = ReadGuide();
        Assert.Contains("## Architecture Map", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchitectureToolsGuide_ContainsArchitectureGraphSection()
    {
        var guide = ReadGuide();
        Assert.Contains("## Architecture Graph", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchitectureToolsGuide_ContainsRefactorPressureNotesSection()
    {
        var guide = ReadGuide();
        Assert.Contains("## Refactor pressure notes", guide, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Group 2: Chunk-3 accuracy — the guide describes what notes must contain
    // -----------------------------------------------------------------------

    [Fact]
    public void ArchitectureToolsGuide_Chunk3_DescribesSourceFileLinkage()
    {
        var chunk3 = ExtractChunk3(ReadGuide());
        // The section must mention that notes link to a source file.
        Assert.True(
            chunk3.Contains("source file", StringComparison.OrdinalIgnoreCase)
            || chunk3.Contains("link", StringComparison.OrdinalIgnoreCase),
            "Chunk 3 must describe that refactor pressure notes link to a source file.");
    }

    [Fact]
    public void ArchitectureToolsGuide_Chunk3_DescribesPressureDescription()
    {
        var chunk3 = ExtractChunk3(ReadGuide());
        // The section must describe that notes include a description of the pressure.
        Assert.True(
            chunk3.Contains("description", StringComparison.OrdinalIgnoreCase)
            || chunk3.Contains("pressure", StringComparison.OrdinalIgnoreCase),
            "Chunk 3 must describe that refactor pressure notes include a description of the pressure.");
    }

    [Fact]
    public void ArchitectureToolsGuide_Chunk3_DescribesSuggestedNextAction()
    {
        var chunk3 = ExtractChunk3(ReadGuide());
        // The section must describe that notes include a suggested next action.
        Assert.True(
            chunk3.Contains("next action", StringComparison.OrdinalIgnoreCase)
            || chunk3.Contains("suggested", StringComparison.OrdinalIgnoreCase),
            "Chunk 3 must describe that refactor pressure notes include a suggested next action.");
    }

    [Fact]
    public void ArchitectureToolsGuide_Chunk3_IsNotEmpty()
    {
        var chunk3 = ExtractChunk3(ReadGuide());
        Assert.False(string.IsNullOrWhiteSpace(chunk3),
            "The Refactor pressure notes section (chunk 3) must not be empty.");
        Assert.True(chunk3.Length >= 100,
            "The Refactor pressure notes section (chunk 3) must contain a meaningful description (at least 100 characters).");
    }

    // -----------------------------------------------------------------------
    // Group 3: Cross-validation — REFACTOR-PRESSURE.md complies with the guide
    // -----------------------------------------------------------------------

    [Fact]
    public void RefactorPressureNotes_ComplyWith_Guide_SourceFileLinkage()
    {
        var root = GetGuideRoot();
        var doc = ReadRefactorPressureDoc();
        var entries = ParseActiveEntries(doc);

        Assert.True(entries.Count >= 1, "REFACTOR-PRESSURE.md must have at least one active pressure note.");

        var missing = new List<string>();

        foreach (var (number, title, body) in entries)
        {
            var paths = ExtractFilePaths(body);

            if (paths.Count == 0)
            {
                missing.Add($"Entry #{number} '{title}': no source file link found (File/Files metadata row required).");
                continue;
            }

            foreach (var relPath in paths)
            {
                var fullPath = Path.GetFullPath(Path.Combine(root, relPath));
                if (!File.Exists(fullPath))
                    missing.Add($"Entry #{number} '{title}': '{relPath}' does not exist at {fullPath}.");
            }
        }

        Assert.True(missing.Count == 0,
            "The following REFACTOR-PRESSURE.md entries do not satisfy the guide's source-file linkage requirement:\n"
            + string.Join("\n", missing));
    }

    [Fact]
    public void RefactorPressureNotes_ComplyWith_Guide_PressureDescriptionHasMeaningfulContent()
    {
        const string whyMarker = "**Why it is under pressure:**";
        const string nextMarker = "**Refactor direction:**";

        var doc = ReadRefactorPressureDoc();
        var entries = ParseActiveEntries(doc);

        var violations = new List<string>();

        foreach (var (number, title, body) in entries)
        {
            var content = ExtractSectionContent(body, whyMarker, nextMarker);

            if (string.IsNullOrWhiteSpace(content))
            {
                violations.Add($"Entry #{number} '{title}': 'Why it is under pressure' section is empty.");
                continue;
            }

            // Require at least one non-blank word beyond the marker line itself.
            var words = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 5)
                violations.Add($"Entry #{number} '{title}': 'Why it is under pressure' section has too little content ({words.Length} word(s)); expected at least 5.");
        }

        Assert.True(violations.Count == 0,
            "The following REFACTOR-PRESSURE.md entries do not satisfy the guide's pressure-description requirement:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void RefactorPressureNotes_ComplyWith_Guide_SuggestedNextActionHasMeaningfulContent()
    {
        const string refactorMarker = "**Refactor direction:**";
        const string prereqMarker = "**Prerequisite:**";

        var doc = ReadRefactorPressureDoc();
        var entries = ParseActiveEntries(doc);

        var violations = new List<string>();

        foreach (var (number, title, body) in entries)
        {
            var content = ExtractSectionContent(body, refactorMarker, prereqMarker);

            if (string.IsNullOrWhiteSpace(content))
            {
                violations.Add($"Entry #{number} '{title}': 'Refactor direction' section is empty.");
                continue;
            }

            var words = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 5)
                violations.Add($"Entry #{number} '{title}': 'Refactor direction' section has too little content ({words.Length} word(s)); expected at least 5.");
        }

        Assert.True(violations.Count == 0,
            "The following REFACTOR-PRESSURE.md entries do not satisfy the guide's suggested-next-action requirement:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void RefactorPressureNotes_ComplyWith_Guide_WhatItDoesNowHasMeaningfulContent()
    {
        const string whatMarker = "**What it does now:**";
        const string whyMarker = "**Why it is under pressure:**";

        var doc = ReadRefactorPressureDoc();
        var entries = ParseActiveEntries(doc);

        var violations = new List<string>();

        foreach (var (number, title, body) in entries)
        {
            var content = ExtractSectionContent(body, whatMarker, whyMarker);

            if (string.IsNullOrWhiteSpace(content))
            {
                violations.Add($"Entry #{number} '{title}': 'What it does now' section is empty.");
                continue;
            }

            var words = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 5)
                violations.Add($"Entry #{number} '{title}': 'What it does now' section has too little content ({words.Length} word(s)); expected at least 5.");
        }

        Assert.True(violations.Count == 0,
            "The following REFACTOR-PRESSURE.md entries do not satisfy the 'What it does now' completeness requirement:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void RefactorPressureNotes_AllSourceFilePaths_AreRelativeAndTraversalFree()
    {
        var doc = ReadRefactorPressureDoc();
        var entries = ParseActiveEntries(doc);

        var violations = new List<string>();

        foreach (var (number, title, body) in entries)
        {
            foreach (var path in ExtractFilePaths(body))
            {
                if (Path.IsPathRooted(path))
                    violations.Add($"Entry #{number} '{title}': path '{path}' must be relative to the repo root, not absolute.");

                if (path.Contains("..", StringComparison.Ordinal))
                    violations.Add($"Entry #{number} '{title}': path '{path}' must not use '..' path traversal.");
            }
        }

        Assert.True(violations.Count == 0,
            "The following REFACTOR-PRESSURE.md file paths violate the relative-path requirement:\n"
            + string.Join("\n", violations));
    }
}
