using DailyDesk.Models;
using DailyDesk.Services;
using Xunit;

namespace DailyDesk.Core.Tests;

[Collection("CoordinatorTests")]
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
    // Chunk 1: standalone construction
    // -------------------------------------------------------------------------

    [Fact]
    public void StudySessionCoordinator_CanBeConstructedWithoutOrchestrator()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var coordinator = new StudySessionCoordinator(
                new TrainingStore(tempDir, db: null),
                new OralDefenseService(new ThrowingModelProvider(), "test-model"),
                new LearningProfileService()
            );
            Assert.NotNull(coordinator);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // -------------------------------------------------------------------------
    // Integration: SaveDefenseAttemptAsync persists to TrainingStore
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveDefenseAttemptAsync_PersistsAttemptAndReturnsSummary()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var trainingStore = new TrainingStore(tempDir, db: null);
            var coordinator = new StudySessionCoordinator(
                trainingStore,
                new OralDefenseService(new ThrowingModelProvider(), "test-model"),
                new LearningProfileService()
            );

            var defenseAttempt = new OralDefenseAttemptRecord
            {
                Title = "Grounding Defense",
                Topic = "grounding",
                Prompt = "Explain the difference between grounding and bonding.",
                Answer = "Grounding connects equipment to earth; bonding connects conductive parts.",
                GenerationSource = "test",
                CompletedAt = DateTimeOffset.Now,
                TotalScore = 16,
                MaxScore = 20,
                Summary = "Strong understanding of core concepts.",
            };

            var summary = await coordinator.SaveDefenseAttemptAsync(defenseAttempt);

            Assert.Single(summary.RecentDefenseAttempts);
            Assert.Equal("grounding", summary.RecentDefenseAttempts[0].Topic);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SaveDefenseAttemptAsync_MultipleAttempts_AccumulatesHistory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var coordinator = new StudySessionCoordinator(
                new TrainingStore(tempDir, db: null),
                new OralDefenseService(new ThrowingModelProvider(), "test-model"),
                new LearningProfileService()
            );

            for (var i = 0; i < 3; i++)
            {
                await coordinator.SaveDefenseAttemptAsync(new OralDefenseAttemptRecord
                {
                    Title = $"Defense {i}",
                    Topic = $"topic-{i}",
                    Prompt = "Describe protection zone coverage.",
                    Answer = "Sample answer.",
                    CompletedAt = DateTimeOffset.Now,
                    TotalScore = 14 + i,
                    MaxScore = 20,
                    Summary = "Acceptable.",
                });
            }

            var finalSummary = await new TrainingStore(tempDir, db: null).LoadSummaryAsync();
            Assert.Equal(3, finalSummary.RecentDefenseAttempts.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // -------------------------------------------------------------------------
    // Integration: full practice workflow round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PracticeWorkflow_ScoreAndSave_ReflectsSummaryCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var coordinator = new StudySessionCoordinator(
                new TrainingStore(tempDir, db: null),
                new OralDefenseService(new ThrowingModelProvider(), "test-model"),
                new LearningProfileService()
            );

            var test = BuildTest(4);
            var answers = new[]
            {
                Answer(0, "A"), // correct
                Answer(1, "B"), // wrong
                Answer(2, "A"), // correct
                Answer(3, "A"), // correct
            };

            var correctCount = StudySessionCoordinator.ScoreAnswers(test, answers);
            Assert.Equal(3, correctCount);

            var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount);
            var summary = await coordinator.SavePracticeAttemptAsync(attempt);

            Assert.Equal(1, summary.TotalAttempts);
            Assert.Equal(4, summary.TotalQuestions);
            Assert.Equal(3, summary.CorrectAnswers);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // -------------------------------------------------------------------------
    // Integration: scoring workflow — ScoreAnswers → BuildAttemptRecord → summary
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ScoringWorkflow_PerfectScore_SummaryAndResultSummaryAreConsistent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var coordinator = new StudySessionCoordinator(
                new TrainingStore(tempDir, db: null),
                new OralDefenseService(new ThrowingModelProvider(), "test-model"),
                new LearningProfileService()
            );

            var test = BuildTest(5);
            var answers = Enumerable.Range(0, 5).Select(i => Answer(i, "A")).ToArray();

            var correctCount = StudySessionCoordinator.ScoreAnswers(test, answers);
            var resultSummary = StudySessionCoordinator.BuildPracticeResultSummary(correctCount, test.Questions.Count);
            var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount);
            var historySummary = await coordinator.SavePracticeAttemptAsync(attempt);

            // Scoring, summary text, and persisted history must all agree on correctCount
            Assert.Equal(5, correctCount);
            Assert.Contains("5/5", resultSummary, StringComparison.Ordinal);
            Assert.Equal(5, historySummary.CorrectAnswers);
            Assert.Equal(1, historySummary.TotalAttempts);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScoringWorkflow_ZeroScore_SummaryAndHistoryAgree()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var coordinator = new StudySessionCoordinator(
                new TrainingStore(tempDir, db: null),
                new OralDefenseService(new ThrowingModelProvider(), "test-model"),
                new LearningProfileService()
            );

            var test = BuildTest(3);
            // Answer all wrong
            var answers = Enumerable.Range(0, 3).Select(i => Answer(i, "B")).ToArray();

            var correctCount = StudySessionCoordinator.ScoreAnswers(test, answers);
            var resultSummary = StudySessionCoordinator.BuildPracticeResultSummary(correctCount, test.Questions.Count);
            var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correctCount);
            var historySummary = await coordinator.SavePracticeAttemptAsync(attempt);

            Assert.Equal(0, correctCount);
            Assert.Contains("0/3", resultSummary, StringComparison.Ordinal);
            Assert.Equal(0, historySummary.CorrectAnswers);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // -------------------------------------------------------------------------
    // Integration: combined practice + defense + reflection workflow
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CombinedWorkflow_PracticeDefenseReflection_AllPersistIndependently()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var trainingStore = new TrainingStore(tempDir, db: null);
            var coordinator = new StudySessionCoordinator(
                trainingStore,
                new OralDefenseService(new ThrowingModelProvider(), "test-model"),
                new LearningProfileService()
            );

            // Practice
            var test = BuildTest(2);
            var correct = StudySessionCoordinator.ScoreAnswers(test, [Answer(0, "A"), Answer(1, "B")]);
            var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correct);
            await coordinator.SavePracticeAttemptAsync(attempt);

            // Defense
            await coordinator.SaveDefenseAttemptAsync(new OralDefenseAttemptRecord
            {
                Title = "Protection Defense",
                Topic = "protection",
                Prompt = "Describe arc-flash boundaries.",
                Answer = "Limited, restricted, and prohibited approach boundaries...",
                CompletedAt = DateTimeOffset.Now,
                TotalScore = 18,
                MaxScore = 20,
                Summary = "Excellent response.",
            });

            // Reflection
            await coordinator.SaveReflectionAsync(new SessionReflectionRecord
            {
                Mode = "Defense",
                Focus = "protection",
                Reflection = "Need to review arc-flash labels more carefully.",
                CompletedAt = DateTimeOffset.Now,
            });

            var finalSummary = await trainingStore.LoadSummaryAsync();
            Assert.Equal(1, finalSummary.TotalAttempts);
            Assert.Single(finalSummary.RecentDefenseAttempts);
            Assert.Single(finalSummary.RecentReflections);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // -------------------------------------------------------------------------
    // LearningProfile rebuild — with history
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BuildLearningProfile_AfterPractice_ReturnsPersonalizedProfile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"coord-test-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var trainingStore = new TrainingStore(tempDir, db: null);
            var coordinator = new StudySessionCoordinator(
                trainingStore,
                new OralDefenseService(new ThrowingModelProvider(), "test-model"),
                new LearningProfileService()
            );

            // Score a test so history is non-empty
            var test = BuildTest(2);
            var correct = StudySessionCoordinator.ScoreAnswers(test, [Answer(0, "A"), Answer(1, "B")]);
            var attempt = StudySessionCoordinator.BuildAttemptRecord(test, correct);
            var historySummary = await coordinator.SavePracticeAttemptAsync(attempt);

            var profile = coordinator.BuildLearningProfile(
                new LearningLibrary { Documents = [] },
                historySummary,
                new SuiteSnapshot()
            );

            Assert.NotNull(profile);
            Assert.NotEmpty(profile.CurrentNeed);
            Assert.NotEmpty(profile.CoachingRules);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
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
