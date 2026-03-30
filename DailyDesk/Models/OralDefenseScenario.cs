namespace DailyDesk.Models;

public sealed class OralDefenseScenario
{
    public string Topic { get; init; } = "electrical production judgment";
    public string Title { get; init; } = "No oral defense loaded.";
    public string Prompt { get; init; } =
        "Generate an oral defense drill to pressure-test reasoning, tradeoffs, and communication.";
    public string WhatGoodLooksLike { get; init; } =
        "A strong response should explain the governing principle, tradeoffs, failure modes, and validation.";
    public string SuiteConnection { get; init; } =
        "Tie the explanation back to operator trust, review gates, or production reliability in Suite.";
    public string GenerationSource { get; init; } = "not generated";
    public IReadOnlyList<string> FollowUpQuestions { get; init; } = Array.Empty<string>();
}
