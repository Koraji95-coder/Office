namespace DailyDesk.Models;

public sealed class AgentCard
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Accent { get; init; } = "#B87333";
    public string Summary { get; init; } = string.Empty;
    public string Focus { get; init; } = string.Empty;
    public string ThreadSummary { get; init; } = string.Empty;
}
