using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace DailyDesk.Services.Agents;

/// <summary>
/// Growth Ops (Business) agent — turns current capability into internal operating moves,
/// pilot-shaped offers, and monetization proof.  Tools: get operator memory, get suggestions.
/// </summary>
public sealed class GrowthOpsAgent : DeskAgent
{
    public GrowthOpsAgent(Kernel kernel, ILogger? logger = null)
        : base(kernel, logger) { }

    public override string RouteId => OfficeRouteCatalog.BusinessRoute;
    public override string Title => "Business Ops";

    public override string SystemPrompt =>
        """
        You are Business Ops inside Office.
        Turn current capability into internal operating moves, pilot-shaped offers, and monetization proof without hype.
        Keep the focus on personal growth, real electrical production-control value, and career proof.
        Avoid generic startup language.
        Respond with short sections named MOVE, WHY IT WINS, and WHAT TO PROVE.
        """;

    /// <summary>
    /// Returns the current operator memory state summary.
    /// </summary>
    [KernelFunction("get_operator_memory")]
    [System.ComponentModel.Description("Get the current operator memory state including daily objective, approval inbox, and monetization leads.")]
    public static string GetOperatorMemory(
        [System.ComponentModel.Description("Daily objective")] string dailyObjective,
        [System.ComponentModel.Description("Approval inbox summary")] string approvalInbox,
        [System.ComponentModel.Description("Comma-separated monetization leads")] string monetizationLeads)
    {
        return $"Daily objective: {dailyObjective}\nApproval inbox: {approvalInbox}\nMonetization leads: {monetizationLeads}";
    }

    /// <summary>
    /// Returns the current suggestions and their status.
    /// </summary>
    [KernelFunction("get_suggestions")]
    [System.ComponentModel.Description("Get current operator suggestions and their status.")]
    public static string GetSuggestions(
        [System.ComponentModel.Description("Comma-separated suggestion summaries")] string suggestionSummaries)
    {
        return string.IsNullOrWhiteSpace(suggestionSummaries)
            ? "No active suggestions."
            : $"Suggestions: {suggestionSummaries}";
    }

    /// <summary>
    /// Returns product pillars and growth context.
    /// </summary>
    [KernelFunction("get_growth_context")]
    [System.ComponentModel.Description("Get product pillars and growth context from the Suite snapshot.")]
    public static string GetGrowthContext(
        [System.ComponentModel.Description("Comma-separated product pillars")] string productPillars,
        [System.ComponentModel.Description("Career progress summary")] string careerProgress)
    {
        return $"Product pillars: {productPillars}\nCareer progress: {careerProgress}";
    }
}
