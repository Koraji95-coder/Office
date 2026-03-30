namespace DailyDesk.Models;

public sealed class ResearchSource
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string SearchSnippet { get; init; } = string.Empty;
    public string Extract { get; init; } = string.Empty;

    public string DisplaySummary =>
        string.IsNullOrWhiteSpace(Domain) ? Title : $"{Title} ({Domain})";
}
