namespace DailyDesk.Models;

public sealed class ResearchReport
{
    public string Query { get; init; } = string.Empty;
    public string Perspective { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Summary { get; init; } = "No live research run yet.";
    public string GenerationSource { get; init; } = "not generated";
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
    public IReadOnlyList<string> KeyTakeaways { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ActionMoves { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ResearchSource> Sources { get; init; } = Array.Empty<ResearchSource>();

    public string RunSummary =>
        $"{GeneratedAt:yyyy-MM-dd HH:mm} | {Sources.Count} sources | {Perspective} | {Model} | {GenerationSource}";
}
