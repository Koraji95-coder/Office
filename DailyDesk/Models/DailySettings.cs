using System.IO;
using System.Text.Json;

namespace DailyDesk.Models;

public sealed class DailySettings
{
    public string SuiteRepoPath { get; init; } = @"C:\Users\koraj\OneDrive\Documents\GitHub\Suite";
    public string SuiteRuntimeStatusEndpoint { get; init; } =
        "http://127.0.0.1:5000/api/runtime/status";
    public string OllamaEndpoint { get; init; } = "http://127.0.0.1:11434";
    public string KnowledgeLibraryPath { get; init; } = string.Empty;
    public IReadOnlyList<string> AdditionalKnowledgePaths { get; init; } = Array.Empty<string>();
    public string OfficeName { get; init; } = "Office";
    public string SuiteFocus { get; init; } =
        "Read-only Suite awareness, unified doctor/runtime trust, and developer workshop signals.";
    public string EngineeringFocus { get; init; } =
        "Electrical engineering growth, standards, grounding, protection, review-first technical judgment, and operator-safe reasoning.";
    public string CadFocus { get; init; } =
        "AutoCAD drafting QA, markup-first review, production drawing reliability, and CAD automation that stays human-reviewed.";
    public string BusinessFocus { get; init; } =
        "Internal operating discipline, monetization hypotheses, pilot framing, pricing realism, and measurable operator value.";
    public string CareerFocus { get; init; } =
        "Turn Suite work, EE learning, and CAD workflow judgment into strong career proof and future business leverage.";
    public string ChiefModel { get; init; } = "qwen3:14b";
    public string MentorModel { get; init; } = "ALIENTELLIGENCE/electricalengineerv2:latest";
    public string RepoModel { get; init; } = "qwen2.5-coder:14b";
    public string TrainingModel { get; init; } = "qwen3:14b";
    public string BusinessModel { get; init; } = "gemma3:12b";

    public string ResolveKnowledgeLibraryPath(string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(KnowledgeLibraryPath))
        {
            return KnowledgeLibraryPath;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Knowledge"));
    }

    public IReadOnlyList<string> ResolveAdditionalKnowledgePaths()
    {
        return AdditionalKnowledgePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static DailySettings Load(string baseDirectory)
    {
        var settingsPath = Path.Combine(baseDirectory, "dailydesk.settings.json");
        if (!File.Exists(settingsPath))
        {
            return new DailySettings();
        }

        try
        {
            var payload = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<DailySettings>(
                       payload,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                   )
                   ?? new DailySettings();
        }
        catch
        {
            return new DailySettings();
        }
    }
}
