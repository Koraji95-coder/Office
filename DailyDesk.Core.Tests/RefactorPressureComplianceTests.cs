using System.Text.RegularExpressions;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify all refactor pressure notes in REFACTOR-PRESSURE.md
/// are correctly structured, include required metadata, and reference source files
/// that exist in the repository.
///
/// The tests are structured in three groups:
///   1. Document structure tests – verify required top-level sections are present.
///   2. Entry metadata tests – verify every active (non-archived) pressure note
///      contains the required metadata table fields and content sections.
///   3. Source-file linkage tests – verify every file path referenced in the
///      pressure notes resolves to an existing file in the repository.
/// </summary>
public sealed class RefactorPressureComplianceTests
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

    private static string GetRefactorPressurePath()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        return Path.Combine(root!, "Docs", "REFACTOR-PRESSURE.md");
    }

    private static string ReadDocument()
    {
        var path = GetRefactorPressurePath();
        Assert.True(File.Exists(path), $"REFACTOR-PRESSURE.md not found at: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Returns the text of each active (non-archived) pressure note.
    /// Sections are split on "### N." headers and the Resolved Pressure section is excluded.
    /// </summary>
    private static List<(int Number, string Title, string Body)> ParseActiveEntries(string document)
    {
        // Split the document at the "## Resolved Pressure" header to isolate active entries.
        var resolvedIndex = document.IndexOf("## Resolved Pressure", StringComparison.Ordinal);
        var activePart = resolvedIndex >= 0 ? document[..resolvedIndex] : document;

        var entries = new List<(int Number, string Title, string Body)>();

        // Match "### N. Title" headers.
        var headerPattern = new Regex(@"^### (\d+)\.\s+(.+)$", RegexOptions.Multiline);
        var matches = headerPattern.Matches(activePart);

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var number = int.Parse(match.Groups[1].Value);
            var title = match.Groups[2].Value.Trim();

            var bodyStart = match.Index + match.Length;
            var bodyEnd = i + 1 < matches.Count ? matches[i + 1].Index : activePart.Length;
            var body = activePart[bodyStart..bodyEnd];

            entries.Add((number, title, body));
        }

        return entries;
    }

    /// <summary>
    /// Extracts all file paths from metadata table rows that match
    /// "| **File** | `path` |" or "| **Files** | `path`, `path` |".
    /// Handles backtick-wrapped paths and comma-separated multiple paths.
    /// </summary>
    private static List<string> ExtractFilePaths(string entryBody)
    {
        var paths = new List<string>();

        // Match a table row whose first cell is "**File**" or "**Files**".
        var rowPattern = new Regex(
            @"\|\s*\*\*Files?\*\*\s*\|\s*(.+?)\s*\|",
            RegexOptions.IgnoreCase);

        foreach (Match row in rowPattern.Matches(entryBody))
        {
            var cell = row.Groups[1].Value;

            // Extract backtick-delimited path tokens.
            var backtickPattern = new Regex(@"`([^`]+)`");
            foreach (Match bt in backtickPattern.Matches(cell))
                paths.Add(bt.Groups[1].Value.Trim());
        }

        return paths;
    }

    // -----------------------------------------------------------------------
    // Group 1: Document structure tests
    // -----------------------------------------------------------------------

    [Fact]
    public void RefactorPressureDocument_Exists()
    {
        var path = GetRefactorPressurePath();
        Assert.True(File.Exists(path), $"REFACTOR-PRESSURE.md must exist at Docs/REFACTOR-PRESSURE.md; not found at: {path}");
    }

    [Fact]
    public void RefactorPressureDocument_ContainsPurposeStatement()
    {
        var doc = ReadDocument();
        Assert.Contains("Purpose", doc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefactorPressureDocument_ContainsHighPressureSection()
    {
        var doc = ReadDocument();
        Assert.Contains("## High Pressure", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void RefactorPressureDocument_ContainsMediumPressureSection()
    {
        var doc = ReadDocument();
        Assert.Contains("## Medium Pressure", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void RefactorPressureDocument_ContainsLowPressureSection()
    {
        var doc = ReadDocument();
        Assert.Contains("## Low Pressure", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void RefactorPressureDocument_ContainsResolvedPressureArchive()
    {
        var doc = ReadDocument();
        Assert.Contains("## Resolved Pressure", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void RefactorPressureDocument_ResolvedArchive_HasRequiredColumns()
    {
        var doc = ReadDocument();

        // Locate the resolved pressure section.
        var resolvedIndex = doc.IndexOf("## Resolved Pressure", StringComparison.Ordinal);
        Assert.True(resolvedIndex >= 0, "Resolved Pressure section must be present");

        var resolvedSection = doc[resolvedIndex..];

        // The archive table must have Area, Resolved In, and Resolution column headers.
        Assert.Contains("Area", resolvedSection, StringComparison.Ordinal);
        Assert.Contains("Resolved In", resolvedSection, StringComparison.Ordinal);
        Assert.Contains("Resolution", resolvedSection, StringComparison.Ordinal);
    }

    [Fact]
    public void RefactorPressureDocument_ResolvedArchive_HasAtLeastOneEntry()
    {
        var doc = ReadDocument();
        var resolvedIndex = doc.IndexOf("## Resolved Pressure", StringComparison.Ordinal);
        Assert.True(resolvedIndex >= 0, "Resolved Pressure section must be present");

        var resolvedSection = doc[resolvedIndex..];

        // Count table data rows (lines starting with "| " that are not header/separator rows).
        var dataRows = resolvedSection
            .Split('\n')
            .Where(line =>
            {
                var trimmed = line.Trim();
                return trimmed.StartsWith("|", StringComparison.Ordinal)
                    && !trimmed.Contains("---")
                    && !trimmed.Contains("Area")
                    && trimmed.Length > 3;
            })
            .ToList();

        Assert.True(dataRows.Count >= 1,
            "Resolved Pressure archive must contain at least one resolved entry to demonstrate the process works.");
    }

    // -----------------------------------------------------------------------
    // Group 2: Active entry metadata tests
    // -----------------------------------------------------------------------

    [Fact]
    public void RefactorPressureDocument_ActiveEntries_AreNumberedSequentially()
    {
        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        Assert.True(entries.Count >= 1, "REFACTOR-PRESSURE.md must have at least one active pressure note");

        for (var i = 0; i < entries.Count; i++)
        {
            Assert.True(entries[i].Number == i + 1,
                $"Active entry at index {i} must be numbered {i + 1}, found {entries[i].Number}");
        }
    }

    [Fact]
    public void RefactorPressureDocument_ActiveEntries_HaveTitles()
    {
        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        foreach (var (number, title, _) in entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(title),
                $"Entry #{number} must have a non-empty title");
        }
    }

    [Fact]
    public void RefactorPressureDocument_ActiveEntries_HaveFileMetadata()
    {
        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        foreach (var (number, title, body) in entries)
        {
            var hasFileRow = Regex.IsMatch(body, @"\|\s*\*\*Files?\*\*\s*\|", RegexOptions.IgnoreCase);
            Assert.True(hasFileRow,
                $"Entry #{number} '{title}' must include a '| **File** |' or '| **Files** |' metadata row");
        }
    }

    [Fact]
    public void RefactorPressureDocument_ActiveEntries_HavePhaseIntroducedMetadata()
    {
        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        foreach (var (_, _, body) in entries)
        {
            Assert.Contains("**Phase introduced**", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RefactorPressureDocument_ActiveEntries_HaveWhatItDoesNowSection()
    {
        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        foreach (var (_, _, body) in entries)
        {
            Assert.Contains("**What it does now:**", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RefactorPressureDocument_ActiveEntries_HaveWhyItIsUnderPressureSection()
    {
        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        foreach (var (_, _, body) in entries)
        {
            Assert.Contains("**Why it is under pressure:**", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RefactorPressureDocument_ActiveEntries_HaveRefactorDirectionSection()
    {
        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        foreach (var (_, _, body) in entries)
        {
            Assert.Contains("**Refactor direction:**", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RefactorPressureDocument_ActiveEntries_HavePrerequisiteField()
    {
        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        foreach (var (_, _, body) in entries)
        {
            Assert.Contains("**Prerequisite:**", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RefactorPressureDocument_ActiveEntries_WhySection_HasAtLeastOneBulletPoint()
    {
        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        foreach (var (number, title, body) in entries)
        {
            // Locate the "Why it is under pressure" section body.
            var whyIndex = body.IndexOf("**Why it is under pressure:**", StringComparison.Ordinal);
            Assert.True(whyIndex >= 0, $"Entry #{number} must contain a Why section");

            var whyEnd = body.IndexOf("**Refactor direction:**", StringComparison.Ordinal);
            var whyBody = whyEnd > whyIndex
                ? body[whyIndex..whyEnd]
                : body[whyIndex..];

            var hasBullet = whyBody.Contains("\n- ", StringComparison.Ordinal);
            Assert.True(hasBullet,
                $"Entry #{number} '{title}' Why section must contain at least one bullet point (- ...)");
        }
    }

    // -----------------------------------------------------------------------
    // Group 3: Source-file linkage tests
    // -----------------------------------------------------------------------

    [Fact]
    public void RefactorPressureDocument_AllReferencedSourceFiles_ExistInRepository()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);

        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        var missing = new List<string>();

        foreach (var (number, title, body) in entries)
        {
            var filePaths = ExtractFilePaths(body);

            // Every active entry must reference at least one file.
            Assert.True(filePaths.Count >= 1,
                $"Entry #{number} '{title}' must reference at least one source file via backtick-wrapped path in the File metadata row");

            foreach (var relPath in filePaths)
            {
                var fullPath = Path.GetFullPath(Path.Combine(root!, relPath));
                if (!File.Exists(fullPath))
                    missing.Add($"Entry #{number} '{title}': '{relPath}' not found at {fullPath}");
            }
        }

        Assert.True(missing.Count == 0,
            "The following source files referenced in REFACTOR-PRESSURE.md do not exist in the repository:\n"
            + string.Join("\n", missing));
    }

    [Fact]
    public void RefactorPressureDocument_ReferencedFilePaths_AreRelativeToRepoRoot()
    {
        var doc = ReadDocument();
        var entries = ParseActiveEntries(doc);

        foreach (var (number, title, body) in entries)
        {
            var paths = ExtractFilePaths(body);
            foreach (var path in paths)
            {
                // Paths must not be absolute and must not contain '..' traversal.
                Assert.False(Path.IsPathRooted(path),
                    $"Entry #{number} '{title}': file path '{path}' must be relative to the repo root, not absolute");
                Assert.DoesNotContain("..", path, StringComparison.Ordinal);
            }
        }
    }
}
