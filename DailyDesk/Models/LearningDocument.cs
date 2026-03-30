namespace DailyDesk.Models;

public sealed class LearningDocument
{
    public string SourceRootPath { get; init; } = string.Empty;
    public string SourceRootLabel { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public DateTimeOffset LastUpdated { get; init; }
    public int CharacterCount { get; init; }
    public IReadOnlyList<string> Topics { get; init; } = Array.Empty<string>();
    public string Summary { get; init; } = string.Empty;
    public string ExtractedText { get; init; } = string.Empty;

    public string PromptSummary =>
        $"[{SourceRootLabel}] {RelativePath} ({Kind}) | topics: {string.Join(", ", Topics.Take(4))} | {Summary}";

    public string DisplaySummary =>
        $"[{SourceRootLabel}] {RelativePath} | {Kind} | {CharacterCount} chars | {string.Join(", ", Topics.Take(4))}";
}
