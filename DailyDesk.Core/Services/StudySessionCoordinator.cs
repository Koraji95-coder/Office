using DailyDesk.Models;

namespace DailyDesk.Services;

/// <summary>
/// Coordinates study session operations — practice, defense, reflection, and scoring.
/// Encapsulates the domain logic that would otherwise be buried inside
/// <see cref="OfficeBrokerOrchestrator"/>, enabling isolated unit testing of each
/// step without constructing the full orchestrator graph.
/// </summary>
public sealed class StudySessionCoordinator
{
    private readonly TrainingStore _trainingStore;
    private readonly OralDefenseService _oralDefenseService;
    private readonly LearningProfileService _learningProfileService;

    public StudySessionCoordinator(
        TrainingStore trainingStore,
        OralDefenseService oralDefenseService,
        LearningProfileService learningProfileService
    )
    {
        _trainingStore = trainingStore;
        _oralDefenseService = oralDefenseService;
        _learningProfileService = learningProfileService;
    }

    // -------------------------------------------------------------------------
    // Isolated scoring logic
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies <paramref name="answers"/> to the questions in <paramref name="test"/>
    /// and returns the number of correctly answered questions.
    /// Each question's <c>SelectedOptionKey</c> and <c>ResultText</c> are updated
    /// in place so the caller can inspect per-question feedback.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="test"/> is <c>null</c> or has no questions.
    /// </exception>
    public static int ScoreAnswers(
        PracticeTest? test,
        IReadOnlyList<OfficePracticeAnswerInput> answers
    )
    {
        ValidatePracticeTest(test);

        var questions = test!.Questions;

        foreach (var answer in answers)
        {
            if (answer.QuestionIndex < 0 || answer.QuestionIndex >= questions.Count)
            {
                continue;
            }

            questions[answer.QuestionIndex].SelectedOptionKey =
                string.IsNullOrWhiteSpace(answer.SelectedOptionKey)
                    ? string.Empty
                    : answer.SelectedOptionKey.Trim().ToUpperInvariant();
        }

        var correctCount = 0;
        foreach (var question in questions)
        {
            var isCorrect = string.Equals(
                question.SelectedOptionKey?.Trim(),
                question.CorrectOptionKey,
                StringComparison.OrdinalIgnoreCase
            );

            if (isCorrect)
            {
                correctCount++;
                question.ResultText = $"Correct. {question.Explanation}";
                continue;
            }

            var correctOption = question.Options.FirstOrDefault(option =>
                option.Key.Equals(question.CorrectOptionKey, StringComparison.OrdinalIgnoreCase)
            );
            var unanswered = string.IsNullOrWhiteSpace(question.SelectedOptionKey);
            question.ResultText =
                $"{(unanswered ? "Unanswered." : "Incorrect.")} Correct answer: {correctOption?.DisplayLabel ?? question.CorrectOptionKey}. {question.Explanation} Connection: {question.SuiteConnection}";
        }

        return correctCount;
    }

    /// <summary>
    /// Builds a <see cref="TrainingAttemptRecord"/> from a scored practice test.
    /// </summary>
    public static TrainingAttemptRecord BuildAttemptRecord(PracticeTest test, int correctCount)
    {
        return new TrainingAttemptRecord
        {
            Title = test.Title,
            Focus = test.Focus,
            Difficulty = test.Difficulty,
            GenerationSource = test.GenerationSource,
            CompletedAt = DateTimeOffset.Now,
            QuestionCount = test.Questions.Count,
            CorrectCount = correctCount,
            Questions = test.Questions
                .Select(question => new TrainingAttemptQuestionRecord
                {
                    Topic = question.Topic,
                    Difficulty = question.Difficulty,
                    Correct = string.Equals(
                        question.SelectedOptionKey?.Trim(),
                        question.CorrectOptionKey,
                        StringComparison.OrdinalIgnoreCase
                    ),
                })
                .ToList(),
        };
    }

    /// <summary>
    /// Formats the human-readable practice result summary shown in the UI.
    /// </summary>
    public static string BuildPracticeResultSummary(int correctCount, int total)
    {
        var percent = total == 0 ? 0 : (double)correctCount / total;
        return $"{correctCount}/{total} correct ({percent:P0}). Weak topics update has been saved locally.";
    }

    // -------------------------------------------------------------------------
    // Backend service integration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Persists a practice attempt to <see cref="TrainingStore"/> and returns the
    /// updated training history summary.
    /// </summary>
    public Task<TrainingHistorySummary> SavePracticeAttemptAsync(
        TrainingAttemptRecord attempt,
        CancellationToken cancellationToken = default
    ) => _trainingStore.SavePracticeAttemptAsync(attempt, cancellationToken);

    /// <summary>
    /// Persists an oral-defense attempt to <see cref="TrainingStore"/> and returns
    /// the updated training history summary.
    /// </summary>
    public Task<TrainingHistorySummary> SaveDefenseAttemptAsync(
        OralDefenseAttemptRecord attempt,
        CancellationToken cancellationToken = default
    ) => _trainingStore.SaveDefenseAttemptAsync(attempt, cancellationToken);

    /// <summary>
    /// Persists a session reflection to <see cref="TrainingStore"/> and returns
    /// the updated training history summary.
    /// </summary>
    public Task<TrainingHistorySummary> SaveReflectionAsync(
        SessionReflectionRecord record,
        CancellationToken cancellationToken = default
    ) => _trainingStore.SaveReflectionAsync(record, cancellationToken);

    /// <summary>
    /// Rebuilds the <see cref="LearningProfile"/> from the current training history.
    /// </summary>
    public LearningProfile BuildLearningProfile(
        LearningLibrary library,
        TrainingHistorySummary historySummary,
        SuiteSnapshot snapshot
    ) => _learningProfileService.Build(library, historySummary, snapshot);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when <paramref name="test"/>
    /// is <c>null</c> or contains no questions.
    /// </summary>
    public static void ValidatePracticeTest(PracticeTest? test)
    {
        if (test is null || test.Questions.Count == 0)
        {
            throw new InvalidOperationException(
                "No active practice test. Generate practice before scoring."
            );
        }
    }
}
