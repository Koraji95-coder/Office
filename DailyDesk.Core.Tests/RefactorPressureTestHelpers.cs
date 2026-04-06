using System.Text.RegularExpressions;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Shared helpers for tests that parse and validate Docs/REFACTOR-PRESSURE.md.
/// Centralises document-discovery, entry-parsing, and file-path extraction so
/// that changes to the document format only need to be updated in one place.
/// </summary>
internal static class RefactorPressureTestHelpers
{
    // -----------------------------------------------------------------------
    // Repository discovery
    // -----------------------------------------------------------------------

    /// <summary>
    /// Walks up from the test assembly's base directory to find the repository
    /// root, identified by the presence of <c>DailyDesk/DailyDesk.csproj</c>.
    /// Returns <see langword="null"/> when the sentinel file cannot be found.
    /// </summary>
    internal static string? FindRepoRoot()
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

    // -----------------------------------------------------------------------
    // Document parsing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns one <c>(Number, Title, Body)</c> tuple per active (non-archived)
    /// pressure note.  Active notes are those that appear before the
    /// <c>## Resolved Pressure</c> section; each is identified by a
    /// <c>### N. Title</c> heading.
    /// </summary>
    internal static List<(int Number, string Title, string Body)> ParseActiveEntries(string document)
    {
        // Exclude everything from "## Resolved Pressure" onward.
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

    // -----------------------------------------------------------------------
    // File-path extraction
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts all backtick-wrapped file paths from the <c>| **File** |</c>
    /// or <c>| **Files** |</c> metadata row within an entry body.
    /// </summary>
    internal static List<string> ExtractFilePaths(string entryBody)
    {
        var paths = new List<string>();
        var rowPattern = new Regex(@"\|\s*\*\*Files?\*\*\s*\|\s*(.+?)\s*\|", RegexOptions.IgnoreCase);
        var backtickPattern = new Regex(@"`([^`]+)`");

        foreach (Match row in rowPattern.Matches(entryBody))
            foreach (Match bt in backtickPattern.Matches(row.Groups[1].Value))
                paths.Add(bt.Groups[1].Value.Trim());

        return paths;
    }
}
