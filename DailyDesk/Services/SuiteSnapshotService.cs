using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class SuiteSnapshotService
{
    private readonly ProcessRunner _processRunner;
    private readonly HttpClient _httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };
    private static readonly TimeSpan RuntimeStatusTimeout = TimeSpan.FromSeconds(3);
    private readonly string _runtimeStatusEndpoint;

    public SuiteSnapshotService(ProcessRunner processRunner, string runtimeStatusEndpoint)
    {
        _processRunner = processRunner;
        _runtimeStatusEndpoint = runtimeStatusEndpoint;
    }

    public async Task<SuiteSnapshot> LoadAsync(
        string repoPath,
        CancellationToken cancellationToken = default
    )
    {
        if (!Directory.Exists(repoPath))
        {
            return new SuiteSnapshot
            {
                RepoPath = repoPath,
                RepoAvailable = false,
                StatusSummary = "Suite repo was not found at the configured path.",
            };
        }

        var statusTask = TryRunGitAsync(repoPath, "status --short", cancellationToken);
        var logTask = TryRunGitAsync(repoPath, "log --oneline -6", cancellationToken);
        var readmeTask = SafeReadAsync(Path.Combine(repoPath, "README.md"), cancellationToken);
        var workSummaryTask = SafeReadAsync(
            Path.Combine(repoPath, "docs", "development", "work-summary-and-todo.md"),
            cancellationToken
        );
        var monetizationTask = SafeReadAsync(
            Path.Combine(repoPath, "docs", "development", "monetization-readiness-backlog.md"),
            cancellationToken
        );
        var developerToolsManifestTask = SafeReadAsync(
            Path.Combine(repoPath, "src", "routes", "developerToolsManifest.data.json"),
            cancellationToken
        );
        var runtimeSummaryTask = LoadRuntimeSummaryAsync(cancellationToken);

        await Task.WhenAll(
            statusTask,
            logTask,
            readmeTask,
            workSummaryTask,
            monetizationTask,
            developerToolsManifestTask,
            runtimeSummaryTask
        );

        var statusOutput = await statusTask;
        var logOutput = await logTask;
        var readme = await readmeTask;
        var workSummary = await workSummaryTask;
        var monetization = await monetizationTask;
        var developerToolsManifest = await developerToolsManifestTask;
        var runtimeSummary = await runtimeSummaryTask;
        var developerToolsSummary = ParseDeveloperToolsManifest(developerToolsManifest);

        var changedFiles = ParseChangedFiles(statusOutput).Where(path => !IsNoisePath(path)).Take(12).ToList();
        var modifiedCount = statusOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Count(line => !line.StartsWith("?? ") && line.Length >= 4 && !IsNoisePath(line[3..].Trim()));
        var newCount = statusOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.StartsWith("?? ") && line.Length >= 4 && !IsNoisePath(line[3..].Trim()));
        var hotAreas = changedFiles
            .Select(GuessArea)
            .Where(area => !string.IsNullOrWhiteSpace(area))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        return new SuiteSnapshot
        {
            RepoPath = repoPath,
            RepoAvailable = true,
            ModifiedCount = modifiedCount,
            NewCount = newCount,
            StatusSummary = $"{modifiedCount} modified, {newCount} new. Hot areas: {(hotAreas.Count > 0 ? string.Join(", ", hotAreas) : "no dominant hotspots yet")}.",
            ChangedFiles = changedFiles,
            HotAreas = hotAreas,
            RecentCommits = logOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Take(6).ToList(),
            NextSessionTasks = ExtractNumberedItems(workSummary, "## 4) Suggested Next Session Order", 5),
            MonetizationMoves = ExtractBullets(monetization, "That means the first commercial package should be built around:", 5),
            ProductPillars = ExtractBullets(readme, "Suite is a local-first engineering operations workspace that combines:", 4),
            RuntimeStatusAvailable = runtimeSummary.Available,
            RuntimeDoctorState = runtimeSummary.State,
            RuntimeDoctorSummary = runtimeSummary.Summary,
            RuntimeDoctorLeadDetail = runtimeSummary.LeadDetail,
            ActionableIssueCount = runtimeSummary.ActionableIssueCount,
            DeveloperToolGroups = developerToolsSummary.Groups,
            DeveloperTools = developerToolsSummary.Tools,
            WorkshopSummary = developerToolsSummary.Summary,
        };
    }

    private async Task<string> TryRunGitAsync(
        string repoPath,
        string args,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await _processRunner.RunAsync("git", $"-C \"{repoPath}\" {args}", null, cancellationToken);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> SafeReadAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static List<string> ParseChangedFiles(string rawStatus)
    {
        var files = new List<string>();
        foreach (var line in rawStatus.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length >= 4)
            {
                files.Add(line[3..].Trim());
            }
        }

        return files;
    }

    private static bool IsNoisePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("backups/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("output/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("dist/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("node_modules/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".git/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".runlogs/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".env", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("zeroclaw-main/", StringComparison.OrdinalIgnoreCase);
    }

    private static string GuessArea(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("/projects/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Project", StringComparison.OrdinalIgnoreCase))
        {
            return "Projects";
        }

        if (normalized.Contains("/dashboard/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Dashboard", StringComparison.OrdinalIgnoreCase))
        {
            return "Dashboard";
        }

        if (normalized.Contains("/knowledge/", StringComparison.OrdinalIgnoreCase))
        {
            return "Knowledge";
        }

        if (normalized.Contains("AppShell", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("src/App.tsx", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("appShellMeta", StringComparison.OrdinalIgnoreCase))
        {
            return "Shell";
        }

        if (normalized.Contains("/routes/apps/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/components/apps/", StringComparison.OrdinalIgnoreCase))
        {
            return "Apps";
        }

        if (normalized.Contains("/services/", StringComparison.OrdinalIgnoreCase))
        {
            return "Services";
        }

        if (normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase))
        {
            return "Testing";
        }

        if (normalized.Contains("/docs/", StringComparison.OrdinalIgnoreCase))
        {
            return "Docs";
        }

        return "Workspace";
    }

    private static IReadOnlyList<string> ExtractBullets(string markdown, string anchor, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Array.Empty<string>();
        }

        var lines = markdown.Split(Environment.NewLine);
        var start = Array.FindIndex(lines, line => line.Contains(anchor, StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            return Array.Empty<string>();
        }

        var items = new List<string>();
        for (var index = start + 1; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("##", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                items.Add(line[2..].Trim());
            }

            if (items.Count >= maxCount)
            {
                break;
            }
        }

        return items;
    }

    private static IReadOnlyList<string> ExtractNumberedItems(string markdown, string heading, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Array.Empty<string>();
        }

        var lines = markdown.Split(Environment.NewLine);
        var start = Array.FindIndex(lines, line => line.Contains(heading, StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            return Array.Empty<string>();
        }

        var items = new List<string>();
        var matcher = new Regex(@"^\d+\.\s+(.*)$");

        for (var index = start + 1; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("##", StringComparison.Ordinal) && items.Count > 0)
            {
                break;
            }

            var match = matcher.Match(line);
            if (match.Success)
            {
                items.Add(match.Groups[1].Value.Trim());
            }

            if (items.Count >= maxCount)
            {
                break;
            }
        }

        return items;
    }

    private async Task<RuntimeSummary> LoadRuntimeSummaryAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_runtimeStatusEndpoint))
        {
            return RuntimeSummary.Unavailable(
                "Suite runtime endpoint is not configured for Daily Desk."
            );
        }

        try
        {
            using var timeoutScope = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            timeoutScope.CancelAfter(RuntimeStatusTimeout);

            using var response = await _httpClient.GetAsync(
                _runtimeStatusEndpoint,
                timeoutScope.Token
            );
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("doctor", out var doctor))
            {
                return RuntimeSummary.Unavailable(
                    "Suite runtime status responded, but the doctor payload is missing."
                );
            }

            var state = doctor.TryGetProperty("overallState", out var stateElement)
                ? stateElement.GetString() ?? "background"
                : "background";
            var actionableIssueCount =
                doctor.TryGetProperty("actionableIssueCount", out var actionableElement)
                && actionableElement.TryGetInt32(out var actionableValue)
                    ? actionableValue
                    : 0;

            var leadDetail = ExtractRuntimeLeadDetail(root, doctor);
            var summary = actionableIssueCount > 0
                ? $"{actionableIssueCount} actionable issue{(actionableIssueCount == 1 ? string.Empty : "s")} need attention across Runtime Control, backend, and workshop routes."
                : state.Equals("background", StringComparison.OrdinalIgnoreCase)
                    ? "Background checks are still settling, but no actionable runtime drift is active."
                    : "Runtime Control, scripts, and developer routes agree on the current workstation health.";

            return new RuntimeSummary(true, state, summary, leadDetail, actionableIssueCount);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RuntimeSummary.Unavailable(
                $"Suite runtime status timed out after {RuntimeStatusTimeout.TotalSeconds:0} seconds from Daily Desk."
            );
        }
        catch (Exception exception)
        {
            return RuntimeSummary.Unavailable(
                $"Suite runtime status is unavailable from Daily Desk: {exception.Message}"
            );
        }
    }

    private static string ExtractRuntimeLeadDetail(JsonElement root, JsonElement doctor)
    {
        if (doctor.TryGetProperty("recommendations", out var recommendations)
            && recommendations.ValueKind == JsonValueKind.Array)
        {
            var firstRecommendation = recommendations
                .EnumerateArray()
                .Select(item => item.GetString())
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
            if (!string.IsNullOrWhiteSpace(firstRecommendation))
            {
                return firstRecommendation!;
            }
        }

        if (doctor.TryGetProperty("groups", out var groups)
            && groups.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in groups.EnumerateArray())
            {
                if (!group.TryGetProperty("checks", out var checks)
                    || checks.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var check in checks.EnumerateArray())
                {
                    var detail = check.TryGetProperty("detail", out var detailElement)
                        ? detailElement.GetString()
                        : null;
                    var actionable = !check.TryGetProperty("actionable", out var actionableElement)
                        || actionableElement.ValueKind != JsonValueKind.False;
                    var severity = check.TryGetProperty("severity", out var severityElement)
                        ? severityElement.GetString()
                        : null;

                    if (actionable && !string.Equals(severity, "ready", StringComparison.OrdinalIgnoreCase))
                    {
                        return string.IsNullOrWhiteSpace(detail)
                            ? "Suite Doctor found drift that needs review."
                            : detail!;
                    }
                }
            }
        }

        if (root.TryGetProperty("service", out var service)
            && service.TryGetProperty("summary", out var summaryElement))
        {
            var summary = summaryElement.GetString();
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return summary!;
            }
        }

        return "Manual doctor recommendations will appear here when the shared runtime snapshot finds drift.";
    }

    private static DeveloperToolsSummary ParseDeveloperToolsManifest(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new DeveloperToolsSummary(
                Array.Empty<string>(),
                Array.Empty<string>(),
                "Developer workshop signals are not available from the local Suite repo."
            );
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var groups = root.TryGetProperty("groups", out var groupsElement)
                && groupsElement.ValueKind == JsonValueKind.Array
                    ? groupsElement
                        .EnumerateArray()
                        .Select(item => item.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null)
                        .Where(title => !string.IsNullOrWhiteSpace(title))
                        .Select(title => title!)
                        .Take(5)
                        .ToList()
                    : [];

            var tools = root.TryGetProperty("tools", out var toolsElement)
                && toolsElement.ValueKind == JsonValueKind.Array
                    ? toolsElement
                        .EnumerateArray()
                        .Select(item => item.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null)
                        .Where(title => !string.IsNullOrWhiteSpace(title))
                        .Select(title => title!)
                        .Take(8)
                        .ToList()
                    : [];

            var summary = tools.Count == 0
                ? "No developer workshop tools were discovered from the current Suite manifest."
                : $"{tools.Count} workshop tools across {groups.Count} groups are staged in the current Suite developer portal.";

            return new DeveloperToolsSummary(groups, tools, summary);
        }
        catch
        {
            return new DeveloperToolsSummary(
                Array.Empty<string>(),
                Array.Empty<string>(),
                "The local Suite developer tool manifest could not be parsed."
            );
        }
    }

    private sealed record RuntimeSummary(
        bool Available,
        string State,
        string Summary,
        string LeadDetail,
        int ActionableIssueCount
    )
    {
        public static RuntimeSummary Unavailable(string summary) =>
            new(
                false,
                "background",
                summary,
                "Manual doctor recommendations will appear here when the shared runtime snapshot finds drift.",
                0
            );
    }

    private sealed record DeveloperToolsSummary(
        IReadOnlyList<string> Groups,
        IReadOnlyList<string> Tools,
        string Summary
    );
}
