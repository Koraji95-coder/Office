using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace DailyDesk.Services.Agents;

/// <summary>
/// Suite Context agent — keeps the office aware of Suite trust, availability,
/// and workflow context.  Tools: get Suite snapshot, get library docs.
/// </summary>
public sealed class SuiteContextAgent : DeskAgent
{
    public SuiteContextAgent(Kernel kernel, ILogger? logger = null)
        : base(kernel, logger) { }

    public override string RouteId => OfficeRouteCatalog.SuiteRoute;
    public override string Title => "Suite Context";

    public override string SystemPrompt =>
        """
        You are the Suite Context desk inside Office.
        Keep the office aware of Suite trust, availability, and workflow context without turning into a repo-planning tool.
        Stay read-only and avoid implementation proposals unless explicitly asked.
        Prefer current runtime facts over older thread summaries.
        Respond with short sections named CONTEXT, TRUST, and WHY IT MATTERS.
        """;

    /// <summary>
    /// Returns the current Suite snapshot summary.
    /// </summary>
    [KernelFunction("get_suite_snapshot")]
    [System.ComponentModel.Description("Get the current Suite snapshot including status, hot areas, recent commits, and next tasks.")]
    public static string GetSuiteSnapshot(
        [System.ComponentModel.Description("Suite status summary")] string statusSummary,
        [System.ComponentModel.Description("Comma-separated hot areas")] string hotAreas,
        [System.ComponentModel.Description("Comma-separated recent commits")] string recentCommits,
        [System.ComponentModel.Description("Comma-separated next session tasks")] string nextTasks)
    {
        return $"Status: {statusSummary}\nHot areas: {hotAreas}\nRecent commits: {recentCommits}\nNext tasks: {nextTasks}";
    }

    /// <summary>
    /// Returns a summary of the imported learning library documents.
    /// </summary>
    [KernelFunction("get_library_docs")]
    [System.ComponentModel.Description("Get a summary of imported knowledge library documents.")]
    public static string GetLibraryDocs(
        [System.ComponentModel.Description("Comma-separated document summaries")] string documentSummaries)
    {
        return string.IsNullOrWhiteSpace(documentSummaries)
            ? "No library documents imported yet."
            : $"Library documents: {documentSummaries}";
    }

    /// <summary>
    /// Returns Suite trust and awareness context.
    /// </summary>
    [KernelFunction("get_suite_trust")]
    [System.ComponentModel.Description("Get the current Suite trust and awareness status.")]
    public static string GetSuiteTrust(
        [System.ComponentModel.Description("Suite awareness summary")] string awareness,
        [System.ComponentModel.Description("Suite trust summary")] string trust)
    {
        return $"Awareness: {awareness}\nTrust: {trust}";
    }
}
