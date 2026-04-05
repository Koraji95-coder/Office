using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace DailyDesk.Services.Agents;

/// <summary>
/// Chief of Staff agent — routes the day across Suite, engineering, CAD workflow,
/// and business operations.  Tools: get state, list jobs, get schedule.
/// </summary>
public sealed class ChiefOfStaffAgent : DeskAgent
{
    public ChiefOfStaffAgent(Kernel kernel, ILogger? logger = null)
        : base(kernel, logger) { }

    public override string RouteId => OfficeRouteCatalog.ChiefRoute;
    public override string Title => "Chief of Staff";

    public override string SystemPrompt =>
        """
        You are the Chief of Staff inside Office.
        Route the day across Suite, electrical engineering, CAD workflow judgment, and business operations.
        Stay read-only toward Suite.
        Answer the current request directly. Do not recycle old assistant wording when fresher state is provided.
        Respond with short sections named NEXT MOVE, WHY, and HANDOFF.
        """;

    /// <summary>
    /// Returns a brief summary of the current office state suitable for tool invocation.
    /// </summary>
    [KernelFunction("get_office_state")]
    [System.ComponentModel.Description("Get a summary of the current office state including provider, session focus, and daily objective.")]
    public static string GetOfficeState(
        [System.ComponentModel.Description("Current provider label")] string providerLabel,
        [System.ComponentModel.Description("Current session focus")] string sessionFocus,
        [System.ComponentModel.Description("Daily objective")] string dailyObjective)
    {
        return $"Provider: {providerLabel} | Focus: {sessionFocus} | Objective: {dailyObjective}";
    }

    /// <summary>
    /// Returns a summary of recent job activity.
    /// </summary>
    [KernelFunction("list_recent_jobs")]
    [System.ComponentModel.Description("List recent async jobs and their status.")]
    public static string ListRecentJobs(
        [System.ComponentModel.Description("Comma-separated list of recent job summaries")] string jobSummaries)
    {
        return string.IsNullOrWhiteSpace(jobSummaries)
            ? "No recent jobs."
            : $"Recent jobs: {jobSummaries}";
    }

    /// <summary>
    /// Returns the current daily schedule / run objective.
    /// </summary>
    [KernelFunction("get_daily_schedule")]
    [System.ComponentModel.Description("Get the current daily run schedule and objective.")]
    public static string GetDailySchedule(
        [System.ComponentModel.Description("Current daily run objective")] string objective,
        [System.ComponentModel.Description("Current daily run blocks summary")] string blocksSummary)
    {
        return $"Objective: {objective}\nBlocks: {blocksSummary}";
    }
}
