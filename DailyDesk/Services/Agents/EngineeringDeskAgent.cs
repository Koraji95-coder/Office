using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace DailyDesk.Services.Agents;

/// <summary>
/// Engineering Desk agent — teaches EE + CAD judgment, practice-test coaching,
/// and oral-defense reasoning.  Tools: get training history, start practice, run defense.
/// </summary>
public sealed class EngineeringDeskAgent : DeskAgent
{
    public EngineeringDeskAgent(Kernel kernel, ILogger? logger = null)
        : base(kernel, logger) { }

    public override string RouteId => OfficeRouteCatalog.EngineeringRoute;
    public override string Title => "Engineering Desk";

    public override string SystemPrompt =>
        """
        You are the Engineering Desk inside Office.
        Combine electrical engineering teaching, CAD workflow judgment, practice-test coaching, and oral-defense reasoning.
        Keep answers practical, operator-safe, and tied to review-first production work.
        Lead with the governing principle, then give one bounded next move.
        Do not mention internal model/provider details unless the user explicitly asks.
        Do not echo stale thread wording when fresher state is provided.
        Respond with short sections named ANSWER, CHECKS, and CAD OR SUITE LINK.
        """;

    /// <summary>
    /// Returns the current training history summary.
    /// </summary>
    [KernelFunction("get_training_history")]
    [System.ComponentModel.Description("Get the training history summary including practice results, defense summary, and weak topics.")]
    public static string GetTrainingHistory(
        [System.ComponentModel.Description("Overall practice summary")] string overallSummary,
        [System.ComponentModel.Description("Review queue summary")] string reviewQueue,
        [System.ComponentModel.Description("Defense summary")] string defenseSummary,
        [System.ComponentModel.Description("Comma-separated weak topics")] string weakTopics)
    {
        return $"Practice: {overallSummary}\nReview queue: {reviewQueue}\nDefense: {defenseSummary}\nWeak topics: {weakTopics}";
    }

    /// <summary>
    /// Returns the current learning profile.
    /// </summary>
    [KernelFunction("get_learning_profile")]
    [System.ComponentModel.Description("Get the current learning profile with summary, current need, and coaching rules.")]
    public static string GetLearningProfile(
        [System.ComponentModel.Description("Learning profile summary")] string summary,
        [System.ComponentModel.Description("Current learning need")] string currentNeed,
        [System.ComponentModel.Description("Comma-separated coaching rules")] string coachingRules)
    {
        return $"Summary: {summary}\nCurrent need: {currentNeed}\nRules: {coachingRules}";
    }

    /// <summary>
    /// Returns imported knowledge context relevant to the current topic.
    /// </summary>
    [KernelFunction("get_knowledge_context")]
    [System.ComponentModel.Description("Get relevant imported knowledge excerpts for a given topic.")]
    public static string GetKnowledgeContext(
        [System.ComponentModel.Description("Relevant knowledge context block")] string knowledgeContext)
    {
        return string.IsNullOrWhiteSpace(knowledgeContext)
            ? "No relevant notebook evidence available."
            : knowledgeContext;
    }
}
