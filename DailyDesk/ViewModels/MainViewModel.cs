using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Input;
using Microsoft.Win32;
using DailyDesk.Models;
using DailyDesk.Services;

namespace DailyDesk.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan InstalledModelsLoadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SuiteSnapshotLoadTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan TrainingHistoryLoadTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LearningLibraryLoadTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan OperatorMemoryLoadTimeout = TimeSpan.FromSeconds(5);

    private readonly DailySettings _settings;
    private readonly IModelProvider _ollamaService;
    private readonly SuiteSnapshotService _suiteSnapshotService;
    private readonly TrainingGeneratorService _trainingGeneratorService;
    private readonly TrainingStore _trainingStore;
    private readonly KnowledgeImportService _knowledgeImportService;
    private readonly LearningProfileService _learningProfileService;
    private readonly OralDefenseService _oralDefenseService;
    private readonly LiveResearchService _liveResearchService;
    private readonly string _knowledgeLibraryPath;
    private readonly string _stateRootPath;
    private readonly IReadOnlyList<string> _additionalKnowledgePaths;

    private readonly RelayCommand _refreshContextCommand;
    private readonly RelayCommand _generateChiefBriefCommand;
    private readonly RelayCommand _generateChallengeCommand;
    private readonly RelayCommand _generateMonetizationCommand;
    private readonly RelayCommand _generatePracticeTestCommand;
    private readonly RelayCommand _scorePracticeTestCommand;
    private readonly RelayCommand _generateOralDefenseCommand;
    private readonly RelayCommand _scoreOralDefenseCommand;
    private readonly RelayCommand _saveSessionReflectionCommand;
    private readonly RelayCommand _startRecommendedReviewCommand;
    private readonly RelayCommand _runLiveResearchCommand;
    private readonly RelayCommand _saveResearchToKnowledgeCommand;
    private readonly RelayCommand _openExternalLinkCommand;
    private readonly RelayCommand _openKnowledgeFolderCommand;
    private readonly RelayCommand _importKnowledgeFilesCommand;

    private SuiteSnapshot _suiteSnapshot = new();
    private TrainingHistorySummary _trainingHistorySummary = new();
    private LearningLibrary _learningLibrary = new();
    private LearningProfile _learningProfile = new();
    private OralDefenseScenario _oralDefenseScenario = new();
    private ResearchReport _currentResearchReport = new();
    private PracticeTest? _currentPracticeTest;
    private IReadOnlyList<string> _installedModelCache = Array.Empty<string>();

    private string _dailyBrief = "Load the first context snapshot to get a tailored daily brief.";
    private string _challengeBrief =
        "Generate a challenge when you want the EE mentor to pressure-test your current focus.";
    private string _monetizationBrief =
        "Generate a business map to tie current Suite work to a future paid offer.";
    private string _suitePulse = "Suite awareness will appear after the first refresh.";
    private string _statusMessage = "Ready.";
    private string _installedModelSummary = "Waiting on local model discovery.";
    private string _practiceFocusText =
        "Protection, grounding, standards, drafting safety";
    private string _practiceQuestionCountText = "6";
    private string _selectedPracticeDifficulty = "Mixed";
    private string _currentPracticeTitle = "No active practice test.";
    private string _currentPracticeOverview =
        "Generate a practice test to build a local training loop.";
    private string _practiceResultSummary = "No scored practice yet.";
    private string _trainingOverallSummary = "No scored practice history yet.";
    private string _trainingNextActionSummary =
        "Score a practice test to unlock adaptive training recommendations.";
    private string _reviewQueueSummary =
        "No review queue yet. Score a practice set to schedule follow-up work.";
    private string _researchQueryText =
        "electrical drawing QA workflows review gates automation";
    private string _selectedResearchMode = "EE Mentor";
    private string _researchSummary =
        "Run a live research query to pull current web sources into the desk.";
    private string _researchRunSummary = "No live research run yet.";
    private string _learningLibrarySummary = "Knowledge folder not scanned yet.";
    private string _learningProfileSummary = "Learning profile not built yet.";
    private string _oralDefenseTitle = "No oral defense loaded.";
    private string _oralDefensePrompt =
        "Generate an oral defense drill to pressure-test reasoning, tradeoffs, and communication.";
    private string _oralDefenseGoodLooksLike =
        "A strong response should explain the governing principle, tradeoffs, failure modes, and validation.";
    private string _oralDefenseSuiteConnection =
        "Tie the explanation back to operator trust, review gates, or production reliability in Suite.";
    private string _oralDefenseSource = "not generated";
    private string _oralDefenseAnswerText = string.Empty;
    private string _oralDefenseScoreSummary = "No scored oral-defense answer yet.";
    private string _oralDefenseFeedbackSummary =
        "Score a typed answer to get rubric feedback and follow-up coaching.";
    private string _defenseHistorySummary = "No scored oral-defense history yet.";
    private string _sessionReflectionText = string.Empty;
    private string _reflectionContextSummary =
        "Score a practice or defense session to save a reflection.";
    private string _lastScoredSessionMode = string.Empty;
    private string _lastScoredSessionFocus = string.Empty;
    private string _activeReviewTopic = string.Empty;
    private bool _isBusy;

    public MainViewModel()
    {
        _settings = DailySettings.Load(AppContext.BaseDirectory);

        var processRunner = new ProcessRunner();
        var configuredProviderId = (_settings.PrimaryModelProvider ?? string.Empty).Trim();
        _knowledgeLibraryPath = _settings.ResolveKnowledgeLibraryPath(AppContext.BaseDirectory);
        _stateRootPath = _settings.ResolveStateRootPath(AppContext.BaseDirectory);
        _ollamaService = new OllamaService(_settings.OllamaEndpoint, processRunner);
        if (
            !configuredProviderId.Equals(
                OllamaService.OllamaProviderId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            _installedModelSummary =
                $"Configured provider '{configuredProviderId}' is not available yet. Using {OllamaService.OllamaProviderLabel}.";
        }

        _suiteSnapshotService = new SuiteSnapshotService(
            processRunner,
            _settings.SuiteRuntimeStatusEndpoint
        );
        _trainingGeneratorService = new TrainingGeneratorService(
            _ollamaService,
            _settings.TrainingModel
        );
        _trainingStore = new TrainingStore(_stateRootPath);
        _knowledgeImportService = new KnowledgeImportService(
            processRunner,
            Path.Combine(AppContext.BaseDirectory, "Scripts", "extract_document_text.py")
        );
        _additionalKnowledgePaths = _settings.ResolveAdditionalKnowledgePaths();
        _learningProfileService = new LearningProfileService();
        _oralDefenseService = new OralDefenseService(_ollamaService, _settings.MentorModel);
        _liveResearchService = new LiveResearchService(_ollamaService);
        InitializeOperatorLayer();
        InitializeWorkflowLayer();
        InitializeAgentOfficeLayer();

        _refreshContextCommand = new RelayCommand(
            async _ => await RefreshContextAsync(),
            _ => !IsBusy
        );
        _generateChiefBriefCommand = new RelayCommand(
            async _ => await GenerateChiefBriefAsync(),
            _ => !IsBusy
        );
        _generateChallengeCommand = new RelayCommand(
            async _ => await GenerateChallengeAsync(),
            _ => !IsBusy
        );
        _generateMonetizationCommand = new RelayCommand(
            async _ => await GenerateMonetizationAsync(),
            _ => !IsBusy
        );
        _generatePracticeTestCommand = new RelayCommand(
            async _ => await GeneratePracticeTestAsync(),
            _ => !IsBusy
        );
        _scorePracticeTestCommand = new RelayCommand(
            async _ => await ScorePracticeTestAsync(),
            _ => !IsBusy && PracticeQuestions.Count > 0
        );
        _generateOralDefenseCommand = new RelayCommand(
            async _ => await GenerateOralDefenseAsync(),
            _ => !IsBusy
        );
        _scoreOralDefenseCommand = new RelayCommand(
            async _ => await ScoreOralDefenseAsync(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(OralDefenseAnswerText)
        );
        _saveSessionReflectionCommand = new RelayCommand(
            async _ => await SaveSessionReflectionAsync(),
            _ =>
                !IsBusy
                && !string.IsNullOrWhiteSpace(SessionReflectionText)
                && !string.IsNullOrWhiteSpace(_lastScoredSessionMode)
        );
        _startRecommendedReviewCommand = new RelayCommand(
            async parameter => await StartRecommendedReviewAsync(parameter as ReviewRecommendation),
            parameter => !IsBusy && parameter is ReviewRecommendation
        );
        _runLiveResearchCommand = new RelayCommand(
            async _ => await RunLiveResearchAsync(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(ResearchQueryText)
        );
        _saveResearchToKnowledgeCommand = new RelayCommand(
            async _ => await SaveResearchToKnowledgeAsync(),
            _ => !IsBusy && _currentResearchReport.Sources.Count > 0
        );
        _openExternalLinkCommand = new RelayCommand(
            parameter => OpenExternalLink(parameter as string),
            parameter => parameter is string value && !string.IsNullOrWhiteSpace(value)
        );
        _openKnowledgeFolderCommand = new RelayCommand(_ => OpenKnowledgeFolder());
        _importKnowledgeFilesCommand = new RelayCommand(
            async _ => await ImportKnowledgeFilesAsync(),
            _ => !IsBusy
        );

        SeedPracticeDifficulties();
        SeedResearchModes();
        SeedStudyTracks();
        SeedGuardrails();
        SeedCareerSignals();
    }

    public ObservableCollection<AgentCard> Agents { get; } = new();

    public ObservableCollection<FocusCard> FocusCards { get; } = new();

    public ObservableCollection<StudyTrack> StudyTracks { get; } = new();

    public ObservableCollection<string> ChangedFiles { get; } = new();

    public ObservableCollection<string> RecentCommits { get; } = new();

    public ObservableCollection<string> NextSessionTasks { get; } = new();

    public ObservableCollection<string> MonetizationMoves { get; } = new();

    public ObservableCollection<string> ProductPillars { get; } = new();

    public ObservableCollection<string> HotAreas { get; } = new();

    public ObservableCollection<string> Guardrails { get; } = new();

    public ObservableCollection<string> InstalledModels { get; } = new();

    public ObservableCollection<string> CareerSignals { get; } = new();

    public ObservableCollection<QueueItem> QueueItems { get; } = new();

    public ObservableCollection<string> PracticeDifficultyOptions { get; } = new();

    public ObservableCollection<string> ResearchModeOptions { get; } = new();

    public ObservableCollection<TrainingQuestion> PracticeQuestions { get; } = new();

    public ObservableCollection<ResearchSource> ResearchSources { get; } = new();

    public ObservableCollection<string> WeakTopicSummaries { get; } = new();

    public ObservableCollection<string> RecentPracticeSummaries { get; } = new();

    public ObservableCollection<string> TrainingPriorityMoves { get; } = new();

    public ObservableCollection<string> ResearchTakeawaySummaries { get; } = new();

    public ObservableCollection<string> ResearchActionSummaries { get; } = new();

    public ObservableCollection<string> ReviewRecommendationSummaries { get; } = new();

    public ObservableCollection<ReviewRecommendation> ReviewRecommendations { get; } = new();

    public ObservableCollection<string> ImportedDocumentSummaries { get; } = new();

    public ObservableCollection<string> KnowledgeSourceSummaries { get; } = new();

    public ObservableCollection<string> LearningRuleSummaries { get; } = new();

    public ObservableCollection<string> LearningTopicSummaries { get; } = new();

    public ObservableCollection<string> OralDefenseFollowUpSummaries { get; } = new();

    public ObservableCollection<DefenseRubricItem> OralDefenseRubricItems { get; } = new();

    public ObservableCollection<string> RecentDefenseSummaries { get; } = new();

    public ObservableCollection<string> RecentReflectionSummaries { get; } = new();

    public string DailyBrief
    {
        get => _dailyBrief;
        private set => SetProperty(ref _dailyBrief, value);
    }

    public string ChallengeBrief
    {
        get => _challengeBrief;
        private set => SetProperty(ref _challengeBrief, value);
    }

    public string MonetizationBrief
    {
        get => _monetizationBrief;
        private set => SetProperty(ref _monetizationBrief, value);
    }

    public string SuitePulse
    {
        get => _suitePulse;
        private set => SetProperty(ref _suitePulse, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string InstalledModelSummary
    {
        get => _installedModelSummary;
        private set => SetProperty(ref _installedModelSummary, value);
    }

    public string PracticeFocusText
    {
        get => _practiceFocusText;
        set => SetProperty(ref _practiceFocusText, value);
    }

    public string PracticeQuestionCountText
    {
        get => _practiceQuestionCountText;
        set => SetProperty(ref _practiceQuestionCountText, value);
    }

    public string SelectedPracticeDifficulty
    {
        get => _selectedPracticeDifficulty;
        set => SetProperty(ref _selectedPracticeDifficulty, value);
    }

    public string CurrentPracticeTitle
    {
        get => _currentPracticeTitle;
        private set => SetProperty(ref _currentPracticeTitle, value);
    }

    public string CurrentPracticeOverview
    {
        get => _currentPracticeOverview;
        private set => SetProperty(ref _currentPracticeOverview, value);
    }

    public string PracticeResultSummary
    {
        get => _practiceResultSummary;
        private set => SetProperty(ref _practiceResultSummary, value);
    }

    public string TrainingOverallSummary
    {
        get => _trainingOverallSummary;
        private set => SetProperty(ref _trainingOverallSummary, value);
    }

    public string TrainingNextActionSummary
    {
        get => _trainingNextActionSummary;
        private set => SetProperty(ref _trainingNextActionSummary, value);
    }

    public string ReviewQueueSummary
    {
        get => _reviewQueueSummary;
        private set => SetProperty(ref _reviewQueueSummary, value);
    }

    public string ResearchQueryText
    {
        get => _researchQueryText;
        set
        {
            if (SetProperty(ref _researchQueryText, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string SelectedResearchMode
    {
        get => _selectedResearchMode;
        set => SetProperty(ref _selectedResearchMode, value);
    }

    public string ResearchSummary
    {
        get => _researchSummary;
        private set => SetProperty(ref _researchSummary, value);
    }

    public string ResearchRunSummary
    {
        get => _researchRunSummary;
        private set => SetProperty(ref _researchRunSummary, value);
    }

    public string LearningLibrarySummary
    {
        get => _learningLibrarySummary;
        private set => SetProperty(ref _learningLibrarySummary, value);
    }

    public string LearningProfileSummary
    {
        get => _learningProfileSummary;
        private set => SetProperty(ref _learningProfileSummary, value);
    }

    public string OralDefenseTitle
    {
        get => _oralDefenseTitle;
        private set => SetProperty(ref _oralDefenseTitle, value);
    }

    public string OralDefensePrompt
    {
        get => _oralDefensePrompt;
        private set => SetProperty(ref _oralDefensePrompt, value);
    }

    public string OralDefenseGoodLooksLike
    {
        get => _oralDefenseGoodLooksLike;
        private set => SetProperty(ref _oralDefenseGoodLooksLike, value);
    }

    public string OralDefenseSuiteConnection
    {
        get => _oralDefenseSuiteConnection;
        private set => SetProperty(ref _oralDefenseSuiteConnection, value);
    }

    public string OralDefenseSource
    {
        get => _oralDefenseSource;
        private set => SetProperty(ref _oralDefenseSource, value);
    }

    public string OralDefenseAnswerText
    {
        get => _oralDefenseAnswerText;
        set
        {
            if (SetProperty(ref _oralDefenseAnswerText, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string OralDefenseScoreSummary
    {
        get => _oralDefenseScoreSummary;
        private set => SetProperty(ref _oralDefenseScoreSummary, value);
    }

    public string OralDefenseFeedbackSummary
    {
        get => _oralDefenseFeedbackSummary;
        private set => SetProperty(ref _oralDefenseFeedbackSummary, value);
    }

    public string DefenseHistorySummary
    {
        get => _defenseHistorySummary;
        private set => SetProperty(ref _defenseHistorySummary, value);
    }

    public string SessionReflectionText
    {
        get => _sessionReflectionText;
        set
        {
            if (SetProperty(ref _sessionReflectionText, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ReflectionContextSummary
    {
        get => _reflectionContextSummary;
        private set => SetProperty(ref _reflectionContextSummary, value);
    }

    public string SuiteRepoPath => _settings.SuiteRepoPath;

    public string KnowledgeLibraryPath => _knowledgeLibraryPath;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            _refreshContextCommand.RaiseCanExecuteChanged();
            _generateChiefBriefCommand.RaiseCanExecuteChanged();
            _generateChallengeCommand.RaiseCanExecuteChanged();
            _generateMonetizationCommand.RaiseCanExecuteChanged();
            _generatePracticeTestCommand.RaiseCanExecuteChanged();
            _scorePracticeTestCommand.RaiseCanExecuteChanged();
            _generateOralDefenseCommand.RaiseCanExecuteChanged();
            _scoreOralDefenseCommand.RaiseCanExecuteChanged();
            _saveSessionReflectionCommand.RaiseCanExecuteChanged();
            _startRecommendedReviewCommand.RaiseCanExecuteChanged();
            _runLiveResearchCommand.RaiseCanExecuteChanged();
            _saveResearchToKnowledgeCommand.RaiseCanExecuteChanged();
            _openExternalLinkCommand.RaiseCanExecuteChanged();
            _importKnowledgeFilesCommand.RaiseCanExecuteChanged();
            RaiseOperatorCommandState();
            RaiseWorkflowCommandState();
            RaiseAgentOfficeCommandState();
        }
    }

    public ICommand RefreshContextCommand => _refreshContextCommand;

    public ICommand GenerateChiefBriefCommand => _generateChiefBriefCommand;

    public ICommand GenerateChallengeCommand => _generateChallengeCommand;

    public ICommand GenerateMonetizationCommand => _generateMonetizationCommand;

    public ICommand GeneratePracticeTestCommand => _generatePracticeTestCommand;

    public ICommand ScorePracticeTestCommand => _scorePracticeTestCommand;

    public ICommand GenerateOralDefenseCommand => _generateOralDefenseCommand;

    public ICommand ScoreOralDefenseCommand => _scoreOralDefenseCommand;

    public ICommand SaveSessionReflectionCommand => _saveSessionReflectionCommand;

    public ICommand StartRecommendedReviewCommand => _startRecommendedReviewCommand;

    public ICommand RunLiveResearchCommand => _runLiveResearchCommand;

    public ICommand SaveResearchToKnowledgeCommand => _saveResearchToKnowledgeCommand;

    public ICommand OpenExternalLinkCommand => _openExternalLinkCommand;

    public ICommand OpenKnowledgeFolderCommand => _openKnowledgeFolderCommand;

    public ICommand ImportKnowledgeFilesCommand => _importKnowledgeFilesCommand;

    public async Task InitializeAsync() => await RefreshContextAsync(startup: true);

    private async Task RefreshContextAsync(bool startup = false)
    {
        var job = StartJob(
            "Refresh Desk",
            "Chief of Staff",
            "local context",
            "Refreshing models, Suite context, knowledge, and training history.",
            blocking: !startup
        );
        IsBusy = true;
        StatusMessage = startup
            ? "Loading Office context in the background..."
            : "Refreshing local Ollama models, Suite context, learning library, and training history...";

        try
        {
            var installedModelsTask = LoadInstalledModelsSafeAsync();
            var suiteSnapshotTask = LoadSuiteSnapshotSafeAsync();
            var historySummaryTask = LoadTrainingHistorySummarySafeAsync();
            var learningLibraryTask = LoadLearningLibrarySafeAsync();
            var operatorMemoryTask = LoadOperatorMemoryStateSafeAsync();

            await Task.WhenAll(
                installedModelsTask,
                suiteSnapshotTask,
                historySummaryTask,
                learningLibraryTask,
                operatorMemoryTask
            );

            _installedModelCache = await installedModelsTask;
            _suiteSnapshot = await suiteSnapshotTask;
            var historySummary = await historySummaryTask;
            _learningLibrary = await learningLibraryTask;
            _learningProfile = _learningProfileService.Build(
                _learningLibrary,
                historySummary,
                _suiteSnapshot
            );
            _operatorMemoryState = await operatorMemoryTask;

            Replace(InstalledModels, _installedModelCache);
            Replace(ChangedFiles, _suiteSnapshot.ChangedFiles);
            Replace(RecentCommits, _suiteSnapshot.RecentCommits);
            Replace(NextSessionTasks, _suiteSnapshot.NextSessionTasks);
            Replace(MonetizationMoves, _suiteSnapshot.MonetizationMoves);
            Replace(ProductPillars, _suiteSnapshot.ProductPillars);
            Replace(HotAreas, _suiteSnapshot.HotAreas);
            Replace(Agents, BuildAgents(_installedModelCache));
            ApplyLearningState(_learningLibrary, _learningProfile);
            ApplyTrainingHistorySummary(historySummary);
            ApplyOperatorState(_operatorMemoryState);
            LoadAgentReplyGuide();
            RefreshTrainingHistoryMetadata();
            RefreshTrainingSessionState();

            InstalledModelSummary = _installedModelCache.Count == 0
                ? "No local models discovered. Start Ollama or check the endpoint."
                : $"{_installedModelCache.Count} local models found. Primary rack: {string.Join(", ", _installedModelCache.Take(5))}";

            SuitePulse = BuildSuitePulse(_suiteSnapshot);
            DailyBrief = BuildFallbackDailyBrief(
                _suiteSnapshot,
                _learningProfile,
                _trainingHistorySummary
            );
            ChallengeBrief = BuildFallbackChallenge(
                _suiteSnapshot,
                _learningProfile,
                _trainingHistorySummary
            );
            MonetizationBrief = BuildFallbackMonetizationBrief(
                _suiteSnapshot,
                _learningProfile,
                _trainingHistorySummary
            );

            StatusMessage = _suiteSnapshot.RepoAvailable
                ? startup
                    ? $"Office loaded. {_learningLibrary.Documents.Count} learning files are ready."
                    : $"Context refreshed. {_learningLibrary.Documents.Count} learning files loaded and Daily Desk is ready to train."
                : "Context refreshed, but the configured Suite repo path was not found.";
            CompleteJob(job, StatusMessage);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Context refresh failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<IReadOnlyList<string>> LoadInstalledModelsSafeAsync()
    {
        return await RunWithTimeoutFallbackAsync(
            token => _ollamaService.GetInstalledModelsAsync(token),
            InstalledModelsLoadTimeout,
            static () => Array.Empty<string>()
        );
    }

    private async Task<SuiteSnapshot> LoadSuiteSnapshotSafeAsync()
    {
        return await RunWithTimeoutFallbackAsync(
            token => _suiteSnapshotService.LoadAsync(_settings.SuiteRepoPath, token),
            SuiteSnapshotLoadTimeout,
            BuildSuiteSnapshotTimeoutFallback
        );
    }

    private async Task<TrainingHistorySummary> LoadTrainingHistorySummarySafeAsync()
    {
        return await RunWithTimeoutFallbackAsync(
            token => _trainingStore.LoadSummaryAsync(token),
            TrainingHistoryLoadTimeout,
            static () => new TrainingHistorySummary()
        );
    }

    private async Task<LearningLibrary> LoadLearningLibrarySafeAsync()
    {
        return await RunWithTimeoutFallbackAsync(
            token => _knowledgeImportService.LoadAsync(
                _knowledgeLibraryPath,
                _additionalKnowledgePaths,
                token
            ),
            LearningLibraryLoadTimeout,
            BuildLearningLibraryTimeoutFallback
        );
    }

    private async Task<OperatorMemoryState> LoadOperatorMemoryStateSafeAsync()
    {
        return await RunWithTimeoutFallbackAsync(
            token => _operatorMemoryStore.LoadAsync(token),
            OperatorMemoryLoadTimeout,
            () => _operatorMemoryState
        );
    }

    private async Task<T> RunWithTimeoutFallbackAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        Func<T> fallbackFactory
    )
    {
        using var timeoutScope = new CancellationTokenSource();
        var operationTask = operation(timeoutScope.Token);
        var timeoutTask = Task.Delay(timeout);
        var completedTask = await Task.WhenAny(operationTask, timeoutTask);
        if (completedTask == operationTask)
        {
            try
            {
                return await operationTask;
            }
            catch
            {
                return fallbackFactory();
            }
        }

        timeoutScope.Cancel();
        _ = operationTask.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
        return fallbackFactory();
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
                "Office kept loading with local context so the desk stays usable. Use Refresh Office later to retry the shared Suite snapshot.",
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

    private async Task GenerateChiefBriefAsync()
    {
        var job = StartJob(
            "Chief Brief",
            "Chief of Staff",
            _settings.ChiefModel,
            "Generating the daily routing brief."
        );
        await GenerateWithFallbackAsync(
            _settings.ChiefModel,
            PromptComposer.BuildChiefSystemPrompt(),
            PromptComposer.BuildChiefUserPrompt(
                _suiteSnapshot,
                _installedModelCache,
                _learningProfile,
                _learningLibrary,
                _trainingHistorySummary
            ),
            () => BuildFallbackDailyBrief(_suiteSnapshot, _learningProfile, _trainingHistorySummary),
            value => DailyBrief = value,
            "Generating chief brief from local Ollama..."
        );
        CompleteJob(job, StatusMessage);
        await AppendDeskOutputAsync(ChiefDeskId, "Chief of Staff", "brief", DailyBrief);

        await RecordActivityAsync(
            CreateActivity("chief_pass", "Chief of Staff", "daily routing", DailyBrief)
        );
    }

    private async Task GenerateChallengeAsync()
    {
        var job = StartJob(
            "EE Challenge",
            "EE Mentor",
            _settings.MentorModel,
            "Generating the next challenge drill."
        );
        await GenerateWithFallbackAsync(
            _settings.MentorModel,
            PromptComposer.BuildChallengeSystemPrompt(),
            PromptComposer.BuildChallengeUserPrompt(
                _suiteSnapshot,
                _learningProfile,
                _learningLibrary,
                _trainingHistorySummary
            ),
            () => BuildFallbackChallenge(_suiteSnapshot, _learningProfile, _trainingHistorySummary),
            value => ChallengeBrief = value,
            "Generating EE challenge..."
        );
        CompleteJob(job, StatusMessage);
        await AppendDeskOutputAsync(EngineeringDeskId, "Engineering Desk", "challenge", ChallengeBrief);

        await RecordActivityAsync(
            CreateActivity("challenge_generated", "EE Mentor", "training", ChallengeBrief)
        );
    }

    private async Task GenerateStudyGuideAsync()
    {
        var notebookLibrary = BuildNotebookOnlyLibrary();
        var focus = ResolveStudyGuideFocus(notebookLibrary);
        var job = StartJob(
            "Study Guide",
            "EE Mentor",
            _settings.MentorModel,
            "Building a study guide from imported notes."
        );

        IsBusy = true;
        StatusMessage = "Generating notebook-grounded study guide...";

        try
        {
            if (notebookLibrary.Documents.Count == 0)
            {
                var missingNotebookGuide = BuildMissingNotebookStudyGuideMessage();
                StatusMessage = "No OneNote package is currently loaded into Office.";
                CompleteJob(job, StatusMessage);
                await AppendDeskOutputAsync(
                    EngineeringDeskId,
                    "Engineering Desk",
                    "study_guide",
                    missingNotebookGuide
                );
                return;
            }

            var generated = await _ollamaService.GenerateAsync(
                _settings.MentorModel,
                PromptComposer.BuildStudyGuideSystemPrompt(),
                PromptComposer.BuildStudyGuideUserPrompt(
                    focus,
                    _learningProfile,
                    notebookLibrary,
                    _trainingHistorySummary
                )
            );

            var studyGuide = string.IsNullOrWhiteSpace(generated)
                ? BuildFallbackStudyGuide(focus, notebookLibrary)
                : generated.Trim();

            PracticeFocusText = focus;
            if (string.IsNullOrWhiteSpace(_activeReviewTopic))
            {
                _activeReviewTopic = focus;
            }

            SelectedPrimaryTabIndex = OfficeTabIndex;
            StatusMessage = $"Study guide ready using {_settings.MentorModel}.";
            CompleteJob(job, StatusMessage);
            await AppendDeskOutputAsync(
                EngineeringDeskId,
                "Engineering Desk",
                "study_guide",
                studyGuide
            );

            await RecordActivityAsync(
                CreateActivity("study_guide_generated", "EE Mentor", focus, studyGuide)
            );
        }
        catch (Exception exception)
        {
            var fallback = BuildFallbackStudyGuide(focus, notebookLibrary);
            PracticeFocusText = focus;
            if (string.IsNullOrWhiteSpace(_activeReviewTopic))
            {
                _activeReviewTopic = focus;
            }

            StatusMessage = $"Study guide generation fell back to local heuristics: {exception.Message}";
            CompleteJob(job, StatusMessage);
            await AppendDeskOutputAsync(
                EngineeringDeskId,
                "Engineering Desk",
                "study_guide",
                fallback
            );
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GenerateMonetizationAsync()
    {
        var job = StartJob(
            "Business Map",
            "Business Strategist",
            _settings.BusinessModel,
            "Turning current work into future offer framing."
        );
        await GenerateWithFallbackAsync(
            _settings.BusinessModel,
            PromptComposer.BuildBusinessSystemPrompt(),
            PromptComposer.BuildBusinessUserPrompt(
                _suiteSnapshot,
                _learningProfile,
                _learningLibrary,
                _trainingHistorySummary
            ),
            () =>
                BuildFallbackMonetizationBrief(
                    _suiteSnapshot,
                    _learningProfile,
                    _trainingHistorySummary
                ),
            value => MonetizationBrief = value,
            "Generating productization map..."
        );
        CompleteJob(job, StatusMessage);
        await AppendDeskOutputAsync(BusinessDeskId, "Business Ops", "business_map", MonetizationBrief);

        await RecordActivityAsync(
            CreateActivity(
                "business_map",
                "Business Strategist",
                "monetization",
                MonetizationBrief
            )
        );
    }

    private async Task GeneratePracticeTestAsync()
    {
        var job = StartJob(
            "Practice Test",
            "Test Builder",
            _settings.TrainingModel,
            "Building the next practice set."
        );
        IsBusy = true;
        StatusMessage = "Generating practice test...";

        try
        {
            var questionCount = ParseQuestionCount();
            var focus = string.IsNullOrWhiteSpace(PracticeFocusText)
                ? "Protection, grounding, standards, drafting safety"
                : PracticeFocusText.Trim();
            var difficulty = string.IsNullOrWhiteSpace(SelectedPracticeDifficulty)
                ? "Mixed"
                : SelectedPracticeDifficulty.Trim();
            var focusReason = string.IsNullOrWhiteSpace(_activeReviewTopic)
                ? "Manual focus chosen for the guided training session."
                : $"Routed from review queue for {_activeReviewTopic}.";

            _currentPracticeTest = await _trainingGeneratorService.CreatePracticeTestAsync(
                focus,
                difficulty,
                questionCount,
                _suiteSnapshot,
                _trainingHistorySummary,
                _learningProfile,
                _learningLibrary,
                StudyTracks.ToList()
            );

            Replace(PracticeQuestions, _currentPracticeTest.Questions);
            CurrentPracticeTitle = _currentPracticeTest.Title;
            CurrentPracticeOverview =
                $"{_currentPracticeTest.Overview}{Environment.NewLine}{Environment.NewLine}Source: {_currentPracticeTest.GenerationSource}";
            PracticeResultSummary =
                $"Generated {_currentPracticeTest.Questions.Count} questions for '{focus}' at {difficulty} difficulty.";
            SelectedPrimaryTabIndex = TrainingSessionTabIndex;
            MarkPracticeGenerated(focus, focusReason);

            StatusMessage =
                $"Practice test ready using {_currentPracticeTest.GenerationSource}.";
            CompleteJob(job, StatusMessage);
            await AppendDeskOutputAsync(
                EngineeringDeskId,
                "Engineering Desk",
                "practice",
                $"{CurrentPracticeTitle}\n\n{PracticeResultSummary}\n\n{CurrentPracticeOverview}"
            );
        }
        catch (Exception exception)
        {
            StatusMessage = $"Practice generation failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            _scorePracticeTestCommand.RaiseCanExecuteChanged();
            IsBusy = false;
        }
    }

    private async Task GenerateOralDefenseAsync()
    {
        var job = StartJob(
            "Oral Defense",
            "EE Mentor",
            _settings.MentorModel,
            "Preparing a same-topic oral defense drill."
        );
        IsBusy = true;
        StatusMessage = "Generating oral defense drill...";

        try
        {
            var scenario = await _oralDefenseService.CreateScenarioAsync(
                _suiteSnapshot,
                _trainingHistorySummary,
                _learningProfile,
                _learningLibrary,
                StudyTracks.ToList(),
                string.IsNullOrWhiteSpace(_activeReviewTopic) ? null : _activeReviewTopic
            );

            ApplyOralDefenseScenario(scenario);
            MarkDefenseGenerated(
                string.IsNullOrWhiteSpace(scenario.Topic) ? ResolveTrainingSessionFocus() : scenario.Topic,
                string.IsNullOrWhiteSpace(_activeReviewTopic)
                    ? "Defense linked to the current guided session."
                    : $"Defense routed from review target: {_activeReviewTopic}."
            );
            SelectedPrimaryTabIndex = TrainingSessionTabIndex;
            StatusMessage = $"Oral defense drill ready using {scenario.GenerationSource}.";
            CompleteJob(job, StatusMessage);
            await AppendDeskOutputAsync(
                EngineeringDeskId,
                "Engineering Desk",
                "defense",
                $"{OralDefenseTitle}\n\n{OralDefensePrompt}\n\nWhat good looks like: {OralDefenseGoodLooksLike}"
            );
        }
        catch (Exception exception)
        {
            ApplyOralDefenseScenario(
                BuildFallbackOralDefenseScenario(
                    _suiteSnapshot,
                    _trainingHistorySummary,
                    _learningProfile,
                    _learningLibrary,
                    string.IsNullOrWhiteSpace(_activeReviewTopic) ? null : _activeReviewTopic
                )
            );
            MarkDefenseGenerated(
                ResolveTrainingSessionFocus(),
                string.IsNullOrWhiteSpace(_activeReviewTopic)
                    ? "Fallback defense linked to the current guided session."
                    : $"Fallback defense routed from review target: {_activeReviewTopic}."
            );
            StatusMessage = $"Oral defense generation fell back to local heuristics: {exception.Message}";
            CompleteJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ScorePracticeTestAsync()
    {
        if (_currentPracticeTest is null || PracticeQuestions.Count == 0)
        {
            return;
        }

        var job = StartJob(
            "Score Practice",
            "Test Builder",
            _settings.TrainingModel,
            "Scoring the active practice set and writing history."
        );
        IsBusy = true;
        StatusMessage = "Scoring current practice test...";

        try
        {
            var correctCount = 0;
            foreach (var question in PracticeQuestions)
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

                var correctOption = question.Options.FirstOrDefault(
                    option =>
                        option.Key.Equals(
                            question.CorrectOptionKey,
                            StringComparison.OrdinalIgnoreCase
                        )
                );
                var unanswered = string.IsNullOrWhiteSpace(question.SelectedOptionKey);
                question.ResultText =
                    $"{(unanswered ? "Unanswered." : "Incorrect.")} Correct answer: {correctOption?.DisplayLabel ?? question.CorrectOptionKey}. {question.Explanation} Connection: {question.SuiteConnection}";
            }

            var attempt = new TrainingAttemptRecord
            {
                Title = _currentPracticeTest.Title,
                Focus = _currentPracticeTest.Focus,
                Difficulty = _currentPracticeTest.Difficulty,
                GenerationSource = _currentPracticeTest.GenerationSource,
                CompletedAt = DateTimeOffset.Now,
                QuestionCount = PracticeQuestions.Count,
                CorrectCount = correctCount,
                Questions = PracticeQuestions
                    .Select(
                        question => new TrainingAttemptQuestionRecord
                        {
                            Topic = question.Topic,
                            Difficulty = question.Difficulty,
                            Correct = string.Equals(
                                question.SelectedOptionKey?.Trim(),
                                question.CorrectOptionKey,
                                StringComparison.OrdinalIgnoreCase
                            ),
                        }
                    )
                    .ToList(),
            };

            var historySummary = await _trainingStore.SaveAttemptAsync(attempt);
            ApplyTrainingHistorySummary(historySummary);
            RefreshTrainingHistoryMetadata();
            MarkPracticeScored(_currentPracticeTest.Focus);
            SetReflectionContext("Practice", _currentPracticeTest.Focus);

            var percent = PracticeQuestions.Count == 0
                ? 0
                : (double)correctCount / PracticeQuestions.Count;
            PracticeResultSummary =
                $"{correctCount}/{PracticeQuestions.Count} correct ({percent:P0}). Weak topics update has been saved locally.";
            StatusMessage = "Practice scored and saved to local history.";
            CompleteJob(job, StatusMessage);
            await AppendDeskOutputAsync(
                EngineeringDeskId,
                "Engineering Desk",
                "practice_score",
                PracticeResultSummary
            );

            await RecordActivityAsync(
                CreateActivity(
                    "practice_scored",
                    "Test Builder",
                    _currentPracticeTest.Focus,
                    PracticeResultSummary
                )
            );
        }
        catch (Exception exception)
        {
            StatusMessage = $"Scoring failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ScoreOralDefenseAsync()
    {
        if (string.IsNullOrWhiteSpace(OralDefenseAnswerText))
        {
            return;
        }

        var job = StartJob(
            "Score Defense",
            "EE Mentor",
            _settings.MentorModel,
            "Scoring the oral defense answer and writing history."
        );
        IsBusy = true;
        StatusMessage = "Scoring oral-defense answer...";

        try
        {
            var evaluation = await _oralDefenseService.ScoreResponseAsync(
                _oralDefenseScenario,
                OralDefenseAnswerText.Trim(),
                _suiteSnapshot,
                _learningProfile,
                _learningLibrary
            );

            var followUps = evaluation.RecommendedFollowUps.Count == 0
                ? _oralDefenseScenario.FollowUpQuestions
                : evaluation.RecommendedFollowUps;
            var topic = string.IsNullOrWhiteSpace(_oralDefenseScenario.Topic)
                ? _activeReviewTopic
                : _oralDefenseScenario.Topic;

            var attempt = new OralDefenseAttemptRecord
            {
                Title = _oralDefenseScenario.Title,
                Topic = string.IsNullOrWhiteSpace(topic)
                    ? "electrical production judgment"
                    : topic.Trim(),
                Prompt = _oralDefenseScenario.Prompt,
                Answer = OralDefenseAnswerText.Trim(),
                GenerationSource = _oralDefenseScenario.GenerationSource,
                CompletedAt = DateTimeOffset.Now,
                TotalScore = evaluation.TotalScore,
                MaxScore = evaluation.MaxScore,
                Summary = evaluation.Summary,
                NextReviewRecommendation = evaluation.NextReviewRecommendation,
                RubricItems = evaluation.RubricItems.ToList(),
                FollowUpQuestions = followUps.ToList(),
            };

            var historySummary = await _trainingStore.SaveDefenseAttemptAsync(attempt);
            ApplyTrainingHistorySummary(historySummary, refreshOralPreview: false);
            RefreshTrainingHistoryMetadata();
            ApplyDefenseEvaluation(evaluation);
            MarkDefenseScored(attempt.Topic);
            SetReflectionContext("Defense", attempt.Topic);

            StatusMessage = "Oral-defense score saved to local history.";
            CompleteJob(job, StatusMessage);
            await AppendDeskOutputAsync(
                EngineeringDeskId,
                "Engineering Desk",
                "defense_score",
                $"{OralDefenseScoreSummary}\n\n{OralDefenseFeedbackSummary}"
            );

            await RecordActivityAsync(
                CreateActivity("defense_scored", "EE Mentor", attempt.Topic, OralDefenseScoreSummary)
            );
        }
        catch (Exception exception)
        {
            StatusMessage = $"Oral-defense scoring failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveSessionReflectionAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionReflectionText)
            || string.IsNullOrWhiteSpace(_lastScoredSessionMode))
        {
            return;
        }

        var job = StartJob(
            "Save Reflection",
            "Chief of Staff",
            _settings.ChiefModel,
            "Saving the reflection and closing the guided session."
        );
        IsBusy = true;
        StatusMessage = "Saving session reflection...";

        try
        {
            var reflection = new SessionReflectionRecord
            {
                Mode = _lastScoredSessionMode,
                Focus = string.IsNullOrWhiteSpace(_lastScoredSessionFocus)
                    ? "current focus"
                    : _lastScoredSessionFocus,
                Reflection = SessionReflectionText.Trim(),
                CompletedAt = DateTimeOffset.Now,
            };

            var historySummary = await _trainingStore.SaveReflectionAsync(reflection);
            ApplyTrainingHistorySummary(historySummary, refreshOralPreview: false);
            RefreshTrainingHistoryMetadata();
            MarkReflectionSaved(reflection.Focus);
            SessionReflectionText = string.Empty;
            ReflectionContextSummary =
                $"Saved reflection for {reflection.Mode.ToLowerInvariant()} on {reflection.Focus}.";
            StatusMessage = "Reflection saved to local training memory.";
            CompleteJob(job, StatusMessage);
            await AppendDeskOutputAsync(
                EngineeringDeskId,
                "Engineering Desk",
                "reflection",
                $"Reflection on {reflection.Focus}: {reflection.Reflection}"
            );

            await RecordActivityAsync(
                CreateActivity(
                    "reflection_saved",
                    "Chief of Staff",
                    reflection.Focus,
                    reflection.Reflection
                )
            );
        }
        catch (Exception exception)
        {
            StatusMessage = $"Saving reflection failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StartRecommendedReviewAsync(ReviewRecommendation? recommendation)
    {
        if (recommendation is null)
        {
            return;
        }

        _activeReviewTopic = recommendation.Topic.Trim();
        PracticeFocusText = _activeReviewTopic;
        SelectedPracticeDifficulty = recommendation.Accuracy switch
        {
            < 0.5 => "Fundamental",
            < 0.75 => "Intermediate",
            _ => "Mixed",
        };
        PracticeQuestionCountText = "6";
        SelectedPrimaryTabIndex = TrainingSessionTabIndex;
        ResetTrainingSessionProgress(
            _activeReviewTopic,
            $"Routed from review queue: {recommendation.Reason}"
        );
        StatusMessage =
            $"Starting targeted review for {_activeReviewTopic}. The next oral drill will bias to the same topic.";

        await GeneratePracticeTestAsync();
        await RecordActivityAsync(
            CreateActivity(
                "review_started",
                "Chief of Staff",
                recommendation.Topic,
                recommendation.DisplaySummary
            )
        );
    }

    private async Task RunLiveResearchAsync()
    {
        var job = StartJob(
            "Live Research",
            string.IsNullOrWhiteSpace(SelectedResearchMode) ? "EE Mentor" : SelectedResearchMode.Trim(),
            ResolveResearchModel(string.IsNullOrWhiteSpace(SelectedResearchMode) ? "EE Mentor" : SelectedResearchMode.Trim()),
            "Running live web research and local synthesis."
        );
        IsBusy = true;
        StatusMessage = "Running live web research...";

        try
        {
            var perspective = string.IsNullOrWhiteSpace(SelectedResearchMode)
                ? "EE Mentor"
                : SelectedResearchMode.Trim();
            var report = await ExecuteResearchCoreAsync(
                ResearchQueryText.Trim(),
                perspective,
                saveToKnowledge: false,
                watchlist: null,
                eventType: "research_run"
            );

            SelectedPrimaryTabIndex = ResearchTabIndex;
            StatusMessage = $"Live research complete using {report.Model}.";
            CompleteJob(job, StatusMessage);
            await AppendDeskOutputAsync(
                ResolveDeskIdFromPerspective(perspective),
                perspective,
                "research",
                $"{report.Query}\n\n{report.Summary}\n\nTakeaways: {string.Join("; ", report.KeyTakeaways.Take(3))}"
            );
        }
        catch (Exception exception)
        {
            StatusMessage = $"Live research failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveResearchToKnowledgeAsync()
    {
        if (_currentResearchReport.Sources.Count == 0)
        {
            return;
        }

        var job = StartJob(
            "Save Research",
            _currentResearchReport.Perspective,
            _currentResearchReport.Model,
            "Writing the research run into the local knowledge library."
        );
        IsBusy = true;
        StatusMessage = "Saving live research to knowledge library...";

        try
        {
            var filePath = await PersistResearchMarkdownAsync(
                _currentResearchReport,
                reloadKnowledge: true
            );

            StatusMessage = $"Saved research note to {filePath}.";
            CompleteJob(job, StatusMessage);
            await RecordActivityAsync(
                CreateActivity(
                    "research_saved",
                    _currentResearchReport.Perspective,
                    _currentResearchReport.Query,
                    filePath
                )
            );
        }
        catch (Exception exception)
        {
            StatusMessage = $"Saving research note failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GenerateWithFallbackAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        Func<string> fallbackFactory,
        Action<string> apply,
        string busyMessage
    )
    {
        IsBusy = true;
        StatusMessage = busyMessage;

        try
        {
            var generated = await _ollamaService.GenerateAsync(model, systemPrompt, userPrompt);
            apply(string.IsNullOrWhiteSpace(generated) ? fallbackFactory() : generated);
            StatusMessage = $"Generated output with {model}.";
        }
        catch (Exception exception)
        {
            apply(fallbackFactory());
            StatusMessage =
                $"{_ollamaService.ProviderLabel} generation fell back to local heuristics: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyTrainingHistorySummary(
        TrainingHistorySummary historySummary,
        bool refreshOralPreview = true
    )
    {
        _trainingHistorySummary = historySummary;
        _learningProfile = _learningProfileService.Build(
            _learningLibrary,
            historySummary,
            _suiteSnapshot
        );
        ApplyLearningState(_learningLibrary, _learningProfile);
        TrainingOverallSummary = historySummary.OverallSummary;
        TrainingNextActionSummary = BuildTrainingNextActionSummary(historySummary, _suiteSnapshot);
        ReviewQueueSummary = historySummary.ReviewQueueSummary;
        DefenseHistorySummary = historySummary.DefenseSummary;
        Replace(WeakTopicSummaries, historySummary.WeakTopics.Select(topic => topic.DisplaySummary));
        Replace(
            RecentPracticeSummaries,
            historySummary.RecentAttempts.Select(attempt => attempt.DisplaySummary)
        );
        Replace(
            RecentDefenseSummaries,
            historySummary.RecentDefenseAttempts.Select(attempt => attempt.DisplaySummary)
        );
        Replace(
            RecentReflectionSummaries,
            historySummary.RecentReflections.Select(reflection => reflection.DisplaySummary)
        );
        Replace(
            ReviewRecommendationSummaries,
            historySummary.ReviewRecommendations.Select(
                recommendation => $"{recommendation.DisplaySummary} | {recommendation.Reason}"
            )
        );
        Replace(ReviewRecommendations, historySummary.ReviewRecommendations);
        Replace(TrainingPriorityMoves, BuildTrainingPriorityMoves(historySummary, _suiteSnapshot));
        RefreshDashboardState();
        RefreshTrainingSessionState();

        if (refreshOralPreview)
        {
            ApplyOralDefenseScenario(
                BuildFallbackOralDefenseScenario(
                    _suiteSnapshot,
                    historySummary,
                    _learningProfile,
                    _learningLibrary,
                    string.IsNullOrWhiteSpace(_activeReviewTopic) ? null : _activeReviewTopic
                )
            );
        }
    }

    private void ApplyLearningState(LearningLibrary learningLibrary, LearningProfile learningProfile)
    {
        _learningLibrary = learningLibrary;
        _learningProfile = learningProfile;
        LearningLibrarySummary = learningLibrary.Summary;
        LearningProfileSummary = learningProfile.Summary;
        Replace(
            KnowledgeSourceSummaries,
            BuildKnowledgeSourceSummaries(learningLibrary)
        );
        Replace(
            ImportedDocumentSummaries,
            learningLibrary.Documents.Select(document => document.DisplaySummary)
        );
        Replace(LearningRuleSummaries, learningProfile.CoachingRules);
        Replace(LearningTopicSummaries, learningProfile.ActiveTopics);
    }

    private void ApplyOralDefenseScenario(OralDefenseScenario scenario)
    {
        _oralDefenseScenario = scenario;
        _activeReviewTopic = scenario.Topic;
        OralDefenseTitle = scenario.Title;
        OralDefensePrompt = scenario.Prompt;
        OralDefenseGoodLooksLike = scenario.WhatGoodLooksLike;
        OralDefenseSuiteConnection = scenario.SuiteConnection;
        OralDefenseSource = scenario.GenerationSource;
        Replace(OralDefenseFollowUpSummaries, scenario.FollowUpQuestions);
        Replace(OralDefenseRubricItems, Array.Empty<DefenseRubricItem>());
        OralDefenseAnswerText = string.Empty;
        OralDefenseScoreSummary = "No scored oral-defense answer yet.";
        OralDefenseFeedbackSummary =
            "Score a typed answer to get rubric feedback and follow-up coaching.";
    }

    private static IReadOnlyList<string> BuildKnowledgeSourceSummaries(LearningLibrary learningLibrary)
    {
        return learningLibrary.SourceRoots
            .Select(root =>
            {
                var matchingDocuments = learningLibrary.Documents
                    .Where(document => document.SourceRootPath.Equals(root, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var label = matchingDocuments.FirstOrDefault()?.SourceRootLabel
                    ?? (root.IndexOf("OneNote Notebooks", StringComparison.OrdinalIgnoreCase) >= 0 ? "OneNote" : Path.GetFileName(root.TrimEnd('\\')));
                var count = matchingDocuments.Count;
                return $"{label} | {root} | {count} supported file{(count == 1 ? string.Empty : "s")}";
            })
            .ToList();
    }

    private void ApplyDefenseEvaluation(DefenseEvaluation evaluation)
    {
        OralDefenseScoreSummary = evaluation.DisplaySummary;
        var weakestItem = evaluation.RubricItems
            .OrderBy(item => item.MaxScore == 0 ? 0 : (double)item.Score / item.MaxScore)
            .ThenBy(item => item.Name)
            .FirstOrDefault();
        OralDefenseFeedbackSummary = weakestItem is null
            ? evaluation.NextReviewRecommendation
            : $"{evaluation.NextReviewRecommendation} Weakest area: {weakestItem.Name}. {weakestItem.Feedback}";
        Replace(OralDefenseRubricItems, evaluation.RubricItems);
        Replace(
            OralDefenseFollowUpSummaries,
            evaluation.RecommendedFollowUps.Count == 0
                ? _oralDefenseScenario.FollowUpQuestions
                : evaluation.RecommendedFollowUps
        );
    }

    private void SetReflectionContext(string mode, string focus)
    {
        _lastScoredSessionMode = mode;
        _lastScoredSessionFocus = string.IsNullOrWhiteSpace(focus) ? "current focus" : focus.Trim();
        SessionReflectionText = string.Empty;
        ReflectionContextSummary =
            $"Reflect on {_lastScoredSessionMode.ToLowerInvariant()} for {_lastScoredSessionFocus}. Capture what felt weak, what to review next, and any tie-in to Suite or career progress.";
    }

    private void ApplyResearchReport(ResearchReport report)
    {
        _currentResearchReport = report;
        ResearchSummary = report.Summary;
        ResearchRunSummary = report.RunSummary;
        Replace(ResearchTakeawaySummaries, report.KeyTakeaways);
        Replace(ResearchActionSummaries, report.ActionMoves);
        Replace(ResearchSources, report.Sources);
        _saveResearchToKnowledgeCommand.RaiseCanExecuteChanged();
    }

    private int ParseQuestionCount()
    {
        return int.TryParse(PracticeQuestionCountText, out var parsed)
            ? Math.Clamp(parsed, 3, 15)
            : 6;
    }

    private string ResolveResearchModel(string perspective) =>
        perspective switch
        {
            "Chief of Staff" => _settings.ChiefModel,
            "Repo Coach" => _settings.RepoModel,
            "Business Strategist" => _settings.BusinessModel,
            _ => _settings.MentorModel,
        };

    private IReadOnlyList<AgentCard> BuildAgents(IReadOnlyList<string> installedModels)
    {
        bool HasModel(string model) =>
            installedModels.Contains(model, StringComparer.OrdinalIgnoreCase);

        return
        [
            new AgentCard
            {
                Name = "Chief of Staff",
                Role = "Routes the day, keeps work focused, and synthesizes the desk.",
                Model = _settings.ChiefModel,
                Mode = "Mode: prepare plans and queue approvals.",
                Status = HasModel(_settings.ChiefModel) ? "ready" : "missing",
                Summary = "Ties EE study, Suite progress, career positioning, and business framing into one operating brief.",
            },
            new AgentCard
            {
                Name = "EE Mentor",
                Role = "Turns active work into electrical-engineering growth and challenge drills.",
                Model = _settings.MentorModel,
                Mode = "Mode: generate practice and explain tradeoffs.",
                Status = HasModel(_settings.MentorModel) ? "ready" : "missing",
                Summary = "Focuses on grounding, power, standards, drafting reasoning, and operator-safe engineering decisions.",
            },
            new AgentCard
            {
                Name = "Test Builder",
                Role = "Builds structured practice tests that can be scored and trended locally.",
                Model = _settings.TrainingModel,
                Mode = "Mode: output strict JSON tests and keep the training loop measurable.",
                Status = HasModel(_settings.TrainingModel) ? "ready" : "missing",
                Summary = "Separates test generation from coaching so the training loop is more reliable and easier to score.",
            },
            new AgentCard
            {
                Name = "Repo Coach",
                Role = "Reads Suite, explains hotspots, and proposes the safest next implementation move.",
                Model = _settings.RepoModel,
                Mode = "Mode: read-only repo scan and plan patches later.",
                Status = HasModel(_settings.RepoModel) ? "ready" : "missing",
                Summary = "Uses dirty files, commit history, docs, and backlog ordering to suggest high-leverage repo work.",
            },
            new AgentCard
            {
                Name = "Business Strategist",
                Role = "Maps Suite features to realistic pilots, offers, and packaging.",
                Model = _settings.BusinessModel,
                Mode = "Mode: packaging only, avoid hype, stay product-first.",
                Status = HasModel(_settings.BusinessModel) ? "ready" : "missing",
                Summary = "Keeps monetization tied to production control, reliability, and measurable operator value.",
            },
        ];
    }

    private static IReadOnlyList<FocusCard> BuildFocusCards(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile
    )
    {
        var reviewTarget = historySummary.ReviewRecommendations.FirstOrDefault();
        var weakestTopic = historySummary.WeakTopics.FirstOrDefault();
        var studySummary = reviewTarget is null
            ? learningProfile.CurrentNeed
            : $"Current review target: {reviewTarget.Topic} is {reviewTarget.Priority}. {reviewTarget.Reason} {learningProfile.CurrentNeed}";

        return
        [
            new FocusCard
            {
                Tag = "STUDY",
                Title = "Close one technical gap",
                Summary = studySummary,
            },
            new FocusCard
            {
                Tag = "SUITE",
                Title = "Work the next clear repo move",
                Summary =
                    snapshot.NextSessionTasks.FirstOrDefault()
                    ?? "Review the current repo hotspot cluster.",
            },
            new FocusCard
            {
                Tag = "BUSINESS",
                Title = "Package one sellable job",
                Summary =
                    snapshot.MonetizationMoves.FirstOrDefault()
                    ?? "Lead with drawing production control, not generic agents.",
            },
        ];
    }

    private static IReadOnlyList<QueueItem> BuildQueueItems(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile
    )
    {
        var reviewTarget = historySummary.ReviewRecommendations.FirstOrDefault();
        var weakestTopic = historySummary.WeakTopics.FirstOrDefault();
        var studyBlockDetail = reviewTarget is null && weakestTopic is null
            ? learningProfile.CurrentNeed
            : $"Retest {(reviewTarget?.Topic ?? weakestTopic!.Topic)}, then explain how that principle should influence {snapshot.HotAreas.FirstOrDefault() ?? "the current Suite hotspot"} in an oral defense.";

        return
        [
            new QueueItem
            {
                Title = "Study Block",
                Detail = studyBlockDetail,
            },
            new QueueItem
            {
                Title = "Repo Review",
                Detail =
                    snapshot.NextSessionTasks.FirstOrDefault()
                    ?? "Inspect the current dirty worktree and choose the safest next unit of progress.",
            },
            new QueueItem
            {
                Title = "Career Proof",
                Detail = "Turn one finished Suite task into a portfolio bullet about operator-first electrical automation.",
            },
            new QueueItem
            {
                Title = "Business Angle",
                Detail =
                    snapshot.MonetizationMoves.FirstOrDefault()
                    ?? "Define one pilot package before trying to sell the whole platform.",
            },
        ];
    }

    private static string BuildTrainingNextActionSummary(
        TrainingHistorySummary historySummary,
        SuiteSnapshot snapshot
    )
    {
        var repoTie = snapshot.HotAreas.FirstOrDefault() ?? "the current Suite hotspot";
        var recentDefense = historySummary.RecentDefenseAttempts.FirstOrDefault();

        if (recentDefense is not null && recentDefense.ScoreRatio < 0.7)
        {
            return
                $"Re-answer {recentDefense.Topic} in defense mode. The latest rubric score was {recentDefense.TotalScore}/{recentDefense.MaxScore}. Tighten the weakest explanation area, then tie the same principle back to {repoTie}.";
        }

        if (historySummary.TotalAttempts == 0 || historySummary.ReviewRecommendations.Count == 0)
        {
            return "Run and score one practice test. After that, Daily Desk will start routing you toward weak topics and the most relevant Suite tie-in.";
        }

        var reviewTarget = historySummary.ReviewRecommendations[0];
        return
            $"Hit {reviewTarget.Topic} next. It is {reviewTarget.Priority} with {reviewTarget.Correct}/{reviewTarget.Attempted} correct so far. After the retest, defend how the same principle should shape {repoTie}.";
    }

    private static IReadOnlyList<string> BuildTrainingPriorityMoves(
        TrainingHistorySummary historySummary,
        SuiteSnapshot snapshot
    )
    {
        var moves = new List<string>();
        var reviewTarget = historySummary.ReviewRecommendations.FirstOrDefault();
        var weakestTopic = historySummary.WeakTopics.FirstOrDefault();

        if (reviewTarget is null && weakestTopic is null)
        {
            moves.Add(
                "Generate a mixed-difficulty test first so the desk has a real baseline for your EE strengths and misses."
            );
        }
        else
        {
            moves.Add(
                $"Retest {(reviewTarget?.Topic ?? weakestTopic!.Topic)} until accuracy clears 80%, then move the topic into oral-defense mode instead of basic recall."
            );
        }

        moves.Add(
            $"Use the review queue to time the next drill, then tie it to {snapshot.HotAreas.FirstOrDefault() ?? "the current Suite hotspot"} so the concept becomes implementation judgment, not isolated trivia."
        );
        if (historySummary.RecentReflections.Count > 0)
        {
            moves.Add(
                $"Use the latest reflection as a hard constraint on the next session: {historySummary.RecentReflections[0].DisplaySummary}"
            );
        }
        moves.Add(
            $"Convert one solved concept into career proof or a monetizable workflow such as {snapshot.MonetizationMoves.FirstOrDefault() ?? "drawing production control for electrical teams"}."
        );

        return moves;
    }

    private static OralDefenseScenario BuildFallbackOralDefenseScenario(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        LearningLibrary learningLibrary,
        string? preferredTopic = null
    )
    {
        var reviewTarget = historySummary.ReviewRecommendations.FirstOrDefault();
        var targetTopic = preferredTopic
            ?? reviewTarget?.Topic
            ?? historySummary.WeakTopics.FirstOrDefault()?.Topic
            ?? learningProfile.ActiveTopics.FirstOrDefault()
            ?? "electrical production judgment";
        var repoTie = snapshot.HotAreas.FirstOrDefault() ?? "the current Suite hotspot";
        var evidenceTie = learningLibrary.Documents.FirstOrDefault()?.RelativePath ?? "your imported notes";

        return new OralDefenseScenario
        {
            Topic = targetTopic,
            Title = $"Oral Defense: {targetTopic} and {repoTie}",
            Prompt =
                $"Defend how {targetTopic} should shape the next move in {repoTie}. Explain the governing principle, the tradeoff, the failure mode to avoid, and the validation step you would require before trusting the result.",
            WhatGoodLooksLike =
                $"A strong answer names a real engineering consequence, explains the tradeoff clearly, gives one likely failure path, and uses {evidenceTie} or prior study as evidence instead of generic language.",
            SuiteConnection =
                $"Connect the explanation back to operator trust, review gates, or production reliability in {repoTie}.",
            GenerationSource = "local preview",
            FollowUpQuestions =
            [
                $"What assumption would most likely break this decision?",
                $"What would you inspect first if the result looked right in the UI but wrong in the field?",
                $"How would you explain this choice to another engineer who is skeptical of automation here?",
                $"What proof would a paying customer need before trusting this as a Suite workflow later?",
            ],
        };
    }

    private void OpenKnowledgeFolder()
    {
        Directory.CreateDirectory(_knowledgeLibraryPath);
        Process.Start(
            new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_knowledgeLibraryPath}\"",
                UseShellExecute = true,
            }
        );
        StatusMessage = $"Opened knowledge folder: {_knowledgeLibraryPath}";
    }

    private async Task ImportKnowledgeFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import study files into Office knowledge",
            Filter =
                "Supported study files|*.md;*.txt;*.docx;*.pdf;*.pptx;*.onepkg|Markdown|*.md|Text|*.txt|Word|*.docx|PDF|*.pdf|PowerPoint|*.pptx|OneNote Package|*.onepkg|All files|*.*",
            Multiselect = true,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
        {
            StatusMessage = "Knowledge import cancelled.";
            return;
        }

        var targetDirectory = Path.Combine(_knowledgeLibraryPath, "Class Notes");
        Directory.CreateDirectory(targetDirectory);

        var importedCount = 0;
        foreach (var sourcePath in dialog.FileNames)
        {
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var targetPath = GetUniqueKnowledgeImportPath(targetDirectory, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, targetPath, overwrite: false);
            importedCount++;
        }

        StatusMessage = importedCount == 0
            ? "No new study files were imported."
            : $"Imported {importedCount} study file(s). Refreshing Office context...";

        if (importedCount > 0)
        {
            await RefreshContextAsync();
        }
    }

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

    private void OpenExternalLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(
            new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            }
        );
        StatusMessage = $"Opened source: {url}";
    }

    private static string BuildSuitePulse(SuiteSnapshot snapshot)
    {
        if (!snapshot.RepoAvailable)
        {
            return "Suite awareness is unavailable at the configured path right now.";
        }

        return BuildQuietSuiteContextSummary(snapshot);
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

    private static string BuildFallbackDailyBrief(
        SuiteSnapshot snapshot,
        LearningProfile learningProfile,
        TrainingHistorySummary historySummary
    ) =>
        $"""
        TODAY
        Block one session for electrical-engineering growth and one session for Suite progress.

        STUDY
        {learningProfile.CurrentNeed} Review memory: {historySummary.ReviewQueueSummary}

        SUITE
        Current pulse: {snapshot.StatusSummary}
        Best next move: {snapshot.NextSessionTasks.FirstOrDefault() ?? "Review the active Suite hotspot cluster before branching into new work."}

        CAREER
        Frame this work as operator-first electrical automation, production reliability, and domain-aware tooling. Reflection memory: {historySummary.ReflectionSummary}

        BUSINESS
        Do not sell the whole platform first. The strongest current angle is: {snapshot.MonetizationMoves.FirstOrDefault() ?? "Drawing production control for electrical AutoCAD teams."}
        """;

    private static string BuildFallbackChallenge(
        SuiteSnapshot snapshot,
        LearningProfile learningProfile,
        TrainingHistorySummary historySummary
    ) =>
        $"""
        TITLE
        Defend the next engineering move in {snapshot.HotAreas.FirstOrDefault() ?? "the active repo hotspot"}

        PROMPT
        Explain which electrical or operator-safety principle should shape the next change, why it matters, and what validation would prove the change is trustworthy. Current learning need: {learningProfile.CurrentNeed} Defense memory: {historySummary.DefenseSummary}

        WHAT GOOD LOOKS LIKE
        A strong answer ties the code or workflow choice back to a real engineering consequence, names one likely failure mode, and proposes a concrete check.
        """;

    private static string BuildFallbackMonetizationBrief(
        SuiteSnapshot snapshot,
        LearningProfile learningProfile,
        TrainingHistorySummary historySummary
    ) =>
        $"""
        CORE OFFER
        Start with one boringly reliable production-control package instead of pitching the whole workspace.

        WHY IT WINS
        It is easier to explain, easier to trust, and easier to prove with customer metrics than a broad agent story. The current learning profile is pushing toward: {learningProfile.CurrentNeed}

        WHAT TO PROVE NEXT
        Turn these items into a single pilot-shaped flow: {(snapshot.MonetizationMoves.Count > 0 ? string.Join(", ", snapshot.MonetizationMoves) : "Drawing production control, standards checks, title block sync, transmittal packaging, and watchdog rollups.")} Latest reflection: {historySummary.ReflectionSummary}
        """;

    private string ResolveStudyGuideFocus(LearningLibrary notebookLibrary)
    {
        if (!string.IsNullOrWhiteSpace(_activeReviewTopic))
        {
            return _activeReviewTopic.Trim();
        }

        var oneNoteDocument = notebookLibrary.Documents
            .OrderByDescending(document => document.LastUpdated)
            .FirstOrDefault();

        if (oneNoteDocument is not null)
        {
            if (oneNoteDocument.Topics.Count > 0)
            {
                return string.Join(", ", oneNoteDocument.Topics.Take(3));
            }

            return Path.GetFileNameWithoutExtension(oneNoteDocument.FileName)
                .Replace('-', ' ')
                .Replace('_', ' ');
        }

        if (!string.IsNullOrWhiteSpace(PracticeFocusText))
        {
            return PracticeFocusText.Trim();
        }

        if (_learningProfile.ActiveTopics.Count > 0)
        {
            return string.Join(", ", _learningProfile.ActiveTopics.Take(3));
        }

        return "your imported electrical notes";
    }

    private string BuildFallbackStudyGuide(string focus, LearningLibrary notebookLibrary)
    {
        var relevantDocuments = notebookLibrary.Documents
            .OrderByDescending(document => document.LastUpdated)
            .Take(2)
            .ToList();

        if (relevantDocuments.Count == 0)
        {
            return BuildMissingNotebookStudyGuideMessage();
        }

        var sourceSummary = relevantDocuments.Count == 0
            ? "No imported notebook sources were available."
            : string.Join(
                "; ",
                relevantDocuments.Select(document => $"[{document.SourceRootLabel}] {document.RelativePath}")
            );

        var topicLines = relevantDocuments
            .SelectMany(document => document.Topics)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var notebookEvidence = KnowledgePromptContextBuilder.BuildRelevantContext(
            notebookLibrary,
            new[]
            {
                focus,
                _learningProfile.CurrentNeed,
                _trainingHistorySummary.ReviewQueueSummary,
                _trainingHistorySummary.DefenseSummary,
            },
            maxDocuments: 2,
            maxTotalCharacters: 1200,
            maxExcerptCharacters: 420
        );

        var coreIdeas = topicLines.Count == 0
            ? "- Start by identifying the governing devices, protection zones, thresholds, and standards language used in the notes."
            : string.Join(Environment.NewLine, topicLines.Select(topic => $"- {topic}"));

        var evidencePreview = notebookEvidence.Equals("none recorded", StringComparison.OrdinalIgnoreCase)
            ? "No readable notebook excerpt was available."
            : Truncate(notebookEvidence.Replace(Environment.NewLine, " "), 420);

        return $"""
        TITLE
        Study Guide: {focus}

        NOTEBOOK SCOPE
        Primary notebook sources: {sourceSummary}

        CORE IDEAS
        {coreIdeas}

        FORMULAS AND RULES
        - Pull the governing equations, relay logic, settings, and standards language from the imported notes before trusting memory.
        - Keep the current learning need in view: {_learningProfile.CurrentNeed}
        - Notebook evidence preview: {evidencePreview}

        FAILURE MODES
        - Confusing device function with operating criteria.
        - Memorizing labels without understanding what failure the protection or design decision is meant to catch.
        - Skipping the validation step that proves the notebook concept would hold up in real review work.

        SELF-CHECK
        1. What is the main engineering problem this notebook material is trying to solve?
        2. Which thresholds, devices, or rules control the decision?
        3. What is the most likely failure mode if the concept is applied incorrectly?
        4. What evidence from the notes would you cite to defend the design choice?
        5. How would you explain this topic to another engineer during review?

        NEXT DRILL
        Use Practice next for {focus}, then run Defense to explain the same topic out loud using the notebook as evidence.
        """;
    }

    private LearningLibrary BuildNotebookOnlyLibrary()
    {
        var notebookDocuments = _learningLibrary.Documents
            .Where(document => document.Kind.Equals("ONEPKG", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(document => document.LastUpdated)
            .ToList();

        var topicHeadlines = notebookDocuments
            .SelectMany(document => document.Topics)
            .GroupBy(topic => topic, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(8)
            .Select(group => group.Key)
            .ToList();

        return new LearningLibrary
        {
            RootPath = _learningLibrary.RootPath,
            Exists = _learningLibrary.Exists,
            SourceRoots = notebookDocuments
                .Select(document => document.SourceRootPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Documents = notebookDocuments,
            TopicHeadlines = topicHeadlines,
        };
    }

    private static string BuildMissingNotebookStudyGuideMessage() =>
        """
        TITLE
        Study Guide unavailable

        NOTEBOOK SCOPE
        No exported OneNote `.onepkg` package is currently loaded into Office.

        NEXT STEP
        Put the `.onepkg` file in your scanned OneNote folder or import it through Library, then click Refresh Office and run Study Guide again.
        """;

    private void SeedPracticeDifficulties()
    {
        Replace(
            PracticeDifficultyOptions,
            ["Mixed", "Fundamental", "Intermediate", "Challenging"]
        );
    }

    private void SeedResearchModes()
    {
        Replace(
            ResearchModeOptions,
            ["EE Mentor", "Chief of Staff", "Repo Coach", "Business Strategist"]
        );
    }

    private void SeedStudyTracks()
    {
        Replace(
            StudyTracks,
            [
                new StudyTrack
                {
                    Title = "Protection, grounding, and safe design constraints",
                    Summary = "Study how real electrical constraints should shape software decisions, review gates, and drafting workflows.",
                    NextMilestone = "Next: explain one design constraint from memory and tie it to a Suite feature.",
                },
                new StudyTrack
                {
                    Title = "Standards, drawings, and operator trust",
                    Summary = "Use drawing QA, title blocks, standards checks, and transmittals as a path to stronger EE production judgment.",
                    NextMilestone = "Next: build one challenge around standards-checking logic and human review.",
                },
                new StudyTrack
                {
                    Title = "Automation with deterministic review gates",
                    Summary = "Learn where automation helps, where it must stop, and how to structure preview, validate, and execute phases.",
                    NextMilestone = "Next: explain why review-first automation is more credible than full autonomy in this domain.",
                },
            ]
        );
    }

    private void SeedGuardrails()
    {
        Replace(
            Guardrails,
            [
                "Daily Desk reads Suite only. It does not edit or execute against the repo by default.",
                "Treat auth, routing, and operator safety as guarded areas unless there is an explicit plan.",
                "Keep monetization grounded in one clear job before chasing broad AI positioning.",
                "Use Suite as a proving ground for your EE growth, not just as a feature backlog.",
            ]
        );
    }

    private void SeedCareerSignals()
    {
        Replace(
            CareerSignals,
            [
                "Suite work already supports a narrative around electrical automation, operator control, and production reliability.",
                "Dashboard, telemetry, and project-surface work strengthens your systems-thinking story.",
                "Standards-checking and review-first flows are stronger career proof than generic AI demos.",
                "Packaging one reliable production-control workflow is a better business story than shipping more novelty.",
            ]
        );
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static string BuildResearchMarkdown(ResearchReport report)
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
}
