using System.IO;
using System.Text.Json;
using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class TrainingStore
{
    private readonly string _storePath;
    private readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public TrainingStore(string? stateRootPath = null)
    {
        var root = string.IsNullOrWhiteSpace(stateRootPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DailyDesk"
            )
            : Path.GetFullPath(stateRootPath);
        Directory.CreateDirectory(root);
        _storePath = Path.Combine(root, "training-history.json");
    }

    public string StorePath => _storePath;

    public bool Exists => File.Exists(_storePath);

    public DateTimeOffset? GetLastWriteTime() =>
        File.Exists(_storePath) ? File.GetLastWriteTime(_storePath) : null;

    public async Task<TrainingHistorySummary> LoadSummaryAsync(
        CancellationToken cancellationToken = default
    )
    {
        var payload = await LoadPayloadAsync(cancellationToken);
        return BuildSummary(payload.PracticeAttempts, payload.DefenseAttempts, payload.Reflections);
    }

    public async Task<TrainingHistorySummary> SaveAttemptAsync(
        TrainingAttemptRecord attempt,
        CancellationToken cancellationToken = default
    )
    {
        return await SavePracticeAttemptAsync(attempt, cancellationToken);
    }

    public async Task<TrainingHistorySummary> SavePracticeAttemptAsync(
        TrainingAttemptRecord attempt,
        CancellationToken cancellationToken = default
    )
    {
        var payload = await LoadPayloadAsync(cancellationToken);
        payload.PracticeAttempts.Insert(0, attempt);
        payload.PracticeAttempts = payload.PracticeAttempts
            .OrderByDescending(item => item.CompletedAt)
            .Take(120)
            .ToList();

        await SavePayloadAsync(payload, cancellationToken);
        return BuildSummary(payload.PracticeAttempts, payload.DefenseAttempts, payload.Reflections);
    }

    public async Task<TrainingHistorySummary> SaveDefenseAttemptAsync(
        OralDefenseAttemptRecord attempt,
        CancellationToken cancellationToken = default
    )
    {
        var payload = await LoadPayloadAsync(cancellationToken);
        payload.DefenseAttempts.Insert(0, attempt);
        payload.DefenseAttempts = payload.DefenseAttempts
            .OrderByDescending(item => item.CompletedAt)
            .Take(120)
            .ToList();

        await SavePayloadAsync(payload, cancellationToken);
        return BuildSummary(payload.PracticeAttempts, payload.DefenseAttempts, payload.Reflections);
    }

    public async Task<TrainingHistorySummary> SaveReflectionAsync(
        SessionReflectionRecord reflection,
        CancellationToken cancellationToken = default
    )
    {
        var payload = await LoadPayloadAsync(cancellationToken);
        payload.Reflections.Insert(0, reflection);
        payload.Reflections = payload.Reflections
            .OrderByDescending(item => item.CompletedAt)
            .Take(120)
            .ToList();

        await SavePayloadAsync(payload, cancellationToken);
        return BuildSummary(payload.PracticeAttempts, payload.DefenseAttempts, payload.Reflections);
    }

    public async Task<TrainingHistorySummary> ResetAsync(
        CancellationToken cancellationToken = default
    )
    {
        var payload = new TrainingStorePayload();
        await SavePayloadAsync(payload, cancellationToken);
        return BuildSummary(payload.PracticeAttempts, payload.DefenseAttempts, payload.Reflections);
    }

    private async Task<TrainingStorePayload> LoadPayloadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storePath))
        {
            return new TrainingStorePayload();
        }

        try
        {
            var payload = await File.ReadAllTextAsync(_storePath, cancellationToken);
            var deserialized =
                JsonSerializer.Deserialize<TrainingStorePayload>(payload, _jsonOptions)
                ?? new TrainingStorePayload();
            deserialized.HydrateLegacyPracticeAttempts();
            return deserialized;
        }
        catch
        {
            return new TrainingStorePayload();
        }
    }

    private async Task SavePayloadAsync(
        TrainingStorePayload payload,
        CancellationToken cancellationToken
    )
    {
        payload.Attempts = payload.PracticeAttempts.ToList();
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        await File.WriteAllTextAsync(_storePath, json, cancellationToken);
    }

    private static TrainingHistorySummary BuildSummary(
        IReadOnlyList<TrainingAttemptRecord> practiceAttempts,
        IReadOnlyList<OralDefenseAttemptRecord> defenseAttempts,
        IReadOnlyList<SessionReflectionRecord> reflections
    )
    {
        var totalQuestions = practiceAttempts.Sum(item => item.QuestionCount);
        var correctAnswers = practiceAttempts.Sum(item => item.CorrectCount);
        var topicEvents = practiceAttempts
            .SelectMany(
                attempt =>
                    attempt.Questions.Select(
                        question => new TopicAttemptEvent
                        {
                            Topic = question.Topic,
                            Correct = question.Correct,
                            CompletedAt = attempt.CompletedAt,
                        }
                    )
            )
            .ToList();

        var weakTopics = topicEvents
            .GroupBy(item => item.Topic)
            .Select(group => new TopicMasterySummary
            {
                Topic = group.Key,
                Attempted = group.Count(),
                Correct = group.Count(item => item.Correct),
            })
            .OrderBy(summary => summary.Accuracy)
            .ThenByDescending(summary => summary.Attempted)
            .Take(6)
            .ToList();

        var reviewRecommendations = topicEvents
            .GroupBy(item => item.Topic)
            .Select(
                group =>
                {
                    var attempted = group.Count();
                    var correct = group.Count(item => item.Correct);
                    var accuracy = attempted == 0 ? 0 : (double)correct / attempted;
                    var lastPracticedAt = group.Max(item => item.CompletedAt);
                    var intervalDays = accuracy switch
                    {
                        < 0.5 => 1,
                        < 0.7 => 3,
                        < 0.85 => 7,
                        _ => 14,
                    };
                    var dueAt = lastPracticedAt.AddDays(intervalDays);
                    var priority = dueAt <= DateTimeOffset.Now
                        ? "due now"
                        : dueAt <= DateTimeOffset.Now.AddDays(2)
                            ? "due soon"
                            : "stable";
                    var reason = accuracy < 0.7
                        ? "Low accuracy requires fast reinforcement."
                        : accuracy < 0.85
                            ? "Moderate accuracy needs spaced follow-up."
                            : "Keep the topic warm with maintenance review.";

                    return new ReviewRecommendation
                    {
                        Topic = group.Key,
                        Attempted = attempted,
                        Correct = correct,
                        LastPracticedAt = lastPracticedAt,
                        DueAt = dueAt,
                        Priority = priority,
                        Reason = reason,
                    };
                }
            )
            .OrderBy(item => item.DueAt)
            .ThenBy(item => item.Accuracy)
            .ThenByDescending(item => item.Attempted)
            .Take(8)
            .ToList();

        return new TrainingHistorySummary
        {
            TotalAttempts = practiceAttempts.Count,
            TotalQuestions = totalQuestions,
            CorrectAnswers = correctAnswers,
            WeakTopics = weakTopics,
            RecentAttempts = practiceAttempts
                .OrderByDescending(item => item.CompletedAt)
                .Take(8)
                .ToList(),
            ReviewRecommendations = reviewRecommendations,
            RecentDefenseAttempts = defenseAttempts
                .OrderByDescending(item => item.CompletedAt)
                .Take(8)
                .ToList(),
            RecentReflections = reflections
                .OrderByDescending(item => item.CompletedAt)
                .Take(8)
                .ToList(),
        };
    }

    private sealed class TopicAttemptEvent
    {
        public string Topic { get; init; } = string.Empty;
        public bool Correct { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
    }

    private sealed class TrainingStorePayload
    {
        public List<TrainingAttemptRecord> Attempts { get; set; } = [];
        public List<TrainingAttemptRecord> PracticeAttempts { get; set; } = [];
        public List<OralDefenseAttemptRecord> DefenseAttempts { get; set; } = [];
        public List<SessionReflectionRecord> Reflections { get; set; } = [];

        public void HydrateLegacyPracticeAttempts()
        {
            if (PracticeAttempts.Count == 0 && Attempts.Count > 0)
            {
                PracticeAttempts = Attempts
                    .OrderByDescending(item => item.CompletedAt)
                    .ToList();
            }
        }
    }
}
