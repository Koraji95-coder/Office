using System.Text.Json.Serialization;

namespace DailyDesk.Models;

public sealed class DeskMessageRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeskId { get; set; } = string.Empty;
    public string Role { get; set; } = "assistant";
    public string Author { get; set; } = string.Empty;
    public string Kind { get; set; } = "chat";
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// Optional tool invocation metadata for messages produced by SK agents with tool-calling.
    /// Null for plain text responses (backward compatible).
    /// </summary>
    public List<ToolCallRecord>? ToolCalls { get; set; }

    [JsonIgnore]
    public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsAssistant => !IsUser;

    [JsonIgnore]
    public bool HasToolCalls => ToolCalls is { Count: > 0 };

    [JsonIgnore]
    public string Meta =>
        HasToolCalls
            ? $"{Author} | {CreatedAt:MMM d, h:mm tt} | {ToolCalls!.Count} tool call{(ToolCalls.Count == 1 ? "" : "s")}"
            : $"{Author} | {CreatedAt:MMM d, h:mm tt}";
}

/// <summary>
/// Records a single tool invocation made by an SK agent during message generation.
/// Displayed in the chat view as an expandable card.
/// </summary>
public sealed class ToolCallRecord
{
    /// <summary>
    /// Name of the tool that was invoked (e.g., "GetTrainingHistory", "SearchKnowledge").
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Input arguments passed to the tool, serialized as a display-friendly string.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Result returned by the tool invocation.
    /// </summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// Status of the tool call: "succeeded", "failed", or "skipped".
    /// </summary>
    public string Status { get; set; } = "succeeded";

    /// <summary>
    /// Duration of the tool call in milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Display label for the tool call card in the chat view.
    /// </summary>
    [JsonIgnore]
    public string DisplayLabel => Status switch
    {
        "failed" => $"❌ {ToolName}",
        "skipped" => $"⏭️ {ToolName}",
        _ => $"🔧 {ToolName}",
    };

    /// <summary>
    /// Short summary for the collapsed card view.
    /// </summary>
    [JsonIgnore]
    public string DisplaySummary
    {
        get
        {
            var resultPreview = string.IsNullOrWhiteSpace(Result)
                ? "No output"
                : Result.Length > 120 ? Result[..120] + "…" : Result;
            return DurationMs.HasValue
                ? $"{DisplayLabel} ({DurationMs}ms) — {resultPreview}"
                : $"{DisplayLabel} — {resultPreview}";
        }
    }
}
