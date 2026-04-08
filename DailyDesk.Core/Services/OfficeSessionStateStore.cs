using System.IO;
using System.Text.Json;
using DailyDesk.Models;
using LiteDB;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace DailyDesk.Services;

public sealed class OfficeSessionStateStore
{
    private readonly string _storePath;
    private readonly OfficeDatabase? _db;
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public OfficeSessionStateStore(string? stateRootPath = null, OfficeDatabase? db = null)
    {
        var root = string.IsNullOrWhiteSpace(stateRootPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyDesk"
            )
            : Path.GetFullPath(stateRootPath);
        Directory.CreateDirectory(root);
        _storePath = Path.Combine(root, "broker-live-session.json");
        _db = db;

        if (_db is not null)
        {
            MigrateFromJsonIfNeeded();
        }
    }

    public string StorePath => _storePath;

    public async Task<OfficeLiveSessionState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_db is not null)
        {
            var doc = _db.SessionState.FindById("current");
            if (doc is not null)
            {
                var state = BsonMapper.Global.Deserialize<OfficeLiveSessionState>(doc);
                return Normalize(state);
            }
            return Normalize(new OfficeLiveSessionState());
        }

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

        if (_db is not null)
        {
            var doc = BsonMapper.Global.Serialize(normalized);
            doc.AsDocument["_id"] = "current";
            _db.SessionState.Upsert(doc.AsDocument);
            return;
        }

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

    /// <summary>
    /// Migrates existing JSON session state into LiteDB on first run.
    /// Only called from constructor when _db is guaranteed non-null.
    /// </summary>
    private void MigrateFromJsonIfNeeded()
    {
        if (_db is null) return;
        if (_db.HasMigrated("session-state")) return;
        if (!File.Exists(_storePath)) { _db.MarkMigrated("session-state"); return; }

        try
        {
            var json = File.ReadAllText(_storePath);
            var state = JsonSerializer.Deserialize<OfficeLiveSessionState>(json, _jsonOptions)
                ?? new OfficeLiveSessionState();
            var normalized = Normalize(state);
            var doc = BsonMapper.Global.Serialize(normalized);
            doc.AsDocument["_id"] = "current";
            _db.SessionState.Upsert(doc.AsDocument);

            _db.MarkMigrated("session-state");

            var migratedPath = _storePath + ".migrated";
            if (!File.Exists(migratedPath))
            {
                File.Move(_storePath, migratedPath);
            }
        }
        catch
        {
            // Migration failure is non-fatal.
        }
    }

    /// <summary>
    /// Normalizes a <see cref="OfficeLiveSessionState"/> instance by applying default values
    /// and validation rules to every field before the state is returned to callers or persisted.
    /// </summary>
    /// <remarks>
    /// Called after every load (JSON and LiteDB paths), after every save, and during reset.
    /// This ensures the application always operates on a fully-initialized, consistent state
    /// regardless of how the data was persisted or whether fields are missing in older saves.
    ///
    /// Normalization rules applied (in order):
    /// <list type="bullet">
    ///   <item>
    ///     <term><see cref="OfficeLiveSessionState.CurrentRoute"/></term>
    ///     <description>
    ///       Passed through <see cref="OfficeRouteCatalog.NormalizeRoute"/>. Null, empty, or
    ///       unrecognized values are replaced with <see cref="OfficeRouteCatalog.ChiefRoute"/>.
    ///       Casing is normalized to the canonical lowercase route key.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="OfficeLiveSessionState.Focus"/></term>
    ///     <description>
    ///       Trimmed. If null or whitespace, defaults to
    ///       <c>"Protection, grounding, standards, drafting safety"</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="OfficeLiveSessionState.FocusReason"/></term>
    ///     <description>
    ///       Trimmed. If null or whitespace, defaults to
    ///       <c>"Set a focus manually or start from a review target to begin a guided session."</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="OfficeLiveSessionState.Difficulty"/></term>
    ///     <description>
    ///       Trimmed. If null or whitespace, defaults to <c>"Mixed"</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="OfficeLiveSessionState.QuestionCount"/></term>
    ///     <description>
    ///       Clamped to the inclusive range [3, 10] using <see cref="Math.Clamp{T}"/>.
    ///       Values below 3 are raised to 3; values above 10 are lowered to 10.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="OfficeLiveSessionState.ActiveDefenseScenario"/></term>
    ///     <description>
    ///       Null-coalesced to a new empty <see cref="OralDefenseScenario"/> instance,
    ///       ensuring downstream code never encounters a null scenario object.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="OfficeLiveSessionState.PracticeResultSummary"/></term>
    ///     <description>
    ///       Trimmed. If null or whitespace, defaults to <c>"No scored practice yet."</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="OfficeLiveSessionState.DefenseScoreSummary"/></term>
    ///     <description>
    ///       Trimmed. If null or whitespace, defaults to
    ///       <c>"No scored oral-defense answer yet."</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="OfficeLiveSessionState.DefenseFeedbackSummary"/></term>
    ///     <description>
    ///       Trimmed. If null or whitespace, defaults to
    ///       <c>"Score a typed answer to get rubric feedback and follow-up coaching."</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="OfficeLiveSessionState.ReflectionContextSummary"/></term>
    ///     <description>
    ///       Trimmed. If null or whitespace, defaults to
    ///       <c>"Score a practice or defense session to save a reflection."</c>.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="state">The state object to normalize. Modified in place and returned.</param>
    /// <returns>The same <paramref name="state"/> instance after normalization.</returns>
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
