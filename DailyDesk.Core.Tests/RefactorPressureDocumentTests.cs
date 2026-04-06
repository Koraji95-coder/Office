using System.Text.RegularExpressions;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Unit tests that validate the structure of Docs/REFACTOR-PRESSURE.md.
///
/// Every numbered entry (### N. Title) in that document must contain:
///   1. <strong>Priority</strong>  — placement under a ## High, ## Medium, or ## Low Pressure section.
///   2. <strong>Source file</strong> — a | **File** | or | **Files** | table row.
///   3. <strong>Pressure description</strong> — a "Why it is under pressure:" heading.
///
/// In addition, every repo-relative file path referenced inside a File/Files
/// table row must resolve to a real file in the repository (valid links).
/// </summary>
public sealed class RefactorPressureDocumentTests
{
    private static readonly string RepoRoot =
        FindRepoRoot() ?? throw new InvalidOperationException("Could not locate repository root.");

    private static readonly string DocPath =
        Path.Combine(RepoRoot, "Docs", "REFACTOR-PRESSURE.md");

    private const string PressureDescriptionMarker = "Why it is under pressure";

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

    /// <summary>
    /// Reads REFACTOR-PRESSURE.md and returns one <see cref="PressureEntry"/> per
    /// numbered heading (### N. …).
    /// </summary>
    private static List<PressureEntry> ParseEntries()
    {
        var lines = File.ReadAllLines(DocPath);
        var entries = new List<PressureEntry>();
        string? currentPriority = null;
        PressureEntry? current = null;

        // Matches: ## High Pressure  /  ## Medium Pressure  /  ## Low Pressure
        var priorityHeader = new Regex(
            @"^##\s+(High|Medium|Low)\s+Pressure",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Matches: ### 1. OfficeBrokerOrchestrator …
        var entryHeader = new Regex(@"^###\s+\d+\.", RegexOptions.Compiled);

        foreach (var line in lines)
        {
            var pm = priorityHeader.Match(line);
            if (pm.Success)
            {
                currentPriority = pm.Groups[1].Value;
                continue;
            }

            if (entryHeader.IsMatch(line))
            {
                if (current is not null)
                    entries.Add(current);

                current = new PressureEntry
                {
                    Header = line.Trim(),
                    Priority = currentPriority,
                };
                continue;
            }

            if (current is null)
                continue;

            // Capture the **File** / **Files** table row.
            if (Regex.IsMatch(line, @"\|\s*\*\*Files?\*\*\s*\|"))
                current.FileCell = line;

            // Capture presence of the pressure description heading.
            if (line.Contains(PressureDescriptionMarker, StringComparison.OrdinalIgnoreCase))
                current.HasPressureDescription = true;
        }

        if (current is not null)
            entries.Add(current);

        return entries;
    }

    private sealed class PressureEntry
    {
        public string Header { get; init; } = "";
        public string? Priority { get; set; }
        public string? FileCell { get; set; }
        public bool HasPressureDescription { get; set; }
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public void RefactorPressureDocument_FileExists()
    {
        Assert.True(File.Exists(DocPath),
            $"Expected REFACTOR-PRESSURE.md at: {DocPath}");
    }

    [Fact]
    public void RefactorPressureDocument_HasAtLeastOneEntry()
    {
        var entries = ParseEntries();
        Assert.NotEmpty(entries);
    }

    [Fact]
    public void AllEntries_HavePriority()
    {
        var entries = ParseEntries();

        foreach (var entry in entries)
        {
            Assert.True(
                entry.Priority is not null,
                $"Entry '{entry.Header}' is not under a ## High / ## Medium / ## Low Pressure section.");
        }
    }

    [Fact]
    public void AllEntries_HaveSourceFileField()
    {
        var entries = ParseEntries();

        foreach (var entry in entries)
        {
            Assert.True(
                entry.FileCell is not null,
                $"Entry '{entry.Header}' is missing a '| **File** |' or '| **Files** |' table row.");
        }
    }

    [Fact]
    public void AllEntries_HavePressureDescription()
    {
        var entries = ParseEntries();

        foreach (var entry in entries)
        {
            Assert.True(
                entry.HasPressureDescription,
                $"Entry '{entry.Header}' is missing a 'Why it is under pressure:' section.");
        }
    }

    [Fact]
    public void AllEntries_ReferencedFilesExistInRepository()
    {
        var entries = ParseEntries();

        foreach (var entry in entries)
        {
            if (entry.FileCell is null)
                continue;

            // Extract backtick-quoted tokens and keep only those that look like
            // repo-relative paths (they contain at least one path separator).
            var referencedPaths = Regex.Matches(entry.FileCell, @"`([^`]+)`")
                .Select(m => m.Groups[1].Value)
                .Where(p => p.Contains('/') || p.Contains('\\'))
                .ToList();

            foreach (var relativePath in referencedPaths)
            {
                var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(RepoRoot, normalizedPath);

                Assert.True(
                    File.Exists(fullPath),
                    $"Entry '{entry.Header}' references '{relativePath}' which does not exist at '{fullPath}'.");
            }
        }
    }
}
