using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DailyDesk.Services;

/// <summary>
/// Base class for desk-specific SK agents.  Each subclass provides a system prompt
/// and registers tool functions that the LLM can invoke during conversation.
/// The agent manages a <see cref="ChatHistory"/> that mirrors the desk thread state
/// and supports context-window management (keep last N, summarise older messages).
/// </summary>
public abstract class DeskAgent
{
    /// <summary>Maximum number of full messages retained before older messages are summarised.</summary>
    protected const int MaxFullMessages = 12;

    /// <summary>Maximum number of messages that trigger a summary of older content.</summary>
    protected const int SummaryThreshold = 16;

    private readonly ILogger _logger;

    protected DeskAgent(Kernel kernel, ILogger? logger = null)
    {
        Kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>The SK kernel wired to the LLM backend.</summary>
    protected Kernel Kernel { get; }

    /// <summary>Route identifier (e.g. "chief", "engineering").</summary>
    public abstract string RouteId { get; }

    /// <summary>Human-readable title for the agent.</summary>
    public abstract string Title { get; }

    /// <summary>System prompt that defines the agent's persona and response format.</summary>
    public abstract string SystemPrompt { get; }

    /// <summary>
    /// Sends a user message through the agent, applying context from the desk thread,
    /// and returns the assistant reply.
    /// </summary>
    public virtual async Task<string> ChatAsync(
        string userMessage,
        IReadOnlyList<DailyDesk.Models.DeskMessageRecord> threadMessages,
        string? threadSummary,
        string contextBlock,
        CancellationToken cancellationToken = default)
    {
        var chatHistory = BuildChatHistory(threadMessages, threadSummary, contextBlock, userMessage);

        try
        {
            var chatService = Kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                kernel: Kernel,
                cancellationToken: cancellationToken);

            return result?.Content?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeskAgent {Route} chat failed, returning empty.", RouteId);
            return string.Empty;
        }
    }

    /// <summary>
    /// Builds a <see cref="ChatHistory"/> from thread messages, optional summary,
    /// contextual information, and the current user message.
    /// </summary>
    protected virtual ChatHistory BuildChatHistory(
        IReadOnlyList<DailyDesk.Models.DeskMessageRecord> threadMessages,
        string? threadSummary,
        string contextBlock,
        string userMessage)
    {
        var history = new ChatHistory();

        // 1. System prompt — agent persona
        history.AddSystemMessage(SystemPrompt);

        // 2. Condensed summary of older conversation (if present)
        if (!string.IsNullOrWhiteSpace(threadSummary))
        {
            history.AddSystemMessage($"Summary of earlier conversation:\n{threadSummary}");
        }

        // 3. Contextual state block (office parameters, suite, training, etc.)
        if (!string.IsNullOrWhiteSpace(contextBlock))
        {
            history.AddSystemMessage($"Current context:\n{contextBlock}");
        }

        // 4. Recent thread messages (keep last MaxFullMessages non-system messages)
        var recentMessages = threadMessages
            .Where(m => !m.Kind.Equals("system", StringComparison.OrdinalIgnoreCase))
            .TakeLast(MaxFullMessages)
            .ToList();

        foreach (var msg in recentMessages)
        {
            if (msg.IsUser)
            {
                history.AddUserMessage(msg.Content);
            }
            else
            {
                history.AddAssistantMessage(msg.Content);
            }
        }

        // 5. Current user message
        history.AddUserMessage(userMessage);

        return history;
    }

    /// <summary>
    /// Generates a summary of older messages that have scrolled out of the context window.
    /// Uses the LLM to produce a concise recap.
    /// </summary>
    public virtual async Task<string?> SummarizeOlderMessagesAsync(
        IReadOnlyList<DailyDesk.Models.DeskMessageRecord> allMessages,
        CancellationToken cancellationToken = default)
    {
        var nonSystem = allMessages
            .Where(m => !m.Kind.Equals("system", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nonSystem.Count <= SummaryThreshold)
        {
            return null; // Not enough messages to warrant summarisation
        }

        // Messages to summarise: everything before the last MaxFullMessages
        var toSummarise = nonSystem.Take(nonSystem.Count - MaxFullMessages).ToList();
        if (toSummarise.Count == 0)
        {
            return null;
        }

        var summaryPrompt = new ChatHistory();
        summaryPrompt.AddSystemMessage(
            "Summarise the following conversation excerpt in 3-5 sentences. " +
            "Focus on key decisions, topics discussed, and action items. " +
            "Keep it factual and concise.");

        var transcript = string.Join("\n",
            toSummarise.Select(m => $"{m.Author}: {Truncate(m.Content, 300)}"));
        summaryPrompt.AddUserMessage(transcript);

        try
        {
            var chatService = Kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatService.GetChatMessageContentAsync(
                summaryPrompt,
                kernel: Kernel,
                cancellationToken: cancellationToken);

            return result?.Content?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeskAgent {Route} summary generation failed.", RouteId);
            return null;
        }
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}
