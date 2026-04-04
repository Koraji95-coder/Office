using System.IO;
using System.Text;
using DailyDesk.Models;

namespace DailyDesk.Services;

public sealed class OfficeBrokerOrchestrator
{
    private static readonly TimeSpan InstalledModelsLoadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SuiteSnapshotLoadTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan TrainingHistoryLoadTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LearningLibraryLoadTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan OperatorMemoryLoadTimeout = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly OfficeBrokerRuntimeMetadata _brokerMetadata;
    private readonly DailySettings _settings;
    private readonly string _officeRootPath;
    private readonly string _knowledgeLibraryPath;
    private readonly string _stateRootPath;
    private readonly IReadOnlyList<string> _additionalKnowledgePaths;
    private readonly IReadOnlyList<StudyTrack> _studyTracks = BuildDefaultStudyTracks();

    private readonly IModelProvider _modelProvider;
    private readonly SuiteSnapshotService _suiteSnapshotService;
    private readonly TrainingGeneratorService _trainingGeneratorService;
    private readonly TrainingStore _trainingStore;
    private readonly KnowledgeImportService _knowledgeImportService;
    private readonly LearningProfileService _learningProfileService;
    private readonly OralDefenseService _oralDefenseService;
    private readonly LiveResearchService _liveResearchService;
    private readonly OperatorMemoryStore _operatorMemoryStore;
    private readonly OfficeSessionStateStore _sessionStore;
    private readonly MLAnalyticsService _mlAnalyticsService;

    private bool _initialized;
    private DateTimeOffset _lastRefreshAt = DateTimeOffset.Now;
    private IReadOnlyList<string> _installedModelCache = Array.Empty<string>();
    private SuiteSnapshot _suiteSnapshot = new();
    private TrainingHistorySummary _trainingHistorySummary = new();
    private LearningLibrary _learningLibrary = new();
    private LearningProfile _learningProfile = new();
    private OperatorMemoryState _operatorMemoryState = new();
    private OfficeLiveSessionState _session = new();
    private MLAnalyticsResult? _latestMLAnalytics;
    private MLForecastResult? _latestMLForecast;
    private MLEmbeddingsResult? _latestMLEmbeddings;
    private string? _lastMLArtifactExportPath;
    private DateTimeOffset? _lastMLRunAt;

    public OfficeBrokerOrchestrator(OfficeBrokerRuntimeMetadata brokerMetadata)
    {
        _brokerMetadata = brokerMetadata;
        _officeRootPath = ResolveOfficeRootPath(AppContext.BaseDirectory);
        var settingsRoot = Path.Combine(_officeRootPath, "DailyDesk");
        _settings = DailySettings.Load(settingsRoot);
        _knowledgeLibraryPath = _settings.ResolveKnowledgeLibraryPath(settingsRoot);
        _stateRootPath = _settings.ResolveStateRootPath(settingsRoot);
        Directory.CreateDirectory(_knowledgeLibraryPath);
        Directory.CreateDirectory(_stateRootPath);
        _additionalKnowledgePaths = _settings.ResolveAdditionalKnowledgePaths();

        var processRunner = new ProcessRunner();
        _modelProvider = new OllamaService(_settings.OllamaEndpoint, processRunner);
        _suiteSnapshotService = new SuiteSnapshotService(
            processRunner,
            _settings.SuiteRuntimeStatusEndpoint
        );
        _trainingGeneratorService = new TrainingGeneratorService(
            _modelProvider,
            _settings.TrainingModel
        );
        _trainingStore = new TrainingStore(_stateRootPath);
        _knowledgeImportService = new KnowledgeImportService(
            processRunner,
            Path.Combine(_officeRootPath, "DailyDesk", "Scripts", "extract_document_text.py")
        );
        _learningProfileService = new LearningProfileService();
        _oralDefenseService = new OralDefenseService(_modelProvider, _settings.MentorModel);
        _liveResearchService = new LiveResearchService(_modelProvider);
        _operatorMemoryStore = new OperatorMemoryStore(_stateRootPath);
        _sessionStore = new OfficeSessionStateStore(_stateRootPath);
        _mlAnalyticsService = new MLAnalyticsService(
            processRunner,
            Path.Combine(_officeRootPath, "DailyDesk", "Scripts")
        );
    }

