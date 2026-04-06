using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

public sealed class StudySessionCoordinatorTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static PracticeTest BuildTest(int questionCount = 3)
    {
        var questions = Enumerable.Range(0, questionCount)
            .Select(i => new TrainingQuestion
            {
                Topic = $"grounding-{i}",
                Difficulty = "Standard",
                Prompt = $"Question {i}?",
                CorrectOptionKey = "A",
                Explanation = $"Explanation {i}.",
                SuiteConnection = $"Suite connection {i}.",
                Options =
                [
                    new TrainingOption { Key = "A", Text = "Correct answer" },
                    new TrainingOption { Key = "B", Text = "Wrong answer" },
                ],
            })
            .ToList();

        return new PracticeTest
        {
            Title = "Test Practice",
            Focus = "protection",
            Difficulty = "Standard",
            Questions = questions,
        };
    }

    private static OfficePracticeAnswerInput Answer(int index, string key) =>
        new() { QuestionIndex = index, SelectedOptionKey = key };

    // -------------------------------------------------------------------------
    // ValidatePracticeTest
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidatePracticeTest_Null_ThrowsInvalidOperation()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StudySessionCoordinator.ValidatePracticeTest(null)
        );
        Assert.Contains("Generate practice", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePracticeTest_EmptyQuestions_ThrowsInvalidOperation()
    {
        var emptyTest = new PracticeTest { Title = "Empty", Questions = [] };
        var ex = Assert.Throws<InvalidOperationException>(
            () => StudySessionCoordinator.ValidatePracticeTest(emptyTest)
        );
        Assert.Contains("Generate practice", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePracticeTest_WithQuestions_DoesNotThrow()
    {
        var test = BuildTest(1);
        // Should not throw
        StudySessionCoordinator.ValidatePracticeTest(test);
    }

    // -------------------------------------------------------------------------
    // ScoreAnswers — correctness counting
    // -------------------------------------------------------------------------

    [Fact]
    public void ScoreAnswers_AllCorrect_ReturnsFullCount()
    {
        var test = BuildTest(3);
        var answers = new[]
        {
            Answer(0, "A"),
            Answer(1, "A"),
            Answer(2, "A"),
        };

        var correctCount = StudySessionCoordinator.ScoreAnswers(test, answers);

        Assert.Equal(3, correctCount);
    }

    [Fact]
    public void ScoreAnswers_AllWrong_ReturnsZero()
    {
        var test = BuildTest(3);
        var answers = new[]
        {
            Answer(0, "B"),
            Answer(1, "B"),
            Answer(2, "B"),
        };

        var correctCount = StudySessionCoordinator.ScoreAnswers(test, answers);

        Assert.Equal(0, correctCount);
    }

    [Fact]
    public void ScoreAnswers_MixedAnswers_ReturnsCorrectCount()
    {
        var test = BuildTest(3);
        var answers = new[]
        {
            Answer(0, "A"), // correct
            Answer(1, "B"), // wrong
            Answer(2, "A"), // correct
        };

        var correctCount = StudySessionCoordinator.ScoreAnswers(test, answers);

        Assert.Equal(2, correctCount);
    }

    [Fact]
    public void ScoreAnswers_NoAnswersProvided_CountsAllAsUnanswered()
    {
        var test = BuildTest(3);

        var correctCount = StudySessionCoordinator.ScoreAnswers(test, []);

        Assert.Equal(0, correctCount);
    }

    [Fact]
    public void ScoreAnswers_CaseInsensitiveMatch_CountsAsCorrect()
    {
        var test = BuildTest(1);
        var answers = new[] { Answer(0, "a") }; // lowercase "a" should match "A"

        var correctCount = StudySessionCoordinator.ScoreAnswers(test, answers);

        Assert.Equal(1, correctCount);
    }

    [Fact]
    public void ScoreAnswers_OutOfRangeIndex_IsIgnored()
    {
        var test = BuildTest(2);
        var answers = new[]
        {
            Answer(-1, "A"), // below range
            Answer(5, "A"),  // above range
        };

        // Should not throw, no questions answered
        var correctCount = StudySessionCoordinator.ScoreAnswers(test, answers);

        Assert.Equal(0, correctCount);
    }

    // -------------------------------------------------------------------------
    // ScoreAnswers — result text
    // -------------------------------------------------------------------------

    [Fact]
    public void ScoreAnswers_CorrectAnswer_SetsCorrectResultText()
    {
        var test = BuildTest(1);
        var answers = new[] { Answer(0, "A") };

        StudySessionCoordinator.ScoreAnswers(test, answers);

        var resultText = test.Questions[0].ResultText;
        Assert.StartsWith("Correct.", resultText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Explanation 0.", resultText, StringComparison.Ordinal);
    }

    [Fact]
    public void ScoreAnswers_WrongAnswer_SetsIncorrectResultText()
    {
        var test = BuildTest(1);
        var answers = new[] { Answer(0, "B") };

        StudySessionCoordinator.ScoreAnswers(test, answers);

        var resultText = test.Questions[0].ResultText;
        Assert.StartsWith("Incorrect.", resultText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Correct answer:", resultText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScoreAnswers_UnansweredQuestion_SetsUnansweredResultText()
    {
        var test = BuildTest(1);
        // Provide no answers so the question remains unanswered
        StudySessionCoordinator.ScoreAnswers(test, []);

        var resultText = test.Questions[0].ResultText;
        Assert.StartsWith("Unanswered.", resultText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScoreAnswers_WrongAnswer_ResultTextContainsCorrectOptionLabel()
    {
        var test = BuildTest(1);
        var answers = new[] { Answer(0, "B") };

        StudySessionCoordinator.ScoreAnswers(test, answers);

        // "A. Correct answer" is the DisplayLabel of option A
        var resultText = test.Questions[0].ResultText;
        Assert.Contains("A. Correct answer", resultText, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------
    // BuildPracticeResultSummary
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildPracticeResultSummary_AllCorrect_Shows100Percent()
    {
        var summary = StudySessionCoordinator.BuildPracticeResultSummary(3, 3);

        Assert.Contains("3/3", summary, StringComparison.Ordinal);
        Assert.Contains("%", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPracticeResultSummary_ZeroTotal_DoesNotDivideByZero()
    {
        var summary = StudySessionCoordinator.BuildPracticeResultSummary(0, 0);

        Assert.Contains("0/0", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPracticeResultSummary_PartialScore_ContainsRatio()
    {
        var summary = StudySessionCoordinator.BuildPracticeResultSummary(2, 4);

        Assert.Contains("2/4", summary, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------
    // BuildAttemptRecord
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildAttemptRecord_ReflectsTestMetadata()
    {
        var test = BuildTest(2);
        test.Questions[0].SelectedOptionKey = "A"; // correct
        test.Questions[1].SelectedOptionKey = "B"; // wrong

        var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount: 1);

        Assert.Equal(test.Title, attempt.Title);
        Assert.Equal(test.Focus, attempt.Focus);
        Assert.Equal(test.Difficulty, attempt.Difficulty);
        Assert.Equal(2, attempt.QuestionCount);
        Assert.Equal(1, attempt.CorrectCount);
        Assert.Equal(2, attempt.Questions.Count);
    }

    [Fact]
    public void BuildAttemptRecord_QuestionCorrectField_MatchesSelectedOption()
    {
        var test = BuildTest(2);
        test.Questions[0].SelectedOptionKey = "A"; // correct (CorrectOptionKey = "A")
        test.Questions[1].SelectedOptionKey = "B"; // wrong

        var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount: 1);

        Assert.True(attempt.Questions[0].Correct);
        Assert.False(attempt.Questions[1].Correct);
    }

    // -------------------------------------------------------------------------
    // Integration: SavePracticeAttemptAsync persists to TrainingStore
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SavePracticeAttemptAsync_PersistsAttemptAndReturnsSummary()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            // Use null db (JSON-file fallback) to avoid LiteDB parallel-init contention
            var trainingStore = new TrainingStore(tempDir, db: null);
            var coordinator = new StudySessionCoordinator(
                trainingStore,
                new OralDefenseService(new ThrowingModelProvider(), "test-model"),
                new LearningProfileService()
            );

            var test = BuildTest(2);
            var correct = StudySessionCoordinator.ScoreAnswers(test, [Answer(0, "A")]);
            var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correct);

            var summary = await coordinator.SavePracticeAttemptAsync(attempt);

            Assert.Equal(1, summary.TotalAttempts);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SaveReflectionAsync_PersistsReflectionRecord()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            // Use null db (JSON-file fallback) to avoid LiteDB parallel-init contention
            var trainingStore = new TrainingStore(tempDir, db: null);
            var coordinator = new StudySessionCoordinator(
                trainingStore,
                new OralDefenseService(new ThrowingModelProvider(), "test-model"),
                new LearningProfileService()
            );

            var record = new SessionReflectionRecord
            {
                Mode = "Practice",
                Focus = "protection",
                Reflection = "Struggled with grounding rules.",
                CompletedAt = DateTimeOffset.Now,
            };

            var summary = await coordinator.SaveReflectionAsync(record);

            Assert.Single(summary.RecentReflections);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // -------------------------------------------------------------------------
    // LearningProfile rebuild
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildLearningProfile_WithEmptyHistory_ReturnsBaselineProfile()
    {
        var coordinator = new StudySessionCoordinator(
            new TrainingStore(),
            new OralDefenseService(new ThrowingModelProvider(), "test-model"),
            new LearningProfileService()
        );

        var profile = coordinator.BuildLearningProfile(
            new LearningLibrary { Documents = [] },
            new TrainingHistorySummary(),
            new SuiteSnapshot()
        );

        Assert.NotNull(profile);
        Assert.NotEmpty(profile.CurrentNeed);
    }

    // -------------------------------------------------------------------------
    // Private stub
    // -------------------------------------------------------------------------

    private sealed class ThrowingModelProvider : IModelProvider
    {
        public string ProviderId => "throwing-stub";
        public string ProviderLabel => "Throwing Stub";

        public Task<IReadOnlyList<string>> GetInstalledModelsAsync(
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Stub provider unavailable.");

        public Task<string> GenerateAsync(
            string model,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Stub provider unavailable.");

        public Task<T?> GenerateJsonAsync<T>(
            string model,
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Stub provider unavailable.");

        public Task<bool> PingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
