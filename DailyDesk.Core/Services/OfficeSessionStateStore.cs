using System.IO;
using System.Text.Json;
using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class OfficeSessionStateStore
{
    private readonly string _storePath;
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public OfficeSessionStateStore(string? stateRootPath = null)
    {
        var root = string.IsNullOrWhiteSpace(stateRootPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyDesk"
            )
            : Path.GetFullPath(stateRootPath);
        Directory.CreateDirectory(root);
        _storePath = Path.Combine(root, "broker-live-session.json");
    }

    public string StorePath => _storePath;

    public async Task<OfficeLiveSessionState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storePath))
        {
            return new OfficeLiveSessionState();
        }

        try
        {
            var payload = await File.ReadAllTextAsync(_storePath, cancellationToken);
            var state =
                JsonSerializer.Deserialize<OfficeLiveSessionState>(payload, _jsonOptions)
                ?? new OfficeLiveSessionState();
            return Normalize(state);
        }
        catch
        {
            return new OfficeLiveSessionState();
        }
    }

    public async Task SaveAsync(
        OfficeLiveSessionState state,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = Normalize(state);
        normalized.UpdatedAt = DateTimeOffset.Now;
        var payload = JsonSerializer.Serialize(normalized, _jsonOptions);
        await File.WriteAllTextAsync(_storePath, payload, cancellationToken);
    }

    public async Task<OfficeLiveSessionState> ResetAsync(
        CancellationToken cancellationToken = default
    )
    {
        var state = Normalize(new OfficeLiveSessionState());
        await SaveAsync(state, cancellationToken);
        return state;
    }

    private static OfficeLiveSessionState Normalize(OfficeLiveSessionState state)
    {
        state.CurrentRoute = OfficeRouteCatalog.NormalizeRoute(state.CurrentRoute);
        state.Focus = string.IsNullOrWhiteSpace(state.Focus)
            ? "Protection, grounding, standards, drafting safety"
            : state.Focus.Trim();
        state.FocusReason = string.IsNullOrWhiteSpace(state.FocusReason)
            ? "Set a focus manually or start from a review target to begin a guided session."
            : state.FocusReason.Trim();
        state.Difficulty = string.IsNullOrWhiteSpace(state.Difficulty)
            ? "Mixed"
            : state.Difficulty.Trim();
        state.QuestionCount = Math.Clamp(state.QuestionCount, 3, 10);
        state.ActiveDefenseScenario ??= new OralDefenseScenario();
        state.PracticeResultSummary = string.IsNullOrWhiteSpace(state.PracticeResultSummary)
            ? "No scored practice yet."
            : state.PracticeResultSummary.Trim();
        state.DefenseScoreSummary = string.IsNullOrWhiteSpace(state.DefenseScoreSummary)
            ? "No scored oral-defense answer yet."
            : state.DefenseScoreSummary.Trim();
        state.DefenseFeedbackSummary = string.IsNullOrWhiteSpace(state.DefenseFeedbackSummary)
            ? "Score a typed answer to get rubric feedback and follow-up coaching."
            : state.DefenseFeedbackSummary.Trim();
        state.ReflectionContextSummary = string.IsNullOrWhiteSpace(state.ReflectionContextSummary)
            ? "Score a practice or defense session to save a reflection."
            : state.ReflectionContextSummary.Trim();
        return state;
    }
}