    public async Task<object> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            return new
            {
                status = "ok",
                broker = _brokerMetadata.BaseUrl,
                provider = _modelProvider.ProviderId,
                providerReady = _installedModelCache.Count > 0,
                routes = OfficeRouteCatalog.KnownRoutes,
                refreshedAt = _lastRefreshAt,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OfficeBrokerState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            return BuildStateLocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<OfficeChatThread>> GetChatThreadsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            return BuildChatThreadsLocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> SetChatRouteAsync(
        string? route,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            _session.CurrentRoute = OfficeRouteCatalog.NormalizeRoute(route);
            await SaveSessionLockedAsync(cancellationToken);
            return _session.CurrentRoute;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DeskMessageRecord> SendChatAsync(
        string prompt,
        string? routeOverride = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required.", nameof(prompt));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var route = OfficeRouteCatalog.NormalizeRoute(routeOverride ?? _session.CurrentRoute);
            _session.CurrentRoute = route;
            var routeTitle = OfficeRouteCatalog.ResolveRouteTitle(route);
            var routeModel = ResolveDeskModel(route);
            var userPrompt = prompt.Trim();
            var userMessage = new DeskMessageRecord
            {
                DeskId = route,
                Role = "user",
                Author = "You",
                Kind = "chat",
                Content = userPrompt,
                CreatedAt = DateTimeOffset.Now,
            };
            await AppendDeskMessagesLockedAsync(route, userMessage, cancellationToken);

            string response;
            var deterministicResponse = TryBuildDeterministicDeskResponseLocked(route, userPrompt);
            if (!string.IsNullOrWhiteSpace(deterministicResponse))
            {
                response = deterministicResponse;
            }
            else
            {
                try
                {
                    response = await _modelProvider.GenerateAsync(
                        routeModel,
                        BuildDeskSystemPrompt(route),
                        BuildDeskConversationPromptLocked(route, userPrompt),
                        cancellationToken
                    );
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        response = BuildDeskFallbackResponse(route, userPrompt);
                    }
                }
                catch
                {
                    response = BuildDeskFallbackResponse(route, userPrompt);
                }
            }

            var assistantMessage = new DeskMessageRecord
            {
                DeskId = route,
                Role = "assistant",
                Author = routeTitle,
                Kind = "chat",
                Content = response.Trim(),
                CreatedAt = DateTimeOffset.Now,
            };
            await AppendDeskMessagesLockedAsync(route, assistantMessage, cancellationToken);
            await RecordActivityLockedAsync(
                "desk_chat",
                routeTitle,
                route,
                Truncate(assistantMessage.Content, 220),
                cancellationToken
            );
            await SaveSessionLockedAsync(cancellationToken);
            return assistantMessage;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TrainingSessionState> StartStudyAsync(
        string? focus,
        string? difficulty,
        int? questionCount,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            ResetSessionProgressLocked(
                string.IsNullOrWhiteSpace(focus) ? _session.Focus : focus.Trim(),
                "Manual focus selected for the next guided session.",
                difficulty,
                questionCount
            );
            await SaveSessionLockedAsync(cancellationToken);
            return BuildTrainingSessionStateLocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PracticeTest> GeneratePracticeAsync(
        string? focus,
        string? difficulty,
        int? questionCount,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var resolvedFocus = string.IsNullOrWhiteSpace(focus)
                ? _session.Focus
                : focus.Trim();
            var resolvedDifficulty = string.IsNullOrWhiteSpace(difficulty)
                ? _session.Difficulty
                : difficulty.Trim();
            var resolvedQuestionCount = Math.Clamp(
                questionCount ?? _session.QuestionCount,
                3,
                10
            );
            var focusReason = string.IsNullOrWhiteSpace(focus)
                ? "Practice linked to the current guided session."
                : "Manual focus chosen for the guided training session.";

            var practiceTest = await _trainingGeneratorService.CreatePracticeTestAsync(
                resolvedFocus,
                resolvedDifficulty,
                resolvedQuestionCount,
                _suiteSnapshot,
                _trainingHistorySummary,
                _learningProfile,
                _learningLibrary,
                _studyTracks,
                cancellationToken
            );

            _session.Focus = resolvedFocus;
            _session.Difficulty = resolvedDifficulty;
            _session.QuestionCount = resolvedQuestionCount;
            _session.FocusReason = focusReason;
            _session.ActivePracticeTest = practiceTest;
            _session.PracticeGenerated = true;
            _session.PracticeScored = false;
            _session.DefenseGenerated = false;
            _session.DefenseScored = false;
            _session.ReflectionSaved = false;
            _session.PracticeResultSummary =
                $"Generated {practiceTest.Questions.Count} questions for '{resolvedFocus}' at {resolvedDifficulty} difficulty.";
            _session.LastScoredSessionMode = string.Empty;
            _session.LastScoredSessionFocus = string.Empty;
            _session.ReflectionContextSummary =
                "Score a practice or defense session to save a reflection.";
            await SaveSessionLockedAsync(cancellationToken);
            await RecordActivityLockedAsync(
                "practice_generated",
                "Test Builder",
                resolvedFocus,
                _session.PracticeResultSummary,
                cancellationToken
            );
            return practiceTest;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TrainingAttemptRecord> ScorePracticeAsync(
        IReadOnlyList<OfficePracticeAnswerInput> answers,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var practiceTest = _session.ActivePracticeTest;
            if (practiceTest is null || practiceTest.Questions.Count == 0)
            {
                throw new InvalidOperationException(
                    "No active practice test. Generate practice before scoring."
                );
            }

            foreach (var answer in answers)
            {
                if (answer.QuestionIndex < 0 || answer.QuestionIndex >= practiceTest.Questions.Count)
                {
                    continue;
                }

                var selected = string.IsNullOrWhiteSpace(answer.SelectedOptionKey)
                    ? string.Empty
                    : answer.SelectedOptionKey.Trim().ToUpperInvariant();
                practiceTest.Questions[answer.QuestionIndex].SelectedOptionKey = selected;
            }

            var correctCount = 0;
            foreach (var question in practiceTest.Questions)
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

            var attempt = new TrainingAttemptRecord
            {
                Title = practiceTest.Title,
                Focus = practiceTest.Focus,
                Difficulty = practiceTest.Difficulty,
                GenerationSource = practiceTest.GenerationSource,
                CompletedAt = DateTimeOffset.Now,
                QuestionCount = practiceTest.Questions.Count,
                CorrectCount = correctCount,
                Questions = practiceTest.Questions
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

            _trainingHistorySummary = await _trainingStore.SaveAttemptAsync(attempt, cancellationToken);
            _learningProfile = _learningProfileService.Build(
                _learningLibrary,
                _trainingHistorySummary,
                _suiteSnapshot
            );

            var percent = practiceTest.Questions.Count == 0
                ? 0
                : (double)correctCount / practiceTest.Questions.Count;
            _session.PracticeGenerated = true;
            _session.PracticeScored = true;
            _session.Focus = practiceTest.Focus;
            _session.PracticeResultSummary =
                $"{correctCount}/{practiceTest.Questions.Count} correct ({percent:P0}). Weak topics update has been saved locally.";
            _session.LastScoredSessionMode = "Practice";
            _session.LastScoredSessionFocus = practiceTest.Focus;
            _session.ReflectionSaved = false;
            _session.ReflectionContextSummary =
                $"Reflect on practice for {practiceTest.Focus}. Capture what felt weak, what to review next, and any tie-in to Suite or career progress.";
            await SaveSessionLockedAsync(cancellationToken);
            await RecordActivityLockedAsync(
                "practice_scored",
                "Test Builder",
                practiceTest.Focus,
                _session.PracticeResultSummary,
                cancellationToken
            );

            return attempt;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OralDefenseScenario> GenerateDefenseAsync(
        string? topic,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var preferredTopic = string.IsNullOrWhiteSpace(topic) ? _session.Focus : topic.Trim();
            var scenario = await _oralDefenseService.CreateScenarioAsync(
                _suiteSnapshot,
                _trainingHistorySummary,
                _learningProfile,
                _learningLibrary,
                _studyTracks,
                preferredTopic,
                cancellationToken
            );

            _session.ActiveDefenseScenario = scenario;
            _session.Focus = string.IsNullOrWhiteSpace(scenario.Topic)
                ? preferredTopic
                : scenario.Topic;
            _session.DefenseGenerated = true;
            _session.DefenseScored = false;
            _session.ReflectionSaved = false;
            _session.DefenseAnswerDraft = string.Empty;
            _session.LastDefenseEvaluation = null;
            _session.DefenseScoreSummary = "No scored oral-defense answer yet.";
            _session.DefenseFeedbackSummary =
                "Score a typed answer to get rubric feedback and follow-up coaching.";
            await SaveSessionLockedAsync(cancellationToken);
            await RecordActivityLockedAsync(
                "defense_generated",
                "EE Mentor",
                _session.Focus,
                scenario.Title,
                cancellationToken
            );
            return scenario;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OralDefenseAttemptRecord> ScoreDefenseAsync(
        string answer,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new ArgumentException("Answer is required.", nameof(answer));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var scenario = _session.ActiveDefenseScenario ?? new OralDefenseScenario();
            var trimmedAnswer = answer.Trim();
            var evaluation = await _oralDefenseService.ScoreResponseAsync(
                scenario,
                trimmedAnswer,
                _suiteSnapshot,
                _learningProfile,
                _learningLibrary,
                cancellationToken
            );

            var followUps = evaluation.RecommendedFollowUps.Count == 0
                ? scenario.FollowUpQuestions
                : evaluation.RecommendedFollowUps;
            var topic = string.IsNullOrWhiteSpace(scenario.Topic)
                ? _session.Focus
                : scenario.Topic.Trim();

            var attempt = new OralDefenseAttemptRecord
            {
                Title = scenario.Title,
                Topic = string.IsNullOrWhiteSpace(topic) ? "electrical production judgment" : topic,
                Prompt = scenario.Prompt,
                Answer = trimmedAnswer,
                GenerationSource = scenario.GenerationSource,
                CompletedAt = DateTimeOffset.Now,
                TotalScore = evaluation.TotalScore,
                MaxScore = evaluation.MaxScore,
                Summary = evaluation.Summary,
                NextReviewRecommendation = evaluation.NextReviewRecommendation,
                RubricItems = evaluation.RubricItems.ToList(),
                FollowUpQuestions = followUps.ToList(),
            };

            _trainingHistorySummary = await _trainingStore.SaveDefenseAttemptAsync(
                attempt,
                cancellationToken
            );
            _learningProfile = _learningProfileService.Build(
                _learningLibrary,
                _trainingHistorySummary,
                _suiteSnapshot
            );

            _session.DefenseGenerated = true;
            _session.DefenseScored = true;
            _session.ReflectionSaved = false;
            _session.Focus = attempt.Topic;
            _session.DefenseAnswerDraft = trimmedAnswer;
            _session.LastDefenseEvaluation = evaluation;
            _session.DefenseScoreSummary = evaluation.DisplaySummary;
            _session.DefenseFeedbackSummary = BuildDefenseFeedbackSummary(evaluation);
            _session.LastScoredSessionMode = "Defense";
            _session.LastScoredSessionFocus = attempt.Topic;
            _session.ReflectionContextSummary =
                $"Reflect on defense for {attempt.Topic}. Capture what felt weak, what to review next, and any tie-in to Suite or career progress.";
            await SaveSessionLockedAsync(cancellationToken);
            await RecordActivityLockedAsync(
                "defense_scored",
                "EE Mentor",
                attempt.Topic,
                _session.DefenseScoreSummary,
                cancellationToken
            );

            return attempt;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SessionReflectionRecord> SaveReflectionAsync(
        string reflection,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(reflection))
        {
            throw new ArgumentException("Reflection is required.", nameof(reflection));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var record = new SessionReflectionRecord
            {
                Mode = string.IsNullOrWhiteSpace(_session.LastScoredSessionMode)
                    ? "Session"
                    : _session.LastScoredSessionMode,
                Focus = string.IsNullOrWhiteSpace(_session.LastScoredSessionFocus)
                    ? _session.Focus
                    : _session.LastScoredSessionFocus,
                Reflection = reflection.Trim(),
                CompletedAt = DateTimeOffset.Now,
            };

            _trainingHistorySummary = await _trainingStore.SaveReflectionAsync(record, cancellationToken);
            _learningProfile = _learningProfileService.Build(
                _learningLibrary,
                _trainingHistorySummary,
                _suiteSnapshot
            );

            _session.ReflectionSaved = true;
            _session.LastReflection = record;
            _session.ReflectionContextSummary =
                $"Saved reflection for {record.Mode.ToLowerInvariant()} on {record.Focus}.";
            await SaveSessionLockedAsync(cancellationToken);
            await RecordActivityLockedAsync(
                "reflection_saved",
                "Chief of Staff",
                record.Focus,
                record.Reflection,
                cancellationToken
            );

            return record;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ResearchReport> RunResearchAsync(
        string query,
        string? perspective,
        bool saveToLibrary,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            return await RunResearchCoreLockedAsync(
                query,
                perspective,
                saveToLibrary,
                cancellationToken
            );
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ResearchReport> RunWatchlistAsync(
        string watchlistId,
        bool? saveToLibraryOverride = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(watchlistId))
        {
            throw new ArgumentException("Watchlist id is required.", nameof(watchlistId));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var watchlist = _operatorMemoryState.Watchlists.FirstOrDefault(item =>
                item.Id.Equals(watchlistId, StringComparison.OrdinalIgnoreCase)
            );
            if (watchlist is null)
            {
                throw new InvalidOperationException($"Watchlist '{watchlistId}' was not found.");
            }

            if (!watchlist.IsEnabled)
            {
                throw new InvalidOperationException(
                    $"Watchlist '{watchlist.Topic}' is disabled and cannot be run."
                );
            }

            var report = await RunResearchCoreLockedAsync(
                watchlist.Query,
                watchlist.PreferredPerspective,
                saveToLibraryOverride ?? watchlist.SaveToKnowledgeDefault,
                cancellationToken
            );

            var updatedWatchlists = _operatorMemoryState.Watchlists
                .Select(item => new ResearchWatchlist
                {
                    Id = item.Id,
                    Topic = item.Topic,
                    Query = item.Query,
                    Frequency = item.Frequency,
                    PreferredPerspective = item.PreferredPerspective,
                    SaveToKnowledgeDefault = item.SaveToKnowledgeDefault,
                    IsEnabled = item.IsEnabled,
                    LastRunAt = item.Id.Equals(watchlist.Id, StringComparison.OrdinalIgnoreCase)
                        ? report.GeneratedAt
                        : item.LastRunAt,
                })
                .ToList();

            _operatorMemoryState = await _operatorMemoryStore.SaveWatchlistsAsync(
                updatedWatchlists,
                cancellationToken
            );

            await RecordActivityLockedAsync(
                "watchlist_run",
                watchlist.PreferredPerspective,
                watchlist.Topic,
                report.RunSummary,
                cancellationToken
            );

            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> SaveLatestResearchAsync(
        string? notes = null,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            var report = _session.LatestResearchReport;
            if (report is null || report.Sources.Count == 0)
            {
                throw new InvalidOperationException("No live research report is available to save.");
            }

            var filePath = await PersistResearchMarkdownLockedAsync(
                report,
                notes,
                reloadKnowledge: true,
                cancellationToken
            );
            await RecordActivityLockedAsync(
                "research_saved",
                report.Perspective,
                report.Query,
                string.IsNullOrWhiteSpace(notes) ? filePath : $"{filePath} | notes captured",
                cancellationToken
            );
            return filePath;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ResearchReport> RunResearchCoreLockedAsync(
        string query,
        string? perspective,
        bool saveToLibrary,
        CancellationToken cancellationToken
    )
    {
        var resolvedPerspective = string.IsNullOrWhiteSpace(perspective)
            ? OfficeRouteCatalog.ResolvePerspective(_session.CurrentRoute)
            : perspective.Trim();
        var model = ResolveResearchModel(resolvedPerspective);
        var resolvedQuery = string.IsNullOrWhiteSpace(query)
            ? "electrical drawing QA workflows review gates automation"
            : query.Trim();

        var report = await _liveResearchService.RunAsync(
            resolvedQuery,
            resolvedPerspective,
            model,
            _suiteSnapshot,
            _trainingHistorySummary,
            _learningProfile,
            _learningLibrary,
            cancellationToken
        );

        _session.LatestResearchReport = report;
        await SaveSessionLockedAsync(cancellationToken);

        if (saveToLibrary)
        {
            await PersistResearchMarkdownLockedAsync(
                report,
                notes: null,
                reloadKnowledge: true,
                cancellationToken
            );
        }

        var suggestions = BuildResearchSuggestions(
            report,
            resolvedPerspective,
            ResolvePolicyRequiresApproval(resolvedPerspective)
        );
        if (suggestions.Count > 0)
        {
            _operatorMemoryState = await _operatorMemoryStore.UpsertSuggestionsAsync(
                suggestions,
                cancellationToken
            );
            _operatorMemoryState = await AutoStageSelfServeSuggestionsLockedAsync(
                suggestions,
                cancellationToken
            );
        }

        await RecordActivityLockedAsync(
            "research_run",
            resolvedPerspective,
            report.Query,
            report.RunSummary,
            cancellationToken
        );

        return report;
    }

    public async Task<OfficeInboxSection> GetInboxAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            return BuildInboxSectionLocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SuggestedAction> ResolveSuggestionAsync(
        string suggestionId,
        string status,
        string? reason,
        string? note,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(suggestionId))
        {
            throw new ArgumentException("Suggestion id is required.", nameof(suggestionId));
        }

        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? "deferred"
            : status.Trim().ToLowerInvariant();
        if (normalizedStatus is not ("accepted" or "deferred" or "rejected"))
        {
            throw new ArgumentException("Status must be accepted, deferred, or rejected.", nameof(status));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            var suggestion = ResolveSuggestionByIdLocked(suggestionId);
            if (suggestion is null)
            {
                throw new InvalidOperationException($"Suggestion '{suggestionId}' was not found.");
            }

            var outcomeReason = string.IsNullOrWhiteSpace(reason)
                ? normalizedStatus switch
                {
                    "accepted" => "Accepted from inbox.",
                    "rejected" => "Rejected from inbox.",
                    _ => "Deferred from inbox.",
                }
                : reason.Trim();

            if (suggestion.RequiresApproval && string.IsNullOrWhiteSpace(outcomeReason))
            {
                throw new InvalidOperationException(
                    "A short reason is required for approval-gated suggestions."
                );
            }

            var outcome = new SuggestionOutcome
            {
                Status = normalizedStatus,
                Reason = outcomeReason,
                OutcomeNote = string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim(),
                RecordedAt = DateTimeOffset.Now,
            };
            _operatorMemoryState = await _operatorMemoryStore.RecordSuggestionOutcomeAsync(
                suggestion.Id,
                outcome,
                cancellationToken
            );
            await RecordActivityLockedAsync(
                $"suggestion_{normalizedStatus}",
                suggestion.SourceAgent,
                suggestion.Title,
                outcome.DisplaySummary,
                cancellationToken
            );

            var updated = ResolveSuggestionByIdLocked(suggestion.Id);
            return updated ?? suggestion;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SuggestedAction> QueueSuggestionAsync(
        string suggestionId,
        bool approveFirst,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(suggestionId))
        {
            throw new ArgumentException("Suggestion id is required.", nameof(suggestionId));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            var suggestion = ResolveSuggestionByIdLocked(suggestionId);
            if (suggestion is null)
            {
                throw new InvalidOperationException($"Suggestion '{suggestionId}' was not found.");
            }

            if (suggestion.RequiresApproval && suggestion.IsPending && !approveFirst)
            {
                throw new InvalidOperationException(
                    "Approve this suggestion before queueing it, or set approveFirst=true."
                );
            }

            if (suggestion.RequiresApproval && suggestion.IsPending)
            {
                var acceptedOutcome = new SuggestionOutcome
                {
                    Status = "accepted",
                    Reason = "Approved and queued from inbox.",
                    OutcomeNote = string.Empty,
                    RecordedAt = DateTimeOffset.Now,
                };
                _operatorMemoryState = await _operatorMemoryStore.RecordSuggestionOutcomeAsync(
                    suggestion.Id,
                    acceptedOutcome,
                    cancellationToken
                );
                await RecordActivityLockedAsync(
                    "suggestion_accepted",
                    suggestion.SourceAgent,
                    suggestion.Title,
                    acceptedOutcome.DisplaySummary,
                    cancellationToken
                );
            }
            else if (!suggestion.RequiresApproval && suggestion.IsPending)
            {
                var selfServeOutcome = new SuggestionOutcome
                {
                    Status = "accepted",
                    Reason = "Queued from suggestions.",
                    OutcomeNote = string.Empty,
                    RecordedAt = DateTimeOffset.Now,
                };
                _operatorMemoryState = await _operatorMemoryStore.RecordSuggestionOutcomeAsync(
                    suggestion.Id,
                    selfServeOutcome,
                    cancellationToken
                );
            }

            _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionExecutionAsync(
                suggestion.Id,
                "queued",
                suggestion.ActionType.Equals("research_followup", StringComparison.OrdinalIgnoreCase)
                    ? "Running research follow-through."
                    : "Queued for follow-through.",
                cancellationToken
            );
            await RecordActivityLockedAsync(
                "suggestion_queued",
                suggestion.SourceAgent,
                suggestion.Title,
                suggestion.ActionType.Equals("research_followup", StringComparison.OrdinalIgnoreCase)
                    ? "Running research follow-through."
                    : "Queued for follow-through.",
                cancellationToken
            );

            var updated = ResolveSuggestionByIdLocked(suggestion.Id);
            if (updated is not null &&
                updated.ActionType.Equals("research_followup", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteResearchFollowThroughLockedAsync(updated, cancellationToken);
            }

            return updated ?? suggestion;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OfficeLibraryImportResult> ImportLibraryFilesAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var importedPaths = new List<string>();
            var skippedPaths = new List<string>();
            var targetDirectory = Path.Combine(_knowledgeLibraryPath, "Class Notes");
            Directory.CreateDirectory(targetDirectory);

            foreach (var sourcePath in paths)
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    skippedPaths.Add(sourcePath);
                    continue;
                }

                var fileName = Path.GetFileName(sourcePath);
                var targetPath = GetUniqueKnowledgeImportPath(targetDirectory, fileName);
                File.Copy(sourcePath, targetPath, overwrite: false);
                importedPaths.Add(targetPath);
            }

            _learningLibrary = await _knowledgeImportService.LoadAsync(
                _knowledgeLibraryPath,
                _additionalKnowledgePaths,
                cancellationToken
            );
            _learningProfile = _learningProfileService.Build(
                _learningLibrary,
                _trainingHistorySummary,
                _suiteSnapshot
            );
            await RecordActivityLockedAsync(
                "library_import",
                "Chief of Staff",
                "knowledge library",
                $"{importedPaths.Count} file(s) imported.",
                cancellationToken
            );

            return new OfficeLibraryImportResult
            {
                ImportedCount = importedPaths.Count,
                ImportedPaths = importedPaths,
                SkippedPaths = skippedPaths,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OfficeBrokerState> ResetLocalHistoryAsync(
        bool clearTrainingHistory,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            _operatorMemoryState = await _operatorMemoryStore.ResetAsync(cancellationToken);
            _session = await _sessionStore.ResetAsync(cancellationToken);
            _trainingHistorySummary = clearTrainingHistory
                ? await _trainingStore.ResetAsync(cancellationToken)
                : await _trainingStore.LoadSummaryAsync(cancellationToken);
            _learningProfile = _learningProfileService.Build(
                _learningLibrary,
                _trainingHistorySummary,
                _suiteSnapshot
            );
            _lastRefreshAt = DateTimeOffset.Now;

            return BuildStateLocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OfficeBrokerState> ResetWorkspaceAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            ResetKnowledgeLibraryRoot();
            _operatorMemoryState = await _operatorMemoryStore.ResetAsync(cancellationToken);
            _session = await _sessionStore.ResetAsync(cancellationToken);
            _trainingHistorySummary = await _trainingStore.ResetAsync(cancellationToken);
            _learningLibrary = await _knowledgeImportService.LoadAsync(
                _knowledgeLibraryPath,
                _additionalKnowledgePaths,
                cancellationToken
            );
            _learningProfile = _learningProfileService.Build(
                _learningLibrary,
                _trainingHistorySummary,
                _suiteSnapshot
            );
            _lastRefreshAt = DateTimeOffset.Now;

            return BuildStateLocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureInitializedLockedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await RefreshContextLockedAsync(cancellationToken);
        _initialized = true;
    }

    private async Task RefreshContextLockedAsync(CancellationToken cancellationToken)
    {
        var installedModelsTask = LoadInstalledModelsSafeAsync(cancellationToken);
        var suiteSnapshotTask = LoadSuiteSnapshotSafeAsync(cancellationToken);
        var historySummaryTask = LoadTrainingHistorySummarySafeAsync(cancellationToken);
        var learningLibraryTask = LoadLearningLibrarySafeAsync(cancellationToken);
        var operatorMemoryTask = LoadOperatorMemoryStateSafeAsync(cancellationToken);
        var sessionTask = _sessionStore.LoadAsync(cancellationToken);

        await Task.WhenAll(
            installedModelsTask,
            suiteSnapshotTask,
            historySummaryTask,
            learningLibraryTask,
            operatorMemoryTask,
            sessionTask
        );

        _installedModelCache = await installedModelsTask;
        _suiteSnapshot = await suiteSnapshotTask;
        _trainingHistorySummary = await historySummaryTask;
        _learningLibrary = await learningLibraryTask;
        _operatorMemoryState = await operatorMemoryTask;
        _session = await sessionTask;
        _learningProfile = _learningProfileService.Build(
            _learningLibrary,
            _trainingHistorySummary,
            _suiteSnapshot
        );
        NormalizeSessionLocked();
        if (NormalizeHistoricalStateLocked())
        {
            _operatorMemoryState = await _operatorMemoryStore.SaveSnapshotAsync(
                _operatorMemoryState,
                cancellationToken
            );
        }
        _lastRefreshAt = DateTimeOffset.Now;
    }

    private async Task<IReadOnlyList<string>> LoadInstalledModelsSafeAsync(
        CancellationToken cancellationToken
    )
    {
        return await RunWithTimeoutFallbackAsync(
            token => _modelProvider.GetInstalledModelsAsync(token),
            InstalledModelsLoadTimeout,
            static () => Array.Empty<string>(),
            cancellationToken
        );
    }

    private async Task<SuiteSnapshot> LoadSuiteSnapshotSafeAsync(CancellationToken cancellationToken)
    {
        return await RunWithTimeoutFallbackAsync(
            token => _suiteSnapshotService.LoadAsync(_settings.SuiteRepoPath, token),
            SuiteSnapshotLoadTimeout,
            BuildSuiteSnapshotTimeoutFallback,
            cancellationToken
        );
    }

    private async Task<TrainingHistorySummary> LoadTrainingHistorySummarySafeAsync(
        CancellationToken cancellationToken
    )
    {
        return await RunWithTimeoutFallbackAsync(
            token => _trainingStore.LoadSummaryAsync(token),
            TrainingHistoryLoadTimeout,
            static () => new TrainingHistorySummary(),
            cancellationToken
        );
    }

    private async Task<LearningLibrary> LoadLearningLibrarySafeAsync(
        CancellationToken cancellationToken
    )
    {
        return await RunWithTimeoutFallbackAsync(
            token => _knowledgeImportService.LoadAsync(
                _knowledgeLibraryPath,
                _additionalKnowledgePaths,
                token
            ),
            LearningLibraryLoadTimeout,
            BuildLearningLibraryTimeoutFallback,
            cancellationToken
        );
    }

    private async Task<OperatorMemoryState> LoadOperatorMemoryStateSafeAsync(
        CancellationToken cancellationToken
    )
    {
        return await RunWithTimeoutFallbackAsync(
            token => _operatorMemoryStore.LoadAsync(token),
            OperatorMemoryLoadTimeout,
            static () => new OperatorMemoryState(),
            cancellationToken
        );
    }

    private static async Task<T> RunWithTimeoutFallbackAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        Func<T> fallbackFactory,
        CancellationToken cancellationToken
    )
    {
        using var timeoutScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutScope.CancelAfter(timeout);
        try
        {
            return await operation(timeoutScope.Token);
        }
        catch
        {
            return fallbackFactory();
        }
    }

    private SuiteSnapshot BuildSuiteSnapshotTimeoutFallback()
    {
        return new SuiteSnapshot
        {
            RepoPath = _settings.SuiteRepoPath,
            RepoAvailable = Directory.Exists(_settings.SuiteRepoPath),
            StatusSummary =
                $"Suite awareness timed out after {SuiteSnapshotLoadTimeout.TotalSeconds:0} seconds during Office refresh.",
            RuntimeDoctorSummary =
                "Suite runtime status is currently unavailable from Office because the refresh timed out.",
            RuntimeDoctorLeadDetail =
                "Office kept loading with local context so the desk stays usable. Retry later.",
        };
    }

    private LearningLibrary BuildLearningLibraryTimeoutFallback()
    {
        var sourceRoots = new[] { _knowledgeLibraryPath }
            .Concat(_additionalKnowledgePaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .ToList();

        return new LearningLibrary
        {
            RootPath = _knowledgeLibraryPath,
            Exists = Directory.Exists(_knowledgeLibraryPath),
            SourceRoots = sourceRoots,
        };
    }

    private void NormalizeSessionLocked()
    {
        _session.CurrentRoute = OfficeRouteCatalog.NormalizeRoute(_session.CurrentRoute);
        _session.Focus = string.IsNullOrWhiteSpace(_session.Focus)
            ? "Protection, grounding, standards, drafting safety"
            : _session.Focus.Trim();
        _session.FocusReason = string.IsNullOrWhiteSpace(_session.FocusReason)
            ? "Set a focus manually or start from a review target to begin a guided session."
            : _session.FocusReason.Trim();
        _session.Difficulty = string.IsNullOrWhiteSpace(_session.Difficulty)
            ? "Mixed"
            : _session.Difficulty.Trim();
        _session.QuestionCount = Math.Clamp(_session.QuestionCount, 3, 10);
        _session.ActiveDefenseScenario ??= new OralDefenseScenario();
    }

    private bool NormalizeHistoricalStateLocked()
    {
        var unifiedBaselineModel = ResolveUnifiedBaselineModelLocked();
        if (string.IsNullOrWhiteSpace(unifiedBaselineModel))
        {
            return false;
        }

        return OfficeHistoricalStateNormalizer.NormalizeBaselineAssertions(
            _operatorMemoryState,
            unifiedBaselineModel
        );
    }

    private OfficeBrokerState BuildStateLocked()
    {
        var chatThreads = BuildChatThreadsLocked();
        var currentRoute = OfficeRouteCatalog.NormalizeRoute(_session.CurrentRoute);
        var trainingSession = BuildTrainingSessionStateLocked();
        var inbox = BuildInboxSectionLocked();

        return new OfficeBrokerState
        {
            GeneratedAt = DateTimeOffset.Now,
            Broker = new OfficeBrokerStatusSection
            {
                Status = "ok",
                Host = _brokerMetadata.Host,
                Port = _brokerMetadata.Port,
                BaseUrl = _brokerMetadata.BaseUrl,
                LoopbackOnly = _brokerMetadata.LoopbackOnly,
                StartedAt = _brokerMetadata.StartedAt,
                LastRefreshAt = _lastRefreshAt,
            },
            Provider = new OfficeProviderSection
            {
                ActiveProviderId = _modelProvider.ProviderId,
                ActiveProviderLabel = _modelProvider.ProviderLabel,
                PrimaryProviderLabel = _modelProvider.ProviderLabel,
                ConfiguredProviderId = string.IsNullOrWhiteSpace(_settings.PrimaryModelProvider)
                    ? OllamaService.OllamaProviderId
                    : _settings.PrimaryModelProvider,
                Ready = _installedModelCache.Count > 0,
                InstalledModelCount = _installedModelCache.Count,
                InstalledModels = _installedModelCache,
                RoleModels = BuildProviderRoleModelsLocked(),
                EnableHuggingFaceCatalog = _settings.EnableHuggingFaceCatalog,
                HuggingFaceCatalogUrl = _settings.HuggingFaceMcpUrl,
                HuggingFaceTokenEnvVar = _settings.HuggingFaceTokenEnvVar,
            },
            Suite = new OfficeSuiteSection
            {
                Snapshot = _suiteSnapshot,
                Pulse = BuildQuietSuiteContextSummary(_suiteSnapshot),
                TrustSummary = BuildQuietSuiteTrustSummary(_suiteSnapshot),
                SnapshotLoadedAt = _lastRefreshAt,
            },
            Chat = new OfficeChatSection
            {
                CurrentRoute = currentRoute,
                CurrentRouteTitle = OfficeRouteCatalog.ResolveRouteDisplayTitle(currentRoute),
                ActiveThreadId = chatThreads.FirstOrDefault(thread =>
                    thread.Id.Equals(currentRoute, StringComparison.OrdinalIgnoreCase)
                )?.Id ?? chatThreads.FirstOrDefault()?.Id ?? currentRoute,
                RouteReason = BuildRouteReasonLocked(currentRoute),
                RouteOptions = BuildRouteOptionsLocked(),
                SuggestedMoves = BuildSuggestedMovesLocked(),
                SuiteContext = BuildSuiteContextSignalsLocked(),
                Transcript = BuildTranscriptLocked(chatThreads, currentRoute),
                Threads = chatThreads,
            },
            Study = new OfficeStudySection
            {
                Session = trainingSession,
                Focus = _session.Focus,
                Difficulty = _session.Difficulty,
                QuestionCount = _session.QuestionCount,
                ActivePracticeTest = _session.ActivePracticeTest,
                PracticeQuestions = _session.ActivePracticeTest?.Questions ?? Array.Empty<TrainingQuestion>(),
                PracticeResultSummary = _session.PracticeResultSummary,
                ActiveDefenseScenario = _session.ActiveDefenseScenario,
                LastDefenseEvaluation = _session.LastDefenseEvaluation,
                DefenseScoreSummary = _session.DefenseScoreSummary,
                DefenseFeedbackSummary = _session.DefenseFeedbackSummary,
                ReflectionContextSummary = _session.ReflectionContextSummary,
                PracticePrompt = _session.ActivePracticeTest?.Questions.FirstOrDefault()?.Prompt ?? string.Empty,
                DefensePrompt = _session.ActiveDefenseScenario?.Prompt ?? string.Empty,
                LatestScore = BuildLatestScoreSummaryLocked(),
                LatestReflection = _session.LastReflection?.DisplaySummary
                    ?? _trainingHistorySummary.ReflectionSummary,
                Hints = BuildStudyHintsLocked(),
                Sequence = BuildStudySequenceLocked(),
                History = _trainingHistorySummary,
            },
            Research = new OfficeResearchSection
            {
                LatestReport = _session.LatestResearchReport,
                Summary = _session.LatestResearchReport?.Summary
                    ?? "Run a live research query to pull current web sources into the desk.",
                RunSummary = _session.LatestResearchReport?.RunSummary ?? "No live research run yet.",
                History = BuildResearchHistoryLocked(),
            },
            Library = new OfficeLibrarySection
            {
                Summary = _learningLibrary.Documents.Count == 0
                    ? "Office library is blank. Import notes, references, or reviewed source material to begin."
                    : _learningLibrary.Summary,
                TotalDocumentCount = _learningLibrary.Documents.Count,
                Roots = BuildLibraryRootsLocked(),
                Documents = BuildLibraryDocumentsLocked(),
                Library = _learningLibrary,
                Profile = _learningProfile,
            },
            Growth = new OfficeGrowthSection
            {
                DailyRun = _operatorMemoryState.LatestDailyRun,
                CareerEngineProgressSummary = BuildCareerEngineProgressSummary(_operatorMemoryState),
                WatchlistSummary = BuildWatchlistSummary(_operatorMemoryState),
                ApprovalInboxSummary = BuildApprovalInboxSummary(_operatorMemoryState),
                SuggestionsSummary = BuildSuggestionsSummary(_operatorMemoryState),
                ProofTracks = BuildProofTracksLocked(),
                FocusAreas = BuildGrowthFocusAreasLocked(),
                Highlights = BuildGrowthHighlightsLocked(),
                ResearchRuns = BuildResearchHistoryLocked(),
                Watchlists = _operatorMemoryState.Watchlists.OrderBy(item => item.NextDueAt).ToList(),
            },
            Inbox = inbox,
            ML = BuildMLSectionLocked(),
        };
    }

    private void ResetKnowledgeLibraryRoot()
    {
        if (string.IsNullOrWhiteSpace(_knowledgeLibraryPath))
        {
            return;
        }

        Directory.CreateDirectory(_knowledgeLibraryPath);
        var rootDirectory = new DirectoryInfo(_knowledgeLibraryPath);
        foreach (var entry in rootDirectory.EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo directory)
            {
                directory.Delete(recursive: true);
                continue;
            }

            entry.Delete();
        }

        Directory.CreateDirectory(Path.Combine(_knowledgeLibraryPath, "Class Notes"));
        Directory.CreateDirectory(Path.Combine(_knowledgeLibraryPath, "Research"));
        Directory.CreateDirectory(Path.Combine(_knowledgeLibraryPath, "Follow Through"));
    }

    private IReadOnlyList<OfficeChatThread> BuildChatThreadsLocked()
    {
        var threads = _operatorMemoryState.DeskThreads
            .Select(thread => new OfficeChatThread
            {
                Id = thread.DeskId,
                Title = thread.DeskTitle,
                DisplayTitle = OfficeRouteCatalog.ResolveRouteDisplayTitle(thread.DeskId),
                UpdatedAt = thread.UpdatedAt,
                Messages = thread.Messages.OrderBy(item => item.CreatedAt).ToList(),
            })
            .OrderByDescending(thread => thread.UpdatedAt)
            .ToList();

        if (threads.Count > 0)
        {
            return threads;
        }

        return OfficeRouteCatalog.KnownRoutes
            .Select(route => new OfficeChatThread
            {
                Id = route,
                Title = OfficeRouteCatalog.ResolveRouteTitle(route),
                DisplayTitle = OfficeRouteCatalog.ResolveRouteDisplayTitle(route),
                UpdatedAt = DateTimeOffset.Now,
                Messages = Array.Empty<DeskMessageRecord>(),
            })
            .ToList();
    }

    private TrainingSessionState BuildTrainingSessionStateLocked()
    {
        var stage = OfficeStudySessionLogic.ResolveStage(_session);
        var stageSummary = OfficeStudySessionLogic.BuildStageSummary(stage);

        return new TrainingSessionState
        {
            Stage = stage,
            Focus = _session.Focus,
            FocusReason = _session.FocusReason,
            StageSummary = stageSummary,
            PracticeGenerated = _session.PracticeGenerated,
            PracticeScored = _session.PracticeScored,
            DefenseGenerated = _session.DefenseGenerated,
            DefenseScored = _session.DefenseScored,
            ReflectionSaved = _session.ReflectionSaved,
            HistoryFilePath = _trainingStore.StorePath,
            HistoryExists = _trainingStore.Exists,
            LastHistoryWriteAt = _trainingStore.GetLastWriteTime(),
        };
    }

    private OfficeInboxSection BuildInboxSectionLocked()
    {
        var pending = _operatorMemoryState.PendingApprovalSuggestions
            .OrderBy(GetInboxSortRank)
            .ThenByDescending(item => item.CreatedAt)
            .ToList();
        var open = _operatorMemoryState.OpenSuggestions
            .OrderBy(GetInboxSortRank)
            .ThenByDescending(item => item.CreatedAt)
            .ToList();
        var approved = _operatorMemoryState.ApprovedSuggestions
            .OrderBy(GetInboxSortRank)
            .ThenByDescending(item => item.ExecutionUpdatedAt ?? item.CreatedAt)
            .ToList();
        var queued = _operatorMemoryState.QueuedWorkSuggestions
            .OrderBy(GetInboxSortRank)
            .ThenByDescending(item => item.ExecutionUpdatedAt ?? item.CreatedAt)
            .ToList();
        var recent = _operatorMemoryState.RecentSuggestions.ToList();

        return new OfficeInboxSection
        {
            Summary =
                $"{pending.Count} pending approval | {open.Count} open | {approved.Count} approved | {queued.Count} queued/running/failed.",
            Approvals = pending,
            QueuedReady = open.Concat(approved).Concat(queued).ToList(),
            RecentResults = recent,
            PendingApproval = pending,
            Open = open,
            Approved = approved,
            QueuedWork = queued,
            Recent = recent,
        };
    }

    private IReadOnlyList<OfficeProviderRoleModel> BuildProviderRoleModelsLocked()
    {
        var installed = new HashSet<string>(_installedModelCache, StringComparer.OrdinalIgnoreCase);
        return new List<OfficeProviderRoleModel>
        {
            new() { Role = "Chief", ModelName = _settings.ChiefModel, Installed = installed.Contains(_settings.ChiefModel) },
            new() { Role = "Engineering", ModelName = _settings.MentorModel, Installed = installed.Contains(_settings.MentorModel) },
            new() { Role = "Suite Context", ModelName = _settings.RepoModel, Installed = installed.Contains(_settings.RepoModel) },
            new() { Role = "Growth", ModelName = _settings.BusinessModel, Installed = installed.Contains(_settings.BusinessModel) },
            new() { Role = "Study Builder", ModelName = _settings.TrainingModel, Installed = installed.Contains(_settings.TrainingModel) },
            new() { Role = "ML Engineer", ModelName = _settings.MLModel, Installed = installed.Contains(_settings.MLModel) },
        };
    }

    private IReadOnlyList<OfficeRouteOption> BuildRouteOptionsLocked()
    {
        return OfficeRouteCatalog.KnownRoutes
            .Select(route => new OfficeRouteOption
            {
                Id = route,
                Label = OfficeRouteCatalog.ResolveRouteDisplayTitle(route),
                Title = OfficeRouteCatalog.ResolveRouteTitle(route),
                Perspective = OfficeRouteCatalog.ResolvePerspective(route),
                Summary = BuildThreadIntro(route),
            })
            .ToList();
    }

    private string BuildRouteReasonLocked(string currentRoute)
    {
        if (!string.IsNullOrWhiteSpace(_session.LatestResearchReport?.Perspective))
        {
            return $"Last live research ran through {_session.LatestResearchReport.Perspective}.";
        }

        return currentRoute switch
        {
            OfficeRouteCatalog.EngineeringRoute =>
                $"Current study focus is {_session.Focus}, so Engineering remains the default route.",
            OfficeRouteCatalog.SuiteRoute =>
                "Suite Context is active because runtime trust or repo signals need operator attention.",
            OfficeRouteCatalog.BusinessRoute =>
                "Growth Ops is active to turn current work into proof, career leverage, and disciplined follow-through.",
            _ =>
                "Chief of Staff stays active when no narrower route has taken over.",
        };
    }

    private IReadOnlyList<string> BuildSuggestedMovesLocked()
    {
        var moves = new List<string>();
        if (_operatorMemoryState.PendingApprovalSuggestions.Count > 0)
        {
            moves.Add($"Resolve {_operatorMemoryState.PendingApprovalSuggestions.Count} approval item(s) in the shared inbox.");
        }

        if (!string.IsNullOrWhiteSpace(_session.Focus))
        {
            moves.Add($"Stay on {_session.Focus} until the guided loop is complete.");
        }

        moves.AddRange(
            _session.LatestResearchReport?.ActionMoves?.Take(3)
            ?? Enumerable.Empty<string>());

        moves.AddRange(
            _trainingHistorySummary.ReviewRecommendations
                .Where(item => item.IsDue)
                .Take(2)
                .Select(item => $"Review now: {item.Topic}."));

        return moves
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private IReadOnlyList<string> BuildSuiteContextSignalsLocked()
    {
        var signals = new List<string>
        {
            BuildQuietSuiteContextSummary(_suiteSnapshot),
            BuildQuietSuiteTrustSummary(_suiteSnapshot),
        };

        if (!string.IsNullOrWhiteSpace(_suiteSnapshot.StatusSummary))
        {
            signals.Add(_suiteSnapshot.StatusSummary);
        }

        signals.AddRange(_suiteSnapshot.HotAreas.Take(3).Select(area => $"Hot area: {area}"));

        return signals
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
    }

    private static IReadOnlyList<DeskMessageRecord> BuildTranscriptLocked(
        IReadOnlyList<OfficeChatThread> threads,
        string currentRoute)
    {
        return threads.FirstOrDefault(thread =>
                thread.Id.Equals(currentRoute, StringComparison.OrdinalIgnoreCase)
            )?.Messages
            ?? threads.FirstOrDefault()?.Messages
            ?? Array.Empty<DeskMessageRecord>();
    }

    private IReadOnlyList<string> BuildStudyHintsLocked()
    {
        var hints = new List<string>();
        if (!string.IsNullOrWhiteSpace(_session.FocusReason))
        {
            hints.Add(_session.FocusReason);
        }

        if (_trainingHistorySummary.ReviewRecommendations.FirstOrDefault() is { } review)
        {
            hints.Add($"Review target: {review.Topic}.");
        }

        if (!string.IsNullOrWhiteSpace(_session.DefenseFeedbackSummary))
        {
            hints.Add(_session.DefenseFeedbackSummary);
        }

        if (!string.IsNullOrWhiteSpace(_trainingHistorySummary.ReflectionSummary))
        {
            hints.Add(_trainingHistorySummary.ReflectionSummary);
        }

        return hints
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
    }

    private IReadOnlyList<OfficeStudyStep> BuildStudySequenceLocked()
    {
        return new List<OfficeStudyStep>
        {
            new()
            {
                Id = "focus",
                Title = "Focus",
                Detail = string.IsNullOrWhiteSpace(_session.Focus)
                    ? "Choose the next focus area."
                    : $"Active focus: {_session.Focus}",
                Status = "complete",
            },
            new()
            {
                Id = "practice-generate",
                Title = "Practice generation",
                Detail = _session.PracticeGenerated
                    ? "Practice set is loaded and ready for scoring."
                    : "Generate a practice set from the active focus.",
                Status = _session.PracticeGenerated ? "complete" : "pending",
            },
            new()
            {
                Id = "practice-score",
                Title = "Practice scoring",
                Detail = _session.PracticeScored
                    ? _session.PracticeResultSummary
                    : "Score the current practice set to unlock the defense stage.",
                Status = _session.PracticeScored ? "complete" : (_session.PracticeGenerated ? "running" : "pending"),
            },
            new()
            {
                Id = "defense-generate",
                Title = "Defense generation",
                Detail = _session.DefenseGenerated
                    ? "Defense prompt is ready."
                    : "Generate an oral-defense prompt tied to the same topic.",
                Status = _session.DefenseGenerated ? "complete" : (_session.PracticeScored ? "running" : "pending"),
            },
            new()
            {
                Id = "defense-score",
                Title = "Defense scoring",
                Detail = _session.DefenseScored
                    ? _session.DefenseScoreSummary
                    : "Score a typed defense answer to get rubric feedback.",
                Status = _session.DefenseScored ? "complete" : (_session.DefenseGenerated ? "running" : "pending"),
            },
            new()
            {
                Id = "reflection",
                Title = "Reflection",
                Detail = _session.ReflectionSaved
                    ? _session.ReflectionContextSummary
                    : "Capture what felt weak, what to review next, and the proof value of this session.",
                Status = _session.ReflectionSaved ? "complete" : (_session.DefenseScored ? "running" : "pending"),
            },
            new()
            {
                Id = "proof",
                Title = "Proof output",
                Detail = !string.IsNullOrWhiteSpace(_trainingHistorySummary.ReflectionSummary)
                    ? _trainingHistorySummary.ReflectionSummary
                    : "Saved reflections become proof of growth and future study evidence.",
                Status = _session.ReflectionSaved ? "running" : "pending",
            },
        };
    }

    private string BuildLatestScoreSummaryLocked()
    {
        if (!string.IsNullOrWhiteSpace(_session.DefenseScoreSummary) &&
            !string.Equals(_session.DefenseScoreSummary, "No scored oral-defense answer yet.", StringComparison.Ordinal))
        {
            return _session.DefenseScoreSummary;
        }

        if (!string.IsNullOrWhiteSpace(_session.PracticeResultSummary) &&
            !string.Equals(_session.PracticeResultSummary, "No scored practice yet.", StringComparison.Ordinal))
        {
            return _session.PracticeResultSummary;
        }

        return string.Empty;
    }

    private IReadOnlyList<OfficeLibraryRoot> BuildLibraryRootsLocked()
    {
        var documentCounts = _learningLibrary.Documents
            .GroupBy(document => document.SourceRootPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return _learningLibrary.SourceRoots
            .Select((root, index) => new OfficeLibraryRoot
            {
                Label = index == 0 ? "Primary root" : $"Additional root {index}",
                Path = root,
                Exists = Directory.Exists(root),
                IsPrimary = index == 0,
                DocumentCount = documentCounts.TryGetValue(root, out var count) ? count : 0,
            })
            .ToList();
    }

    private IReadOnlyList<OfficeLibraryDocument> BuildLibraryDocumentsLocked()
    {
        return _learningLibrary.Documents
            .OrderByDescending(document => document.LastUpdated)
            .Take(12)
            .Select(document => new OfficeLibraryDocument
            {
                Id = document.FullPath,
                Title = string.IsNullOrWhiteSpace(document.FileName) ? document.RelativePath : document.FileName,
                Path = document.FullPath,
                Summary = document.DisplaySummary,
                UpdatedAt = document.LastUpdated,
            })
            .ToList();
    }

    private IReadOnlyList<string> BuildProofTracksLocked()
    {
        return new[]
        {
            BuildCareerEngineProgressSummary(_operatorMemoryState),
            _trainingHistorySummary.OverallSummary,
            _trainingHistorySummary.DefenseSummary,
            _trainingHistorySummary.ReflectionSummary,
        }
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .Take(5)
        .ToList();
    }

    private IReadOnlyList<string> BuildGrowthFocusAreasLocked()
    {
        return new[]
        {
            _settings.EngineeringFocus,
            _settings.CadFocus,
            _settings.CareerFocus,
            _settings.BusinessFocus,
        }
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .Take(6)
        .ToList();
    }

    private IReadOnlyList<string> BuildGrowthHighlightsLocked()
    {
        var highlights = new List<string>
        {
            BuildWatchlistSummary(_operatorMemoryState),
            BuildApprovalInboxSummary(_operatorMemoryState),
            BuildSuggestionsSummary(_operatorMemoryState),
        };

        if (_operatorMemoryState.LatestDailyRun is { Objective.Length: > 0 } dailyRun)
        {
            highlights.Add($"Daily objective: {dailyRun.Objective}");
        }

        return highlights
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(6)
            .ToList();
    }

    private IReadOnlyList<OfficeResearchRun> BuildResearchHistoryLocked()
    {
        var runs = new List<OfficeResearchRun>();
        if (_session.LatestResearchReport is { } latest)
        {
            runs.Add(new OfficeResearchRun
            {
                Id = $"{latest.GeneratedAt:yyyyMMddHHmmss}-{CreateSlug(latest.Query)}",
                Title = latest.Query,
                Summary = latest.RunSummary,
                UpdatedAt = latest.GeneratedAt,
            });
        }

        runs.AddRange(
            _operatorMemoryState.Activities
                .Where(activity => activity.EventType is "research_run" or "research_saved")
                .OrderByDescending(activity => activity.OccurredAt)
                .Take(5)
                .Select(activity => new OfficeResearchRun
                {
                    Id = $"{activity.EventType}-{activity.OccurredAt:yyyyMMddHHmmss}",
                    Title = string.IsNullOrWhiteSpace(activity.Topic) ? activity.EventType : activity.Topic,
                    Summary = activity.Summary,
                    UpdatedAt = activity.OccurredAt,
                }));

        return runs
            .GroupBy(run => run.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(run => run.UpdatedAt)
            .Take(8)
            .ToList();
    }

    private async Task AppendDeskMessagesLockedAsync(
        string deskId,
        DeskMessageRecord message,
        CancellationToken cancellationToken
    )
    {
        var thread = ResolveDeskThreadLocked(deskId);
        thread.Messages.Add(message);
        thread.UpdatedAt = message.CreatedAt;
        thread.Messages = thread.Messages.OrderBy(item => item.CreatedAt).TakeLast(120).ToList();
        _operatorMemoryState = await _operatorMemoryStore.SaveDeskThreadsAsync(
            _operatorMemoryState.DeskThreads,
            cancellationToken
        );
    }

    private DeskThreadState ResolveDeskThreadLocked(string deskId)
    {
        var route = OfficeRouteCatalog.NormalizeRoute(deskId);
        var thread = _operatorMemoryState.FindDeskThread(route);
        if (thread is not null)
        {
            return thread;
        }

        var createdAt = DateTimeOffset.Now;
        var created = new DeskThreadState
        {
            DeskId = route,
            DeskTitle = OfficeRouteCatalog.ResolveRouteTitle(route),
            UpdatedAt = createdAt,
            Messages =
            [
                new DeskMessageRecord
                {
                    DeskId = route,
                    Role = "assistant",
                    Author = OfficeRouteCatalog.ResolveRouteTitle(route),
                    Kind = "system",
                    Content = BuildThreadIntro(route),
                    CreatedAt = createdAt,
                },
            ],
        };
        _operatorMemoryState.DeskThreads.Add(created);
        return created;
    }

    private static string BuildThreadIntro(string route) =>
        OfficeRouteCatalog.NormalizeRoute(route) switch
        {
            OfficeRouteCatalog.ChiefRoute =>
                "I route the day across Suite, engineering, CAD, and growth. Ask for a brief, a plan, or a synthesis.",
            OfficeRouteCatalog.EngineeringRoute =>
                "I combine EE coaching, CAD workflow judgment, and training prep. Ask for explanations, drills, or review guidance.",
            OfficeRouteCatalog.SuiteRoute =>
                "I keep the office aware of Suite trust, availability, and workflow context in a calm, read-only way.",
            OfficeRouteCatalog.BusinessRoute =>
                "I translate current capability into growth discipline, offers, proof points, and monetization paths without hype.",
            OfficeRouteCatalog.MLRoute =>
                "I analyze your learning data with ML (Scikit-learn, PyTorch, TensorFlow) and produce insights, forecasts, and Suite-ready artifacts.",
            _ => "Ask for the next move.",
        };

    private static string BuildDeskSystemPrompt(string route)
    {
        return OfficeRouteCatalog.NormalizeRoute(route) switch
        {
            OfficeRouteCatalog.ChiefRoute =>
                """
                You are the Chief of Staff inside Office.
                Route the day across Suite, electrical engineering, CAD workflow judgment, and business operations.
                Stay read-only toward Suite.
                Answer the current request directly. Do not recycle old assistant wording when fresher state is provided.
                Respond with short sections named NEXT MOVE, WHY, and HANDOFF.
                """,
            OfficeRouteCatalog.EngineeringRoute =>
                """
                You are the Engineering Desk inside Office.
                Combine electrical engineering teaching, CAD workflow judgment, practice-test coaching, and oral-defense reasoning.
                Keep answers practical, operator-safe, and tied to review-first production work.
                Lead with the governing principle, then give one bounded next move.
                Do not mention internal model/provider details unless the user explicitly asks.
                Do not echo stale thread wording when fresher state is provided.
                Respond with short sections named ANSWER, CHECKS, and CAD OR SUITE LINK.
                """,
            OfficeRouteCatalog.SuiteRoute =>
                """
                You are the Suite Context desk inside Office.
                Keep the office aware of Suite trust, availability, and workflow context without turning into a repo-planning tool.
                Stay read-only and avoid implementation proposals unless explicitly asked.
                Prefer current runtime facts over older thread summaries.
                Respond with short sections named CONTEXT, TRUST, and WHY IT MATTERS.
                """,
            OfficeRouteCatalog.BusinessRoute =>
                """
                You are Business Ops inside Office.
                Turn current capability into internal operating moves, pilot-shaped offers, and monetization proof without hype.
                Keep the focus on personal growth, real electrical production-control value, and career proof.
                Avoid generic startup language.
                Respond with short sections named MOVE, WHY IT WINS, and WHAT TO PROVE.
                """,
            OfficeRouteCatalog.MLRoute =>
                PromptComposer.BuildMLEngineerSystemPrompt(),
            _ =>
                """
                You are a practical assistant inside Office.
                Respond directly and keep the answer tied to action.
                """,
        };
    }

    private string BuildDeskConversationPromptLocked(string route, string userInput)
    {
        var thread = ResolveDeskThreadLocked(route);
        var history = thread.Messages
            .Where(item => !item.Kind.Equals("system", StringComparison.OrdinalIgnoreCase))
            .TakeLast(6)
            .ToList();
        var knowledgeContext = KnowledgePromptContextBuilder.BuildRelevantContext(
            _learningLibrary,
            new[]
            {
                userInput,
                _session.Focus,
                _learningProfile.CurrentNeed,
                _trainingHistorySummary.ReviewQueueSummary,
                _trainingHistorySummary.DefenseSummary,
            },
            maxDocuments: 2,
            maxTotalCharacters: 1800,
            maxExcerptCharacters: 620
        );

        var builder = new StringBuilder();
        builder.AppendLine("Office operating parameters:");
        builder.AppendLine($"- suite: {_settings.SuiteFocus}");
        builder.AppendLine($"- engineering: {_settings.EngineeringFocus}");
        builder.AppendLine($"- cad: {_settings.CadFocus}");
        builder.AppendLine($"- business: {_settings.BusinessFocus}");
        builder.AppendLine($"- career: {_settings.CareerFocus}");
        builder.AppendLine();
        builder.AppendLine("Current Suite context:");
        builder.AppendLine($"- suite awareness: {BuildQuietSuiteContextSummary(_suiteSnapshot)}");
        builder.AppendLine($"- suite trust: {BuildQuietSuiteTrustSummary(_suiteSnapshot)}");
        builder.AppendLine();
        builder.AppendLine("Current engineering and knowledge context:");
        builder.AppendLine($"- learning profile: {_learningProfile.Summary}");
        builder.AppendLine($"- current need: {_learningProfile.CurrentNeed}");
        builder.AppendLine($"- review queue: {_trainingHistorySummary.ReviewQueueSummary}");
        builder.AppendLine($"- defense summary: {_trainingHistorySummary.DefenseSummary}");
        builder.AppendLine(
            $"- imported knowledge: {JoinOrNone(_learningLibrary.Documents.Take(5).Select(item => item.PromptSummary).ToList())}"
        );
        builder.AppendLine("- relevant notebook evidence:");
        builder.AppendLine(knowledgeContext);
        builder.AppendLine();
        builder.AppendLine("Current growth and operator context:");
        builder.AppendLine($"- daily objective: {_operatorMemoryState.LatestDailyRun?.Objective ?? "no daily run yet"}");
        builder.AppendLine($"- approval inbox: {BuildApprovalInboxSummary(_operatorMemoryState)}");
        builder.AppendLine($"- monetization leads: {JoinOrNone(_suiteSnapshot.MonetizationMoves)}");
        builder.AppendLine();
        builder.AppendLine("Current Office provider context:");
        builder.AppendLine($"- active provider: {_modelProvider.ProviderLabel}");
        builder.AppendLine($"- provider ready: {(_installedModelCache.Count > 0 ? "yes" : "no")}");
        builder.AppendLine($"- installed models: {JoinOrNone(_installedModelCache)}");
        builder.AppendLine(
            $"- role models: Chief={_settings.ChiefModel}; Engineering={_settings.MentorModel}; Suite Context={_settings.RepoModel}; Growth={_settings.BusinessModel}; Study Builder={_settings.TrainingModel}"
        );
        builder.AppendLine(
            "- Only mention model/provider details if the user explicitly asks. Use the provider facts above, not older assistant messages or research history, as the source of truth."
        );
        builder.AppendLine();
        builder.AppendLine("Recent desk thread:");
        foreach (var message in history)
        {
            builder.AppendLine($"{message.Author}: {Truncate(message.Content, 420)}");
        }

        builder.AppendLine();
        builder.AppendLine("User request:");
        builder.AppendLine(userInput);
        builder.AppendLine();
        builder.AppendLine(
            "Keep the answer action-oriented, grounded in the selected desk role, and focused on the current request instead of rehashing older wording."
        );
        return builder.ToString();
    }

    private string BuildDeskFallbackResponse(string route, string userInput)
    {
        return OfficeRouteCatalog.NormalizeRoute(route) switch
        {
            OfficeRouteCatalog.ChiefRoute =>
                $"NEXT MOVE\nRun one bounded block on {_operatorMemoryState.LatestDailyRun?.Objective ?? _learningProfile.CurrentNeed}.\n\nWHY\nThat keeps the day tied to {_suiteSnapshot.StatusSummary} instead of drifting into generic planning.\n\nHANDOFF\nIf this needs current facts, run live research for: {userInput}",
            OfficeRouteCatalog.EngineeringRoute =>
                $"ANSWER\nStart with the governing principle behind {_trainingHistorySummary.ReviewRecommendations.FirstOrDefault()?.Topic ?? _session.Focus}. Explain what can go wrong if that principle is missed.\n\nCHECKS\nName one calculation, one standard or rule check, and one drawing-review step you would use to validate the answer.\n\nCAD OR SUITE LINK\nTie the explanation back to {_suiteSnapshot.HotAreas.FirstOrDefault() ?? "the current Suite hotspot"} and {_settings.CadFocus}.",
            OfficeRouteCatalog.SuiteRoute =>
                $"CONTEXT\n{BuildQuietSuiteContextSummary(_suiteSnapshot)}\n\nTRUST\n{BuildQuietSuiteTrustSummary(_suiteSnapshot)}\n\nWHY IT MATTERS\nUse Suite as background context for better decisions, not as a prompt to start repo work.",
            OfficeRouteCatalog.BusinessRoute =>
                $"MOVE\nTurn the current work into one proof artifact around {_suiteSnapshot.MonetizationMoves.FirstOrDefault() ?? "drawing production control for electrical teams"}.\n\nWHY IT WINS\nIt shows real operator value and career leverage instead of vague AI positioning.\n\nWHAT TO PROVE\nShow the judgment used, the risk removed, and the workflow tightened.",
            _ => "Work from the current context and choose the next bounded move.",
        };
    }

    private string? TryBuildDeterministicDeskResponseLocked(string route, string userInput)
    {
        if (LooksLikeProviderStatusQuestion(userInput))
        {
            return BuildProviderStatusResponseLocked(route);
        }

        return null;
    }

    private string BuildProviderStatusResponseLocked(string route)
    {
        var routeId = OfficeRouteCatalog.NormalizeRoute(route);
        var unifiedModel = ResolveUnifiedBaselineModelLocked();
        var providerReady = _installedModelCache.Count > 0;
        var installedCount = _installedModelCache.Count;
        var providerLabel = _modelProvider.ProviderLabel;
        var installedSummary = installedCount == 0
            ? "none installed yet"
            : string.Join(", ", _installedModelCache);
        var modelSummary = string.IsNullOrWhiteSpace(unifiedModel)
            ? "Office role models are split across multiple configured models."
            : $"All Office roles currently use `{unifiedModel}`.";

        return routeId switch
        {
            OfficeRouteCatalog.ChiefRoute =>
                $"NEXT MOVE\nKeep the baseline simple: {modelSummary}\n\nWHY\nOffice is on {providerLabel} with provider ready = {(providerReady ? "yes" : "no")} and {installedCount} installed model(s): {installedSummary}.\n\nHANDOFF\nIf you want a specialization split later, add a local override layer before changing shared settings.",
            OfficeRouteCatalog.SuiteRoute =>
                $"CONTEXT\n{modelSummary}\n\nTRUST\nOffice is on {providerLabel}. Provider ready = {(providerReady ? "yes" : "no")}. Installed models: {installedSummary}.\n\nWHY IT MATTERS\nThat confirms the local Office provider state only; it does not change Suite runtime trust.",
            OfficeRouteCatalog.BusinessRoute =>
                $"MOVE\nKeep the baseline unified: {modelSummary}\n\nWHY IT WINS\nOne shared local model reduces drift while you tighten Office workflows.\n\nWHAT TO PROVE\nProvider ready = {(providerReady ? "yes" : "no")}; installed models = {installedCount}; provider = {providerLabel}.",
            _ =>
                $"ANSWER\n{modelSummary}\n\nCHECKS\nProvider: {providerLabel}. Provider ready: {(providerReady ? "yes" : "no")}. Installed models: {installedSummary}.\n\nCAD OR SUITE LINK\nThis confirms the Office model rack is online; keep CAD and Suite decisions review-first.",
        };
    }

    private static bool LooksLikeProviderStatusQuestion(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return false;
        }

        return userInput.Contains("which model", StringComparison.OrdinalIgnoreCase)
            || userInput.Contains("what model", StringComparison.OrdinalIgnoreCase)
            || userInput.Contains("baseline model", StringComparison.OrdinalIgnoreCase)
            || userInput.Contains("office roles use", StringComparison.OrdinalIgnoreCase)
            || userInput.Contains("providerready", StringComparison.OrdinalIgnoreCase)
            || userInput.Contains("provider ready", StringComparison.OrdinalIgnoreCase)
            || userInput.Contains("installed model", StringComparison.OrdinalIgnoreCase)
            || userInput.Contains("installedmodel", StringComparison.OrdinalIgnoreCase)
            || userInput.Contains("what provider", StringComparison.OrdinalIgnoreCase)
            || userInput.Contains("ollama model", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveDeskModel(string route)
    {
        return OfficeRouteCatalog.NormalizeRoute(route) switch
        {
            OfficeRouteCatalog.ChiefRoute => _settings.ChiefModel,
            OfficeRouteCatalog.EngineeringRoute => _settings.MentorModel,
            OfficeRouteCatalog.SuiteRoute => _settings.RepoModel,
            OfficeRouteCatalog.BusinessRoute => _settings.BusinessModel,
            OfficeRouteCatalog.MLRoute => _settings.MLModel,
            _ => _settings.ChiefModel,
        };
    }

    private string ResolveResearchModel(string perspective) =>
        perspective switch
        {
            "Chief of Staff" => _settings.ChiefModel,
            "Repo Coach" => _settings.RepoModel,
            "Business Strategist" => _settings.BusinessModel,
            _ => _settings.MentorModel,
        };

    private string? ResolveUnifiedBaselineModelLocked()
    {
        var configuredModels = new[]
            {
                _settings.ChiefModel,
                _settings.MentorModel,
                _settings.RepoModel,
                _settings.TrainingModel,
                _settings.BusinessModel,
            }
            .Where(static model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return configuredModels.Count == 1 ? configuredModels[0] : null;
    }

    private bool ResolvePolicyRequiresApproval(string perspective)
    {
        var policy = _operatorMemoryState.Policies.FirstOrDefault(item =>
            item.Role.Equals(perspective, StringComparison.OrdinalIgnoreCase)
        );
        if (policy is not null)
        {
            return policy.RequiresApproval;
        }

        return perspective is "Repo Coach" or "Business Strategist";
    }

    private SuggestedAction? ResolveSuggestionByIdLocked(string suggestionId)
    {
        return _operatorMemoryState.Suggestions.FirstOrDefault(item =>
            item.Id.Equals(suggestionId, StringComparison.OrdinalIgnoreCase)
        );
    }

    private async Task RecordActivityLockedAsync(
        string eventType,
        string agent,
        string topic,
        string summary,
        CancellationToken cancellationToken
    )
    {
        _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
            new OperatorActivityRecord
            {
                EventType = eventType,
                Agent = agent,
                Topic = topic,
                Summary = Truncate(summary, 220),
                OccurredAt = DateTimeOffset.Now,
            },
            cancellationToken
        );
    }

    private async Task SaveSessionLockedAsync(CancellationToken cancellationToken)
    {
        _session.UpdatedAt = DateTimeOffset.Now;
        await _sessionStore.SaveAsync(_session, cancellationToken);
    }

    private void ResetSessionProgressLocked(
        string focus,
        string reason,
        string? difficulty,
        int? questionCount
    )
    {
        _session.Focus = string.IsNullOrWhiteSpace(focus)
            ? "Protection, grounding, standards, drafting safety"
            : focus.Trim();
        _session.FocusReason = string.IsNullOrWhiteSpace(reason)
            ? "Manual focus selected for the next guided session."
            : reason.Trim();
        _session.Difficulty = string.IsNullOrWhiteSpace(difficulty) ? "Mixed" : difficulty.Trim();
        _session.QuestionCount = Math.Clamp(questionCount ?? _session.QuestionCount, 3, 10);
        _session.PracticeGenerated = false;
        _session.PracticeScored = false;
        _session.DefenseGenerated = false;
        _session.DefenseScored = false;
        _session.ReflectionSaved = false;
        _session.ActivePracticeTest = null;
        _session.ActiveDefenseScenario = new OralDefenseScenario();
        _session.LastDefenseEvaluation = null;
        _session.DefenseAnswerDraft = string.Empty;
        _session.PracticeResultSummary = "No scored practice yet.";
        _session.DefenseScoreSummary = "No scored oral-defense answer yet.";
        _session.DefenseFeedbackSummary =
            "Score a typed answer to get rubric feedback and follow-up coaching.";
        _session.LastScoredSessionMode = string.Empty;
        _session.LastScoredSessionFocus = string.Empty;
        _session.LastReflection = null;
        _session.ReflectionContextSummary =
            "Score a practice or defense session to save a reflection.";
    }

    private async Task<OperatorMemoryState> AutoStageSelfServeSuggestionsLockedAsync(
        IReadOnlyList<SuggestedAction> suggestions,
        CancellationToken cancellationToken
    )
    {
        var candidate = suggestions
            .Where(item =>
                !item.RequiresApproval
                && item.ActionType.Equals("research_followup", StringComparison.OrdinalIgnoreCase)
            )
            .OrderBy(item => item.Priority.Equals("high", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefault();

        if (candidate is null)
        {
            return _operatorMemoryState;
        }

        var acceptedOutcome = new SuggestionOutcome
        {
            Status = "accepted",
            Reason = "Auto-staged from self-serve research.",
            OutcomeNote =
                "Queued automatically because this agent is allowed to prepare low-risk research follow-through.",
            RecordedAt = DateTimeOffset.Now,
        };

        _operatorMemoryState = await _operatorMemoryStore.RecordSuggestionOutcomeAsync(
            candidate.Id,
            acceptedOutcome,
            cancellationToken
        );
        _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionExecutionAsync(
            candidate.Id,
            "queued",
            "Auto-queued from self-serve research.",
            cancellationToken
        );
        await RecordActivityLockedAsync(
            "suggestion_auto_queued",
            candidate.SourceAgent,
            candidate.Title,
            "Auto-queued from self-serve research.",
            cancellationToken
        );

        var queuedSuggestion = ResolveSuggestionByIdLocked(candidate.Id);
        if (queuedSuggestion is not null)
        {
            await ExecuteResearchFollowThroughLockedAsync(queuedSuggestion, cancellationToken);
        }

        return _operatorMemoryState;
    }

    private async Task<SuggestedAction> ExecuteResearchFollowThroughLockedAsync(
        SuggestedAction suggestion,
        CancellationToken cancellationToken
    )
    {
        _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionExecutionAsync(
            suggestion.Id,
            "running",
            "Preparing research follow-through brief.",
            cancellationToken
        );
        await RecordActivityLockedAsync(
            "suggestion_running",
            suggestion.SourceAgent,
            suggestion.Title,
            "Preparing research follow-through brief.",
            cancellationToken
        );

        try
        {
            var brief = await PersistResearchFollowThroughBriefLockedAsync(
                suggestion,
                cancellationToken
            );

            _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionResearchResultAsync(
                suggestion.Id,
                brief.summary,
                brief.detail,
                brief.sources,
                brief.path,
                cancellationToken
            );
            _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionExecutionAsync(
                suggestion.Id,
                "completed",
                brief.summary,
                cancellationToken
            );
            await RecordActivityLockedAsync(
                "suggestion_completed",
                suggestion.SourceAgent,
                suggestion.Title,
                brief.summary,
                cancellationToken
            );
        }
        catch (Exception exception)
        {
            var failureSummary = $"Follow-through failed: {exception.Message}";
            _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionExecutionAsync(
                suggestion.Id,
                "failed",
                failureSummary,
                cancellationToken
            );
            await RecordActivityLockedAsync(
                "suggestion_failed",
                suggestion.SourceAgent,
                suggestion.Title,
                failureSummary,
                cancellationToken
            );
        }

        return ResolveSuggestionByIdLocked(suggestion.Id) ?? suggestion;
    }

    private static IReadOnlyList<SuggestedAction> BuildResearchSuggestions(
        ResearchReport report,
        string perspective,
        bool requiresApproval
    )
    {
        var actions = report.ActionMoves.Count == 0
            ? report.KeyTakeaways.Take(2).ToList()
            : report.ActionMoves.Take(2).ToList();
        var createdAt = DateTimeOffset.Now;

        return actions
            .Select(
                (action, index) =>
                    new SuggestedAction
                    {
                        Title = action,
                        SourceAgent = perspective,
                        ActionType = "research_followup",
                        Priority = index == 0 ? "high" : "medium",
                        Rationale = report.Summary,
                        ExpectedBenefit = action,
                        LinkedArea = report.Query,
                        WhatYouLearn =
                            "This turns live research into a concrete next move instead of leaving it as passive reading.",
                        ProductImpact = perspective switch
                        {
                            "Repo Coach" =>
                                "This can tighten Suite proposals before implementation work starts.",
                            "Business Strategist" =>
                                "This can keep packaging tied to real operator value and current market evidence.",
                            _ =>
                                "This can sharpen the next study or planning step with current source material.",
                        },
                        CareerValue =
                            "This builds evidence that you can turn current research into domain-aware action.",
                        RequiresApproval = requiresApproval,
                        CreatedAt = createdAt,
                    }
            )
            .ToList();
    }

    private async Task<string> PersistResearchMarkdownLockedAsync(
        ResearchReport report,
        string? notes,
        bool reloadKnowledge,
        CancellationToken cancellationToken
    )
    {
        var researchDirectory = Path.Combine(_knowledgeLibraryPath, "Research");
        Directory.CreateDirectory(researchDirectory);

        var slug = CreateSlug(report.Query);
        var fileName = $"{DateTime.Now:yyyyMMdd-HHmmss}-{slug}.md";
        var filePath = Path.Combine(researchDirectory, fileName);
        var markdown = BuildResearchMarkdown(report, notes);
        await File.WriteAllTextAsync(filePath, markdown, cancellationToken);

        if (reloadKnowledge)
        {
            _learningLibrary = await _knowledgeImportService.LoadAsync(
                _knowledgeLibraryPath,
                _additionalKnowledgePaths,
                cancellationToken
            );
            _learningProfile = _learningProfileService.Build(
                _learningLibrary,
                _trainingHistorySummary,
                _suiteSnapshot
            );
        }

        return filePath;
    }

    private async Task<(string path, string summary, string detail, IReadOnlyList<string> sources)>
        PersistResearchFollowThroughBriefLockedAsync(
            SuggestedAction suggestion,
            CancellationToken cancellationToken
        )
    {
        var followThroughDirectory = Path.Combine(_knowledgeLibraryPath, "Follow Through");
        Directory.CreateDirectory(followThroughDirectory);

        var slug = CreateSlug(
            string.IsNullOrWhiteSpace(suggestion.Title) ? suggestion.LinkedArea : suggestion.Title
        );
        var fileName = $"{DateTime.Now:yyyyMMdd-HHmmss}-{slug}.md";
        var filePath = Path.Combine(followThroughDirectory, fileName);
        var report = _session.LatestResearchReport;
        var sources = report?.Sources
            .Select(source =>
                string.IsNullOrWhiteSpace(source.Url) ? source.DisplaySummary : source.Url
            )
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList()
            ?? [];
        var markdown = BuildResearchFollowThroughMarkdown(suggestion, report, fileName);

        await File.WriteAllTextAsync(filePath, markdown, cancellationToken);

        _learningLibrary = await _knowledgeImportService.LoadAsync(
            _knowledgeLibraryPath,
            _additionalKnowledgePaths,
            cancellationToken
        );
        _learningProfile = _learningProfileService.Build(
            _learningLibrary,
            _trainingHistorySummary,
            _suiteSnapshot
        );

        var summary = $"Prepared follow-through brief: {fileName}";
        var detail =
            $"Saved a bounded research follow-through brief under Follow Through for '{suggestion.LinkedArea}'. Review it, then either add notes to the knowledge library or turn it into a concrete Suite or study task.";
        return (filePath, summary, detail, sources);
    }

    private string BuildResearchFollowThroughMarkdown(
        SuggestedAction suggestion,
        ResearchReport? report,
        string fileName
    )
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Research Follow-Through Brief");
        builder.AppendLine();
        builder.AppendLine($"- Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
        builder.AppendLine($"- File: {fileName}");
        builder.AppendLine($"- Source agent: {suggestion.SourceAgent}");
        builder.AppendLine($"- Priority: {suggestion.Priority}");
        builder.AppendLine($"- Linked area: {suggestion.LinkedArea}");
        builder.AppendLine();
        builder.AppendLine("## Prompted move");
        builder.AppendLine(suggestion.Title);
        builder.AppendLine();
        builder.AppendLine("## Why this matters");
        builder.AppendLine(
            string.IsNullOrWhiteSpace(suggestion.Rationale)
                ? "No rationale was captured."
                : suggestion.Rationale
        );
        builder.AppendLine();
        builder.AppendLine("## Expected benefit");
        builder.AppendLine(
            string.IsNullOrWhiteSpace(suggestion.ExpectedBenefit)
                ? "No expected benefit was captured."
                : suggestion.ExpectedBenefit
        );
        builder.AppendLine();
        builder.AppendLine("## Operator framing");
        builder.AppendLine($"- Learning value: {suggestion.WhatYouLearn}");
        builder.AppendLine($"- Product impact: {suggestion.ProductImpact}");
        builder.AppendLine($"- Career value: {suggestion.CareerValue}");
        builder.AppendLine($"- Current study focus: {_session.Focus}");
        builder.AppendLine($"- Current review queue: {_trainingHistorySummary.ReviewQueueSummary}");
        builder.AppendLine($"- Suite context: {BuildQuietSuiteContextSummary(_suiteSnapshot)}");
        builder.AppendLine($"- Suite trust: {BuildQuietSuiteTrustSummary(_suiteSnapshot)}");
        builder.AppendLine();

        if (report is not null)
        {
            builder.AppendLine("## Latest research run");
            builder.AppendLine($"- Query: {report.Query}");
            builder.AppendLine($"- Perspective: {report.Perspective}");
            builder.AppendLine($"- Model: {report.Model}");
            builder.AppendLine($"- Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm}");
            builder.AppendLine($"- Summary: {report.Summary}");
            builder.AppendLine();

            if (report.KeyTakeaways.Count > 0)
            {
                builder.AppendLine("## Key takeaways");
                foreach (var takeaway in report.KeyTakeaways)
                {
                    builder.AppendLine($"- {takeaway}");
                }

                builder.AppendLine();
            }

            if (report.ActionMoves.Count > 0)
            {
                builder.AppendLine("## Suggested next moves");
                foreach (var actionMove in report.ActionMoves)
                {
                    builder.AppendLine($"- {actionMove}");
                }

                builder.AppendLine();
            }

            if (report.Sources.Count > 0)
            {
                builder.AppendLine("## Sources");
                foreach (var source in report.Sources)
                {
                    builder.AppendLine($"- {source.DisplaySummary}: {source.Url}");
                }

                builder.AppendLine();
            }
        }

        builder.AppendLine("## Review-first next check");
        builder.AppendLine(
            "Before treating this as a decision, compare the brief against your own notes, standards, and drawing-review criteria."
        );
        return builder.ToString().Trim() + Environment.NewLine;
    }

    private static string BuildResearchMarkdown(ResearchReport report, string? notes)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Live Research: {report.Query}");
        builder.AppendLine();
        builder.AppendLine($"- Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm}");
        builder.AppendLine($"- Perspective: {report.Perspective}");
        builder.AppendLine($"- Model: {report.Model}");
        builder.AppendLine($"- Source: {report.GenerationSource}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine(report.Summary);
        builder.AppendLine();
        builder.AppendLine("## Key Takeaways");
        builder.AppendLine();
        foreach (var takeaway in report.KeyTakeaways)
        {
            builder.AppendLine($"- {takeaway}");
        }

        builder.AppendLine();
        builder.AppendLine("## Action Moves");
        builder.AppendLine();
        foreach (var action in report.ActionMoves)
        {
            builder.AppendLine($"- {action}");
        }

        builder.AppendLine();
        builder.AppendLine("## Sources");
        builder.AppendLine();
        foreach (var source in report.Sources)
        {
            builder.AppendLine($"### {source.Title}");
            builder.AppendLine();
            builder.AppendLine($"- Domain: {source.Domain}");
            builder.AppendLine($"- URL: {source.Url}");
            builder.AppendLine($"- Search Snippet: {source.SearchSnippet}");
            if (!string.IsNullOrWhiteSpace(source.Extract))
            {
                builder.AppendLine($"- Extract: {source.Extract}");
            }

            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            builder.AppendLine("## Operator Notes");
            builder.AppendLine();
            builder.AppendLine(notes.Trim());
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string CreateSlug(string value)
    {
        var cleaned = new string(
            value
                .Trim()
                .ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray()
        );
        while (cleaned.Contains("--", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        }

        cleaned = cleaned.Trim('-');
        return string.IsNullOrWhiteSpace(cleaned) ? "live-research" : cleaned;
    }

    private static string BuildQuietSuiteContextSummary(SuiteSnapshot snapshot)
    {
        if (!snapshot.RepoAvailable)
        {
            return "Suite awareness is unavailable at the configured path right now.";
        }

        if (!snapshot.RuntimeStatusAvailable)
        {
            return "Suite awareness is connected and read-only. Runtime trust is currently unavailable.";
        }

        return snapshot.RuntimeDoctorState switch
        {
            "ready" => "Suite awareness is connected, read-only, and stable.",
            "needs-attention" => "Suite awareness is connected. Runtime trust needs attention.",
            "unavailable" => "Suite awareness is connected. Runtime trust is unavailable right now.",
            _ => "Suite awareness is connected. Runtime trust is still settling.",
        };
    }

    private static string BuildQuietSuiteTrustSummary(SuiteSnapshot snapshot)
    {
        if (!snapshot.RepoAvailable)
        {
            return "Suite trust cannot be checked until the configured Suite path is available.";
        }

        if (!snapshot.RuntimeStatusAvailable)
        {
            return "Runtime trust is currently unavailable from Office.";
        }

        return snapshot.ActionableIssueCount > 0
            ? "Runtime trust needs attention before you lean on Suite context."
            : "Runtime trust looks steady for read-only awareness.";
    }

    private static string BuildCareerEngineProgressSummary(OperatorMemoryState state)
    {
        var chiefPasses = state.Activities.Count(item =>
            item.EventType.Equals("chief_pass", StringComparison.OrdinalIgnoreCase)
        );
        var researchRuns = state.Activities.Count(item =>
            item.EventType.Equals("research_run", StringComparison.OrdinalIgnoreCase)
            || item.EventType.Equals("watchlist_run", StringComparison.OrdinalIgnoreCase)
        );
        var practice = state.Activities.Count(item =>
            item.EventType.Equals("practice_scored", StringComparison.OrdinalIgnoreCase)
        );
        var defense = state.Activities.Count(item =>
            item.EventType.Equals("defense_scored", StringComparison.OrdinalIgnoreCase)
        );
        var resolved = state.Suggestions.Count(item => !item.IsPending);

        return $"Chief passes {chiefPasses}/8 | Research runs {researchRuns}/8 | Practice {practice}/6 | Defense {defense}/4 | Suggestions resolved {resolved}/10.";
    }

    private static string BuildApprovalInboxSummary(OperatorMemoryState state)
    {
        var pending = state.PendingApprovalSuggestions.Count;
        var resolved = state.Suggestions.Count(item => item.RequiresApproval && !item.IsPending);

        return pending switch
        {
            0 => resolved == 0
                ? "No approvals are pending."
                : $"No approvals are pending. {resolved} recent approval decision{(resolved == 1 ? string.Empty : "s")} recorded.",
            1 => $"{pending} approval is pending. {resolved} recent approval decision{(resolved == 1 ? string.Empty : "s")} recorded.",
            _ => $"{pending} approvals are pending. {resolved} recent approval decision{(resolved == 1 ? string.Empty : "s")} recorded.",
        };
    }

    private static string BuildSuggestionsSummary(OperatorMemoryState state)
    {
        var open = state.OpenSuggestions.Count;
        var approved = state.ApprovedSuggestions.Count;
        return $"{open} open suggestion{(open == 1 ? string.Empty : "s")} | {approved} approved next step{(approved == 1 ? string.Empty : "s")}.";
    }

    private static string BuildWatchlistSummary(OperatorMemoryState state)
    {
        if (state.Watchlists.Count == 0)
        {
            return "No watchlists configured yet. Add one when you want recurring research again.";
        }

        var due = state.DueWatchlists.Count;
        var next = state.Watchlists.Where(item => item.IsEnabled).OrderBy(item => item.NextDueAt).FirstOrDefault();
        return next is null
            ? $"{state.Watchlists.Count} watchlists configured."
            : $"{due} due now | next: {next.Topic} ({next.DueSummary}).";
    }

    private static int GetInboxSortRank(SuggestedAction suggestion)
    {
        if (suggestion.RequiresApproval && suggestion.IsPending)
        {
            return 0;
        }

        if (suggestion.IsPending)
        {
            return 1;
        }

        return suggestion.Status switch
        {
            "deferred" => 2,
            "accepted" => 3,
            "rejected" => 4,
            _ => 5,
        };
    }

    private static string BuildDefenseFeedbackSummary(DefenseEvaluation evaluation)
    {
        var weakestItem = evaluation.RubricItems
            .OrderBy(item => item.MaxScore == 0 ? 0 : (double)item.Score / item.MaxScore)
            .ThenBy(item => item.Name)
            .FirstOrDefault();
        return weakestItem is null
            ? evaluation.NextReviewRecommendation
            : $"{evaluation.NextReviewRecommendation} Weakest area: {weakestItem.Name}. {weakestItem.Feedback}";
    }

    private static string ResolveOfficeRootPath(string baseDirectory)
    {
        var current = new DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            var dailyDeskPath = Path.Combine(current.FullName, "DailyDesk");
            var dailyDeskProjectPath = Path.Combine(dailyDeskPath, "DailyDesk.csproj");
            if (Directory.Exists(dailyDeskPath) && File.Exists(dailyDeskProjectPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", ".."));
    }

    private static IReadOnlyList<StudyTrack> BuildDefaultStudyTracks() =>
    [
        new()
        {
            Title = "Protection, grounding, and safe design constraints",
            Summary =
                "Study how real electrical constraints should shape software decisions, review gates, and drafting workflows.",
            NextMilestone =
                "Next: explain one design constraint from memory and tie it to a Suite feature.",
        },
        new()
        {
            Title = "Standards, drawings, and operator trust",
            Summary =
                "Use drawing QA, title blocks, standards checks, and transmittals as a path to stronger EE production judgment.",
            NextMilestone =
                "Next: build one challenge around standards-checking logic and human review.",
        },
        new()
        {
            Title = "Automation with deterministic review gates",
            Summary =
                "Learn where automation helps, where it must stop, and how to structure preview, validate, and execute phases.",
            NextMilestone =
                "Next: explain why review-first automation is more credible than full autonomy in this domain.",
        },
    ];

    private static string GetUniqueKnowledgeImportPath(string targetDirectory, string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidatePath = Path.Combine(targetDirectory, fileName);
        var copyIndex = 2;

        while (File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(targetDirectory, $"{baseName} ({copyIndex}){extension}");
            copyIndex++;
        }

        return candidatePath;
    }

    private static string JoinOrNone(IReadOnlyList<string> items) =>
        items.Count == 0 ? "none recorded" : string.Join("; ", items);

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..maxLength].Trim()}...";
    }

    // --- ML Pipeline ---

    public async Task<MLAnalyticsResult> RunMLAnalyticsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            var attempts = _trainingStore.LoadAllAttempts();
            var decisions = _operatorMemoryState.Activities;
            _latestMLAnalytics = await _mlAnalyticsService.RunLearningAnalyticsAsync(
                attempts,
                decisions,
                cancellationToken
            );
            _lastMLRunAt = DateTimeOffset.Now;
            return _latestMLAnalytics;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MLForecastResult> RunMLForecastAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            var attempts = _trainingStore.LoadAllAttempts();
            _latestMLForecast = await _mlAnalyticsService.RunProgressForecastAsync(
                attempts,
                cancellationToken
            );
            _lastMLRunAt = DateTimeOffset.Now;
            return _latestMLForecast;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MLEmbeddingsResult> RunMLEmbeddingsAsync(
        string? query = null,
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);
            _latestMLEmbeddings = await _mlAnalyticsService.RunDocumentEmbeddingsAsync(
                _learningLibrary.Documents,
                query,
                cancellationToken
            );
            _lastMLRunAt = DateTimeOffset.Now;
            return _latestMLEmbeddings;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<object> RunFullMLPipelineAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var attempts = _trainingStore.LoadAllAttempts();
            var decisions = _operatorMemoryState.Activities;

            _latestMLAnalytics = await _mlAnalyticsService.RunLearningAnalyticsAsync(
                attempts,
                decisions,
                cancellationToken
            );
            _latestMLForecast = await _mlAnalyticsService.RunProgressForecastAsync(
                attempts,
                cancellationToken
            );
            _latestMLEmbeddings = await _mlAnalyticsService.RunDocumentEmbeddingsAsync(
                _learningLibrary.Documents,
                null,
                cancellationToken
            );

            var artifacts = await _mlAnalyticsService.GenerateSuiteArtifactsAsync(
                _latestMLAnalytics,
                _latestMLEmbeddings,
                _latestMLForecast,
                cancellationToken
            );

            var exportPath = await _mlAnalyticsService.ExportArtifactsAsync(
                artifacts,
                _stateRootPath,
                cancellationToken
            );

            _lastMLArtifactExportPath = exportPath;
            _lastMLRunAt = DateTimeOffset.Now;

            return new
            {
                analytics = _latestMLAnalytics,
                forecast = _latestMLForecast,
                embeddings = _latestMLEmbeddings,
                artifacts,
                exportPath,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SuiteMLArtifactBundle> ExportSuiteArtifactsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureInitializedLockedAsync(cancellationToken);

            var analytics = _latestMLAnalytics ?? new MLAnalyticsResult { Ok = true, Engine = "not-run" };
            var embeddings = _latestMLEmbeddings ?? new MLEmbeddingsResult { Ok = true, Engine = "not-run" };
            var forecast = _latestMLForecast ?? new MLForecastResult { Ok = true, Engine = "not-run" };

            var artifacts = await _mlAnalyticsService.GenerateSuiteArtifactsAsync(
                analytics,
                embeddings,
                forecast,
                cancellationToken
            );

            var exportPath = await _mlAnalyticsService.ExportArtifactsAsync(
                artifacts,
                _stateRootPath,
                cancellationToken
            );

            _lastMLArtifactExportPath = exportPath;
            return artifacts;
        }
        finally
        {
            _gate.Release();
        }
    }

    private OfficeMLSection BuildMLSectionLocked()
    {
        if (!_settings.EnableMLPipeline)
        {
            return new OfficeMLSection
            {
                Enabled = false,
                Summary = "ML pipeline is not enabled. Set enableMLPipeline to true in settings.",
            };
        }

        var summary = _latestMLAnalytics is not null
            ? $"ML pipeline active ({_latestMLAnalytics.Engine}). Readiness: {_latestMLAnalytics.OverallReadiness:P0}. " +
              $"Weak topics: {_latestMLAnalytics.WeakTopics.Count}. " +
              $"Forecast engine: {_latestMLForecast?.Engine ?? "not run"}. " +
              $"Embeddings: {_latestMLEmbeddings?.Engine ?? "not run"}."
            : "ML pipeline is enabled but has not been run yet. Use the ML endpoints to analyze your learning data.";

        return new OfficeMLSection
        {
            Enabled = true,
            Summary = summary,
            Analytics = _latestMLAnalytics,
            Forecast = _latestMLForecast,
            Embeddings = _latestMLEmbeddings,
            LastArtifactExportPath = _lastMLArtifactExportPath,
            LastRunAt = _lastMLRunAt,
        };
    }
}
