using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using DailyDesk.Models;
using DailyDesk.Services;

namespace DailyDesk.ViewModels;

public sealed partial class MainViewModel
{
    private OperatorMemoryStore _operatorMemoryStore = null!;
    private DailyOperatorService _dailyOperatorService = null!;
    private SuiteCoachService _suiteCoachService = null!;

    private RelayCommand _runMorningPassCommand = null!;
    private RelayCommand _generateDailyPlanCommand = null!;
    private RelayCommand _runAutonomyCycleCommand = null!;
    private RelayCommand _runSuiteCoachCommand = null!;
    private RelayCommand _saveAgentPoliciesCommand = null!;
    private RelayCommand _runDueWatchlistsCommand = null!;
    private RelayCommand _addWatchlistFromResearchCommand = null!;
    private RelayCommand _runWatchlistNowCommand = null!;
    private RelayCommand _removeWatchlistCommand = null!;
    private RelayCommand _acceptSuggestedActionCommand = null!;
    private RelayCommand _approveAndQueueSuggestedActionCommand = null!;
    private RelayCommand _approveAndRunSuggestedActionCommand = null!;
    private RelayCommand _deferSuggestedActionCommand = null!;
    private RelayCommand _rejectSuggestedActionCommand = null!;
    private RelayCommand _queueSuggestedActionCommand = null!;
    private RelayCommand _runSuggestedActionNowCommand = null!;

    private OperatorMemoryState _operatorMemoryState = new();

    private string _dailyObjective = "No daily operator plan generated yet.";
    private string _dailyMorningPlan = "Run a chief pass to create a focused morning plan.";
    private string _dailyStudyBlock = "No study block routed yet.";
    private string _dailyRepoBlock = "No repo block routed yet.";
    private string _dailyMiddayCheckpoint = "No midday checkpoint defined yet.";
    private string _dailyEndOfDayReview = "No end-of-day review defined yet.";
    private string _dailyRunSummary = "No daily operator plan generated yet.";
    private string _suiteCoachSummary = "Quiet Suite context has not been refreshed yet.";
    private string _careerEngineProgressSummary =
        "Chief passes 0/8 | Research runs 0/8 | Practice 0/6 | Defense 0/4 | Suggestions resolved 0/10.";
    private string _autonomySummary = "Autonomy policy not loaded yet.";
    private string _approvalInboxSummary = "No approval items yet.";
    private string _suggestionsSummary = "No open suggestions yet.";
    private string _approvedSuggestionsSummary = "No approved next step yet.";
    private string _queuedWorkSummary = "No queued work yet.";
    private string _watchlistSummary = "No research watchlists loaded yet.";
    private string _suggestionMemorySummary = "No suggestion memory yet.";

    public ObservableCollection<AgentPolicy> AgentPolicies { get; } = new();
    public ObservableCollection<string> AutonomyLevelOptions { get; } = new();
    public ObservableCollection<string> WatchlistFrequencyOptions { get; } = new();
    public ObservableCollection<ResearchWatchlist> ResearchWatchlists { get; } = new();
    public ObservableCollection<SuggestedAction> SuggestedActions { get; } = new();
    public ObservableCollection<SuggestedAction> PendingApprovalSuggestions { get; } = new();
    public ObservableCollection<SuggestedAction> OpenSuggestions { get; } = new();
    public ObservableCollection<SuggestedAction> ApprovedSuggestions { get; } = new();
    public ObservableCollection<SuggestedAction> QueuedWorkSuggestions { get; } = new();
    public ObservableCollection<SuggestedAction> SuiteCoachSuggestions { get; } = new();
    public ObservableCollection<string> RecentActivitySummaries { get; } = new();

    public string DailyObjective
    {
        get => _dailyObjective;
        private set => SetProperty(ref _dailyObjective, value);
    }

    public string DailyMorningPlan
    {
        get => _dailyMorningPlan;
        private set => SetProperty(ref _dailyMorningPlan, value);
    }

    public string DailyStudyBlock
    {
        get => _dailyStudyBlock;
        private set => SetProperty(ref _dailyStudyBlock, value);
    }

    public string DailyRepoBlock
    {
        get => _dailyRepoBlock;
        private set => SetProperty(ref _dailyRepoBlock, value);
    }

    public string DailyMiddayCheckpoint
    {
        get => _dailyMiddayCheckpoint;
        private set => SetProperty(ref _dailyMiddayCheckpoint, value);
    }

    public string DailyEndOfDayReview
    {
        get => _dailyEndOfDayReview;
        private set => SetProperty(ref _dailyEndOfDayReview, value);
    }

    public string DailyRunSummary
    {
        get => _dailyRunSummary;
        private set => SetProperty(ref _dailyRunSummary, value);
    }

    public string SuiteCoachSummary
    {
        get => _suiteCoachSummary;
        private set => SetProperty(ref _suiteCoachSummary, value);
    }

    public string CareerEngineProgressSummary
    {
        get => _careerEngineProgressSummary;
        private set => SetProperty(ref _careerEngineProgressSummary, value);
    }

    public string AutonomySummary
    {
        get => _autonomySummary;
        private set => SetProperty(ref _autonomySummary, value);
    }

    public string ApprovalInboxSummary
    {
        get => _approvalInboxSummary;
        private set => SetProperty(ref _approvalInboxSummary, value);
    }

    public string SuggestionsSummary
    {
        get => _suggestionsSummary;
        private set => SetProperty(ref _suggestionsSummary, value);
    }

    public string ApprovedSuggestionsSummary
    {
        get => _approvedSuggestionsSummary;
        private set => SetProperty(ref _approvedSuggestionsSummary, value);
    }

    public string QueuedWorkSummary
    {
        get => _queuedWorkSummary;
        private set => SetProperty(ref _queuedWorkSummary, value);
    }

    public string WatchlistSummary
    {
        get => _watchlistSummary;
        private set => SetProperty(ref _watchlistSummary, value);
    }

    public string SuggestionMemorySummary
    {
        get => _suggestionMemorySummary;
        private set => SetProperty(ref _suggestionMemorySummary, value);
    }

    public ICommand RunMorningPassCommand => _runMorningPassCommand;
    public ICommand GenerateDailyPlanCommand => _generateDailyPlanCommand;
    public ICommand RunAutonomyCycleCommand => _runAutonomyCycleCommand;
    public ICommand RunSuiteCoachCommand => _runSuiteCoachCommand;
    public ICommand SaveAgentPoliciesCommand => _saveAgentPoliciesCommand;
    public ICommand RunDueWatchlistsCommand => _runDueWatchlistsCommand;
    public ICommand AddWatchlistFromResearchCommand => _addWatchlistFromResearchCommand;
    public ICommand RunWatchlistNowCommand => _runWatchlistNowCommand;
    public ICommand RemoveWatchlistCommand => _removeWatchlistCommand;
    public ICommand AcceptSuggestedActionCommand => _acceptSuggestedActionCommand;
    public ICommand ApproveAndQueueSuggestedActionCommand => _approveAndQueueSuggestedActionCommand;
    public ICommand ApproveAndRunSuggestedActionCommand => _approveAndRunSuggestedActionCommand;
    public ICommand DeferSuggestedActionCommand => _deferSuggestedActionCommand;
    public ICommand RejectSuggestedActionCommand => _rejectSuggestedActionCommand;
    public ICommand QueueSuggestedActionCommand => _queueSuggestedActionCommand;
    public ICommand RunSuggestedActionNowCommand => _runSuggestedActionNowCommand;

    private void InitializeOperatorLayer()
    {
        _operatorMemoryStore = new OperatorMemoryStore();
        _dailyOperatorService = new DailyOperatorService(_ollamaService, _settings.ChiefModel);
        _suiteCoachService = new SuiteCoachService(_ollamaService, _settings.RepoModel);

        _runMorningPassCommand = new RelayCommand(async _ => await RunMorningPassAsync(), _ => !IsBusy);
        _generateDailyPlanCommand = new RelayCommand(
            async _ => await GenerateDailyPlanAsync(),
            _ => !IsBusy
        );
        _runAutonomyCycleCommand = new RelayCommand(
            async _ => await RunAutonomyCycleAsync(),
            _ => !IsBusy
        );
        _runSuiteCoachCommand = new RelayCommand(
            async _ => await RunSuiteCoachAsync(),
            _ => !IsBusy
        );
        _saveAgentPoliciesCommand = new RelayCommand(
            async _ => await SaveAgentPoliciesAsync(),
            _ => !IsBusy && AgentPolicies.Count > 0
        );
        _runDueWatchlistsCommand = new RelayCommand(
            async _ => await RunDueWatchlistsAsync(),
            _ => !IsBusy && ResearchWatchlists.Count > 0
        );
        _addWatchlistFromResearchCommand = new RelayCommand(
            async _ => await AddWatchlistFromResearchAsync(),
            _ => !IsBusy && !string.IsNullOrWhiteSpace(ResearchQueryText)
        );
        _runWatchlistNowCommand = new RelayCommand(
            async parameter => await RunWatchlistNowAsync(parameter as ResearchWatchlist),
            parameter => !IsBusy && parameter is ResearchWatchlist
        );
        _removeWatchlistCommand = new RelayCommand(
            async parameter => await RemoveWatchlistAsync(parameter as ResearchWatchlist),
            parameter => !IsBusy && parameter is ResearchWatchlist
        );
        _acceptSuggestedActionCommand = new RelayCommand(
            async parameter => await ResolveSuggestedActionAsync(parameter as SuggestedAction, "accepted"),
            parameter => !IsBusy && parameter is SuggestedAction
        );
        _approveAndQueueSuggestedActionCommand = new RelayCommand(
            async parameter => await QueueSuggestedActionAsync(parameter as SuggestedAction, approveFirst: true),
            parameter => !IsBusy && parameter is SuggestedAction
        );
        _approveAndRunSuggestedActionCommand = new RelayCommand(
            async parameter => await RunSuggestedActionNowAsync(parameter as SuggestedAction, approveFirst: true),
            parameter => !IsBusy && parameter is SuggestedAction
        );
        _deferSuggestedActionCommand = new RelayCommand(
            async parameter => await ResolveSuggestedActionAsync(parameter as SuggestedAction, "deferred"),
            parameter => !IsBusy && parameter is SuggestedAction
        );
        _rejectSuggestedActionCommand = new RelayCommand(
            async parameter => await ResolveSuggestedActionAsync(parameter as SuggestedAction, "rejected"),
            parameter => !IsBusy && parameter is SuggestedAction
        );
        _queueSuggestedActionCommand = new RelayCommand(
            async parameter => await QueueSuggestedActionAsync(parameter as SuggestedAction, approveFirst: false),
            parameter => !IsBusy && parameter is SuggestedAction
        );
        _runSuggestedActionNowCommand = new RelayCommand(
            async parameter => await RunSuggestedActionNowAsync(parameter as SuggestedAction),
            parameter => !IsBusy && parameter is SuggestedAction
        );

        SeedAutonomyLevels();
        SeedWatchlistFrequencies();
    }

    private void RaiseOperatorCommandState()
    {
        _runMorningPassCommand.RaiseCanExecuteChanged();
        _generateDailyPlanCommand.RaiseCanExecuteChanged();
        _runAutonomyCycleCommand.RaiseCanExecuteChanged();
        _runSuiteCoachCommand.RaiseCanExecuteChanged();
        _saveAgentPoliciesCommand.RaiseCanExecuteChanged();
        _runDueWatchlistsCommand.RaiseCanExecuteChanged();
        _addWatchlistFromResearchCommand.RaiseCanExecuteChanged();
        _runWatchlistNowCommand.RaiseCanExecuteChanged();
        _removeWatchlistCommand.RaiseCanExecuteChanged();
        _acceptSuggestedActionCommand.RaiseCanExecuteChanged();
        _approveAndQueueSuggestedActionCommand.RaiseCanExecuteChanged();
        _approveAndRunSuggestedActionCommand.RaiseCanExecuteChanged();
        _deferSuggestedActionCommand.RaiseCanExecuteChanged();
        _rejectSuggestedActionCommand.RaiseCanExecuteChanged();
        _queueSuggestedActionCommand.RaiseCanExecuteChanged();
        _runSuggestedActionNowCommand.RaiseCanExecuteChanged();
    }

    private async Task RunMorningPassAsync()
    {
        StatusMessage = "Generating chief brief...";
        await GenerateChiefBriefAsync();
        StatusMessage = "Chief brief ready. Building daily plan...";
        await GenerateDailyPlanAsync();
        StatusMessage = "Morning pass complete: chief brief and daily plan refreshed.";
    }

    private async Task GenerateDailyPlanAsync()
    {
        var job = StartJob(
            "Daily Plan",
            "Chief of Staff",
            _settings.ChiefModel,
            "Building the operator plan for study, repo work, and review."
        );
        IsBusy = true;
        StatusMessage = "Generating daily operator plan...";

        try
        {
            var dailyRun = await _dailyOperatorService.CreateDailyRunAsync(
                _suiteSnapshot,
                _trainingHistorySummary,
                _learningProfile,
                _learningLibrary,
                _operatorMemoryState
            );

            _operatorMemoryState = await _operatorMemoryStore.SaveDailyRunAsync(dailyRun);
            _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                CreateActivity(
                    "daily_plan_generated",
                    "Chief of Staff",
                    dailyRun.Objective,
                    dailyRun.DisplaySummary
                )
            );

            ApplyOperatorState(_operatorMemoryState);
            SelectedPrimaryTabIndex = OperatorTabIndex;
            StatusMessage = $"Daily plan ready using {dailyRun.GenerationSource}.";
            CompleteJob(job, StatusMessage);
            await AppendDeskOutputAsync(
                ChiefDeskId,
                "Chief of Staff",
                "daily_plan",
                $"{dailyRun.Objective}\n\nMorning: {dailyRun.MorningPlan}\n\nStudy: {dailyRun.StudyBlock}\n\nRepo: {dailyRun.RepoBlock}"
            );
        }
        catch (Exception exception)
        {
            StatusMessage = $"Daily plan generation failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunSuiteCoachAsync()
    {
        var job = StartJob(
            "Suite Context",
            "Suite Context",
            _settings.RepoModel,
            "Refreshing quiet Suite awareness for the office."
        );
        IsBusy = true;
        StatusMessage = "Refreshing quiet Suite context...";

        try
        {
            _suiteSnapshot = await _suiteSnapshotService.LoadAsync(_settings.SuiteRepoPath);
            _learningProfile = _learningProfileService.Build(
                _learningLibrary,
                _trainingHistorySummary,
                _suiteSnapshot
            );
            ApplyLearningState(_learningLibrary, _learningProfile);
            Replace(ChangedFiles, _suiteSnapshot.ChangedFiles);
            Replace(RecentCommits, _suiteSnapshot.RecentCommits);
            Replace(NextSessionTasks, _suiteSnapshot.NextSessionTasks);
            Replace(MonetizationMoves, _suiteSnapshot.MonetizationMoves);
            Replace(ProductPillars, _suiteSnapshot.ProductPillars);
            Replace(HotAreas, _suiteSnapshot.HotAreas);
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

            _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                CreateActivity(
                    "suite_context_refreshed",
                    "Suite Context",
                    "suite awareness",
                    BuildQuietSuiteContextSummary(_suiteSnapshot)
                )
            );

            ApplyOperatorState(_operatorMemoryState);
            SelectedPrimaryTabIndex = OfficeTabIndex;
            SelectedDesk = Agents.FirstOrDefault(item =>
                item.Id.Equals(SuiteDeskId, StringComparison.OrdinalIgnoreCase)
            );
            StatusMessage = "Suite context refreshed.";
            CompleteJob(job, StatusMessage);
            await AppendDeskOutputAsync(
                SuiteDeskId,
                "Suite Context",
                "suite_context",
                $"{BuildQuietSuiteContextSummary(_suiteSnapshot)}\n\n{BuildQuietSuiteTrustSummary(_suiteSnapshot)}"
            );
        }
        catch (Exception exception)
        {
            StatusMessage = $"Suite context refresh failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAgentPoliciesAsync()
    {
        var job = StartJob(
            "Save Policies",
            "Chief of Staff",
            _settings.ChiefModel,
            "Saving autonomy policy updates."
        );
        IsBusy = true;
        StatusMessage = "Saving agent autonomy policies...";

        try
        {
            _operatorMemoryState = await _operatorMemoryStore.SavePoliciesAsync(AgentPolicies.ToList());
            _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                CreateActivity(
                    "policy_saved",
                    "Chief of Staff",
                    "autonomy",
                    $"{AgentPolicies.Count} policies updated."
                )
            );

            ApplyOperatorState(_operatorMemoryState);
            StatusMessage = "Agent policies saved.";
            CompleteJob(job, StatusMessage);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Saving policies failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunDueWatchlistsAsync()
    {
        var dueWatchlists = ResearchWatchlists
            .Where(item => item.IsDue)
            .OrderBy(item => item.NextDueAt)
            .ToList();
        if (dueWatchlists.Count == 0)
        {
            StatusMessage = "No due watchlists right now.";
            return;
        }

        var job = StartJob(
            "Run Watchlists",
            "Chief of Staff",
            ResolveResearchModel("Chief of Staff"),
            $"Running {dueWatchlists.Count} due research watchlists."
        );
        IsBusy = true;
        StatusMessage = $"Running {dueWatchlists.Count} due watchlists...";

        try
        {
            var runNotes = new List<string>();
            foreach (var watchlist in dueWatchlists)
            {
                var report = await ExecuteResearchCoreAsync(
                    watchlist.Query,
                    watchlist.PreferredPerspective,
                    watchlist.SaveToKnowledgeDefault,
                    watchlist,
                    "watchlist_run"
                );
                runNotes.Add($"{watchlist.Topic} ({report.Sources.Count} sources)");
            }

            _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                CreateActivity(
                    "watchlists_run",
                    "Chief of Staff",
                    "recurring research",
                    string.Join("; ", runNotes)
                )
            );

            ApplyOperatorState(_operatorMemoryState);
            SelectedPrimaryTabIndex = ResearchTabIndex;
            StatusMessage = $"Ran {dueWatchlists.Count} due watchlists.";
            CompleteJob(job, StatusMessage);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Running watchlists failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddWatchlistFromResearchAsync()
    {
        var query = string.IsNullOrWhiteSpace(_currentResearchReport.Query)
            ? ResearchQueryText.Trim()
            : _currentResearchReport.Query.Trim();
        var perspective = string.IsNullOrWhiteSpace(_currentResearchReport.Perspective)
            ? SelectedResearchMode.Trim()
            : _currentResearchReport.Perspective.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            StatusMessage = "Enter a research query first.";
            return;
        }

        if (
            ResearchWatchlists.Any(item =>
                item.Query.Equals(query, StringComparison.OrdinalIgnoreCase)
                && item.PreferredPerspective.Equals(perspective, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            StatusMessage = "That watchlist already exists.";
            return;
        }

        var job = StartJob(
            "Add Watchlist",
            string.IsNullOrWhiteSpace(SelectedResearchMode) ? "Chief of Staff" : SelectedResearchMode.Trim(),
            ResolveResearchModel(string.IsNullOrWhiteSpace(SelectedResearchMode) ? "Chief of Staff" : SelectedResearchMode.Trim()),
            "Saving the current research query as a recurring watchlist."
        );
        IsBusy = true;
        StatusMessage = "Adding research watchlist...";

        try
        {
            ResearchWatchlists.Insert(
                0,
                new ResearchWatchlist
                {
                    Topic = query,
                    Query = query,
                    Frequency = "Weekly",
                    PreferredPerspective = perspective,
                    SaveToKnowledgeDefault = true,
                }
            );

            _operatorMemoryState = await _operatorMemoryStore.SaveWatchlistsAsync(
                ResearchWatchlists.ToList()
            );
            _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                CreateActivity("watchlist_added", perspective, query, "Watchlist added from research tab.")
            );

            ApplyOperatorState(_operatorMemoryState);
            SelectedPrimaryTabIndex = ResearchTabIndex;
            StatusMessage = "Watchlist added.";
            CompleteJob(job, StatusMessage);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Adding watchlist failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunWatchlistNowAsync(ResearchWatchlist? watchlist)
    {
        if (watchlist is null)
        {
            return;
        }

        var job = StartJob(
            "Run Watchlist",
            watchlist.PreferredPerspective,
            ResolveResearchModel(watchlist.PreferredPerspective),
            $"Running watchlist: {watchlist.Topic}."
        );
        IsBusy = true;
        StatusMessage = $"Running watchlist: {watchlist.Topic}...";

        try
        {
            var report = await ExecuteResearchCoreAsync(
                watchlist.Query,
                watchlist.PreferredPerspective,
                watchlist.SaveToKnowledgeDefault,
                watchlist,
                "watchlist_run"
            );
            SelectedPrimaryTabIndex = ResearchTabIndex;
            StatusMessage = $"Watchlist run complete using {report.Model}.";
            CompleteJob(job, StatusMessage);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Watchlist run failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RemoveWatchlistAsync(ResearchWatchlist? watchlist)
    {
        if (watchlist is null)
        {
            return;
        }

        var job = StartJob(
            "Remove Watchlist",
            watchlist.PreferredPerspective,
            ResolveResearchModel(watchlist.PreferredPerspective),
            $"Removing watchlist: {watchlist.Topic}."
        );
        IsBusy = true;
        StatusMessage = $"Removing watchlist: {watchlist.Topic}...";

        try
        {
            ResearchWatchlists.Remove(watchlist);
            _operatorMemoryState = await _operatorMemoryStore.SaveWatchlistsAsync(
                ResearchWatchlists.ToList()
            );
            _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                CreateActivity(
                    "watchlist_removed",
                    watchlist.PreferredPerspective,
                    watchlist.Topic,
                    "Watchlist removed."
                )
            );

            ApplyOperatorState(_operatorMemoryState);
            StatusMessage = "Watchlist removed.";
            CompleteJob(job, StatusMessage);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Removing watchlist failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResolveSuggestedActionAsync(SuggestedAction? suggestion, string status)
    {
        if (suggestion is null)
        {
            return;
        }

        var outcomeReason = suggestion.OutcomeReasonInput.Trim();
        if (suggestion.RequiresApproval && string.IsNullOrWhiteSpace(outcomeReason))
        {
            StatusMessage = "Add a short reason before resolving a suggestion.";
            return;
        }

        if (string.IsNullOrWhiteSpace(outcomeReason))
        {
            outcomeReason = status switch
            {
                "deferred" => "Dismissed from self-serve suggestions.",
                "accepted" => "Accepted from self-serve suggestions.",
                "rejected" => "Rejected from self-serve suggestions.",
                _ => "Resolved from self-serve suggestions.",
            };
        }

        var job = StartJob(
            "Resolve Suggestion",
            suggestion.SourceAgent,
            ResolveSuggestionModel(suggestion),
            $"Recording suggestion outcome: {status}."
        );
        IsBusy = true;
        StatusMessage = $"Recording suggestion outcome: {status}...";

        try
        {
            var outcome = new SuggestionOutcome
            {
                Status = status,
                Reason = outcomeReason,
                OutcomeNote = suggestion.OutcomeNoteInput.Trim(),
                RecordedAt = DateTimeOffset.Now,
            };

            _operatorMemoryState = await _operatorMemoryStore.RecordSuggestionOutcomeAsync(
                suggestion.Id,
                outcome
            );
            _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                CreateActivity(
                    $"suggestion_{status}",
                    suggestion.SourceAgent,
                    suggestion.Title,
                    outcome.DisplaySummary
                )
            );

            ApplyOperatorState(_operatorMemoryState);
            StatusMessage = status switch
            {
                "accepted" when suggestion.RequiresApproval =>
                    "Suggestion approved. It moved to Approved next. Queue it or Run now to start follow-through.",
                "accepted" => "Suggestion accepted.",
                _ => $"Suggestion {status}.",
            };
            CompleteJob(job, StatusMessage);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Recording suggestion outcome failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task QueueSuggestedActionAsync(SuggestedAction? suggestion, bool approveFirst)
    {
        if (suggestion is null)
        {
            return;
        }

        if (suggestion.RequiresApproval && suggestion.IsPending && !approveFirst)
        {
            StatusMessage = "Approve this item before you queue it, or use Approve & queue.";
            return;
        }

        if (suggestion.RequiresApproval && suggestion.IsPending && string.IsNullOrWhiteSpace(suggestion.OutcomeReasonInput))
        {
            StatusMessage = "Add a short reason before approving and queueing this suggestion.";
            return;
        }

        var job = StartJob(
            "Queue Suggestion",
            suggestion.SourceAgent,
            ResolveSuggestionModel(suggestion),
            "Adding suggestion to queued work."
        );
        IsBusy = true;
        StatusMessage = "Queueing suggestion...";

        try
        {
            if (suggestion.RequiresApproval && suggestion.IsPending)
            {
                var acceptedOutcome = new SuggestionOutcome
                {
                    Status = "accepted",
                    Reason = suggestion.OutcomeReasonInput.Trim(),
                    OutcomeNote = suggestion.OutcomeNoteInput.Trim(),
                    RecordedAt = DateTimeOffset.Now,
                };

                _operatorMemoryState = await _operatorMemoryStore.RecordSuggestionOutcomeAsync(
                    suggestion.Id,
                    acceptedOutcome
                );
                _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                    CreateActivity(
                        "suggestion_accepted",
                        suggestion.SourceAgent,
                        suggestion.Title,
                        acceptedOutcome.DisplaySummary
                    )
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
                    selfServeOutcome
                );
            }

            _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionExecutionAsync(
                suggestion.Id,
                "queued",
                "Queued for follow-through.",
                CancellationToken.None
            );
            _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                CreateActivity("suggestion_queued", suggestion.SourceAgent, suggestion.Title, "Queued for follow-through.")
            );

            ApplyOperatorState(_operatorMemoryState);
            StatusMessage = "Suggestion queued.";
            CompleteJob(job, StatusMessage);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Queueing suggestion failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunSuggestedActionNowAsync(SuggestedAction? suggestion, bool approveFirst = false)
    {
        if (suggestion is null)
        {
            return;
        }

        if (suggestion.RequiresApproval && suggestion.IsPending && !approveFirst)
        {
            StatusMessage = "Approve this item before running it.";
            return;
        }

        if (suggestion.RequiresApproval && suggestion.IsPending && string.IsNullOrWhiteSpace(suggestion.OutcomeReasonInput))
        {
            StatusMessage = approveFirst
                ? "Add a short reason before approving and running this suggestion."
                : "Approve this item before running it.";
            return;
        }

        if (!suggestion.ActionType.Equals("research_followup", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Run now is currently available only for research follow-ups.";
            return;
        }

        var job = StartJob(
            "Run Suggested Research",
            suggestion.SourceAgent,
            ResolveSuggestionModel(suggestion),
            "Running research directly from an approved suggestion."
        );
        IsBusy = true;
        StatusMessage = "Running suggested research...";

        try
        {
            if (suggestion.RequiresApproval && suggestion.IsPending)
            {
                var acceptedOutcome = new SuggestionOutcome
                {
                    Status = "accepted",
                    Reason = suggestion.OutcomeReasonInput.Trim(),
                    OutcomeNote = suggestion.OutcomeNoteInput.Trim(),
                    RecordedAt = DateTimeOffset.Now,
                };

                _operatorMemoryState = await _operatorMemoryStore.RecordSuggestionOutcomeAsync(
                    suggestion.Id,
                    acceptedOutcome
                );
                _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                    CreateActivity(
                        "suggestion_accepted",
                        suggestion.SourceAgent,
                        suggestion.Title,
                        acceptedOutcome.DisplaySummary
                    )
                );
            }
            else if (!suggestion.RequiresApproval && suggestion.IsPending)
            {
                var selfServeOutcome = new SuggestionOutcome
                {
                    Status = "accepted",
                    Reason = "Started directly from suggestions.",
                    OutcomeNote = string.Empty,
                    RecordedAt = DateTimeOffset.Now,
                };

                _operatorMemoryState = await _operatorMemoryStore.RecordSuggestionOutcomeAsync(
                    suggestion.Id,
                    selfServeOutcome
                );
            }

            _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionExecutionAsync(
                suggestion.Id,
                "running",
                "Running now.",
                CancellationToken.None
            );
            ApplyOperatorState(_operatorMemoryState);

            var perspective = ResolveSuggestionPerspective(suggestion);
            var query = ResolveSuggestionResearchQuery(suggestion);
            ResearchQueryText = query;
            SelectedResearchMode = perspective;

            var report = await ExecuteResearchCoreAsync(
                query,
                perspective,
                saveToKnowledge: false,
                watchlist: null,
                eventType: "research_run"
            );

            _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionResearchResultAsync(
                suggestion.Id,
                report.Summary,
                BuildSuggestionResultDetail(report),
                report.Sources.Take(4).Select(source => source.DisplaySummary).ToList(),
                CancellationToken.None
            );

            _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionExecutionAsync(
                suggestion.Id,
                "completed",
                $"Research completed {report.GeneratedAt:yyyy-MM-dd HH:mm}.",
                CancellationToken.None
            );
            _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                CreateActivity("suggestion_completed", suggestion.SourceAgent, suggestion.Title, report.RunSummary)
            );

            ApplyOperatorState(_operatorMemoryState);
            SelectedPrimaryTabIndex = ResearchTabIndex;
            StatusMessage = $"Suggested research complete using {report.Model}.";
            CompleteJob(job, StatusMessage);
        }
        catch (Exception exception)
        {
            _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionExecutionAsync(
                suggestion.Id,
                "failed",
                $"Run failed: {exception.Message}",
                CancellationToken.None
            );
            _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
                CreateActivity("suggestion_failed", suggestion.SourceAgent, suggestion.Title, exception.Message)
            );
            ApplyOperatorState(_operatorMemoryState);
            StatusMessage = $"Suggested research failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunAutonomyCycleAsync()
    {
        var job = StartJob(
            "Autonomy Cycle",
            "Chief of Staff",
            _settings.ChiefModel,
            "Running the current autonomous prep cycle."
        );
        var executed = new List<string>();

        try
        {
            if (ResearchWatchlists.Any(item => item.IsDue))
            {
                await RunDueWatchlistsAsync();
                executed.Add("watchlists");
            }

            await RunSuiteCoachAsync();
            executed.Add("suite coach");

            await GenerateDailyPlanAsync();
            executed.Add("daily plan");

            var mentorPolicy = ResolveAgentPolicy("EE Mentor");
            var builderPolicy = ResolveAgentPolicy("Test Builder");
            if (
                string.Equals(
                    mentorPolicy?.AutonomyLevel,
                    "Autonomous Prep",
                    StringComparison.OrdinalIgnoreCase
                )
                || string.Equals(
                    builderPolicy?.AutonomyLevel,
                    "Autonomous Prep",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                var reviewTarget = _trainingHistorySummary.ReviewRecommendations.FirstOrDefault();
                if (reviewTarget is not null)
                {
                    _activeReviewTopic = reviewTarget.Topic;
                    PracticeFocusText = reviewTarget.Topic;
                    SelectedPracticeDifficulty = reviewTarget.Accuracy switch
                    {
                        < 0.5 => "Fundamental",
                        < 0.75 => "Intermediate",
                        _ => "Mixed",
                    };
                    PracticeQuestionCountText = "6";
                }

                await GeneratePracticeTestAsync();
                await GenerateOralDefenseAsync();
                executed.Add("training prep");
            }

            await RecordActivityAsync(
                CreateActivity(
                    "autonomy_cycle",
                    "Chief of Staff",
                    "career engine",
                    executed.Count == 0
                        ? "No autonomous actions executed."
                        : $"Executed: {string.Join(", ", executed)}."
                )
            );

            StatusMessage = executed.Count == 0
                ? "Autonomy cycle finished with no actions."
                : $"Autonomy cycle finished: {string.Join(", ", executed)}.";
            CompleteJob(job, StatusMessage);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Autonomy cycle failed: {exception.Message}";
            FailJob(job, StatusMessage);
        }
    }

    private async Task<ResearchReport> ExecuteResearchCoreAsync(
        string query,
        string perspective,
        bool saveToKnowledge,
        ResearchWatchlist? watchlist,
        string eventType
    )
    {
        var model = ResolveResearchModel(perspective);
        var report = await _liveResearchService.RunAsync(
            query,
            perspective,
            model,
            _suiteSnapshot,
            _trainingHistorySummary,
            _learningProfile,
            _learningLibrary
        );

        ApplyResearchReport(report);

        if (saveToKnowledge)
        {
            await PersistResearchMarkdownAsync(report, reloadKnowledge: true);
        }

        if (watchlist is not null)
        {
            watchlist.LastRunAt = DateTimeOffset.Now;
            _operatorMemoryState = await _operatorMemoryStore.SaveWatchlistsAsync(
                ResearchWatchlists.ToList()
            );
        }

        var suggestions = BuildResearchSuggestions(
            report,
            perspective,
            ResolveAgentPolicy(perspective)?.RequiresApproval ?? DefaultRequiresApproval(perspective)
        );
        if (suggestions.Count > 0)
        {
            _operatorMemoryState = await _operatorMemoryStore.UpsertSuggestionsAsync(suggestions);
            _operatorMemoryState = await AutoStageSelfServeSuggestionsAsync(suggestions);
        }

        _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
            CreateActivity(
                eventType,
                perspective,
                watchlist?.Topic ?? report.Query,
                report.RunSummary
            )
        );
        ApplyOperatorState(_operatorMemoryState);

        return report;
    }

    private async Task<OperatorMemoryState> AutoStageSelfServeSuggestionsAsync(
        IReadOnlyList<SuggestedAction> suggestions
    )
    {
        var candidate = suggestions
            .Where(item => !item.RequiresApproval && item.ActionType.Equals("research_followup", StringComparison.OrdinalIgnoreCase))
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
            OutcomeNote = "Queued automatically because this agent is allowed to prepare low-risk research follow-through.",
            RecordedAt = DateTimeOffset.Now,
        };

        _operatorMemoryState = await _operatorMemoryStore.RecordSuggestionOutcomeAsync(
            candidate.Id,
            acceptedOutcome
        );
        _operatorMemoryState = await _operatorMemoryStore.UpdateSuggestionExecutionAsync(
            candidate.Id,
            "queued",
            "Auto-queued from self-serve research.",
            CancellationToken.None
        );
        _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(
            CreateActivity(
                "suggestion_auto_queued",
                candidate.SourceAgent,
                candidate.Title,
                "Auto-queued from self-serve research."
            )
        );

        return _operatorMemoryState;
    }

    private async Task<string> PersistResearchMarkdownAsync(
        ResearchReport report,
        bool reloadKnowledge
    )
    {
        var researchDirectory = Path.Combine(_knowledgeLibraryPath, "Research");
        Directory.CreateDirectory(researchDirectory);

        var slug = CreateSlug(report.Query);
        var fileName = $"{DateTime.Now:yyyyMMdd-HHmmss}-{slug}.md";
        var filePath = Path.Combine(researchDirectory, fileName);
        var markdown = BuildResearchMarkdown(report);
        await File.WriteAllTextAsync(filePath, markdown);

        if (reloadKnowledge)
        {
            _learningLibrary = await _knowledgeImportService.LoadAsync(_knowledgeLibraryPath);
            ApplyTrainingHistorySummary(_trainingHistorySummary, refreshOralPreview: false);
        }

        return filePath;
    }

    private void ApplyOperatorState(OperatorMemoryState state)
    {
        _operatorMemoryState = state;
        var previousSelectionId = SelectedInboxSuggestion?.Id;
        var visibleSuggestions = state.RecentSuggestions
            .Where(IsVisibleOfficeSuggestion)
            .ToList();
        var visiblePendingApprovalSuggestions = state.PendingApprovalSuggestions
            .Where(IsVisibleOfficeSuggestion)
            .OrderBy(GetInboxSortRank)
            .ThenByDescending(item => item.CreatedAt)
            .ToList();
        var visibleOpenSuggestions = state.OpenSuggestions
            .Where(IsVisibleOfficeSuggestion)
            .OrderBy(GetInboxSortRank)
            .ThenByDescending(item => item.CreatedAt)
            .ToList();
        var visibleApprovedSuggestions = state.ApprovedSuggestions
            .Where(IsVisibleOfficeSuggestion)
            .OrderBy(GetInboxSortRank)
            .ThenByDescending(item => item.ExecutionUpdatedAt ?? item.CreatedAt)
            .ToList();
        var visibleQueuedWorkSuggestions = state.QueuedWorkSuggestions
            .Where(IsVisibleOfficeSuggestion)
            .OrderBy(GetInboxSortRank)
            .ThenByDescending(item => item.ExecutionUpdatedAt ?? item.CreatedAt)
            .ToList();

        Replace(AgentPolicies, state.Policies);
        Replace(
            ResearchWatchlists,
            state.Watchlists.OrderBy(item => item.NextDueAt).ThenBy(item => item.Topic)
        );
        Replace(SuggestedActions, visibleSuggestions);
        Replace(InboxSuggestions, visiblePendingApprovalSuggestions);
        Replace(PendingApprovalSuggestions, visiblePendingApprovalSuggestions);
        Replace(OpenSuggestions, visibleOpenSuggestions);
        Replace(ApprovedSuggestions, visibleApprovedSuggestions);
        Replace(QueuedWorkSuggestions, visibleQueuedWorkSuggestions);
        Replace(SuiteCoachSuggestions, Array.Empty<SuggestedAction>());
        Replace(
            RecentActivitySummaries,
            state.RecentActivities.Select(item => item.DisplaySummary)
        );

        ApplyDailyRun(state.LatestDailyRun);
        CareerEngineProgressSummary = BuildCareerEngineProgressSummary(state);
        AutonomySummary = BuildAutonomySummary(state);
        ApprovalInboxSummary = BuildApprovalInboxSummary(state);
        SuggestionsSummary = BuildSuggestionsSummary(state);
        ApprovedSuggestionsSummary = BuildApprovedSuggestionsSummary(state);
        QueuedWorkSummary = BuildQueuedWorkSummary(state);
        WatchlistSummary = BuildWatchlistSummary(state);
        SuggestionMemorySummary = BuildSuggestionMemorySummary(state);
        SuiteCoachSummary = BuildQuietSuiteContextSummary(_suiteSnapshot);
        SelectedInboxSuggestion =
            InboxSuggestions.FirstOrDefault(item => item.Id == previousSelectionId)
            ?? InboxSuggestions.FirstOrDefault(item => item.RequiresApproval && item.IsPending)
            ?? ApprovedSuggestions.FirstOrDefault(item => item.Id == previousSelectionId)
            ?? OpenSuggestions.FirstOrDefault(item => item.Id == previousSelectionId)
            ?? QueuedWorkSuggestions.FirstOrDefault(item => item.Id == previousSelectionId)
            ?? ApprovedSuggestions.FirstOrDefault()
            ?? OpenSuggestions.FirstOrDefault()
            ?? QueuedWorkSuggestions.FirstOrDefault()
            ?? InboxSuggestions.FirstOrDefault();
        RefreshSelectedSuggestionExecutionHistory();
        RaiseWorkflowGlanceProperties();

        RefreshDashboardState();
    }

    private void RefreshSelectedSuggestionExecutionHistory()
    {
        var selectedSuggestion = SelectedInboxSuggestion;
        var history = selectedSuggestion is null
            ? Array.Empty<OperatorActivityRecord>().ToList()
            : _operatorMemoryState.SuggestionExecutionActivities
                .Where(item => item.Topic.Equals(selectedSuggestion.Title, StringComparison.OrdinalIgnoreCase))
                .Take(6)
                .ToList();

        Replace(SuggestionExecutionHistory, history);
        OnPropertyChanged(nameof(HasSuggestionExecutionHistory));
        OnPropertyChanged(nameof(SuggestionExecutionHistorySummary));
    }

    private static string BuildSuggestionResultDetail(ResearchReport report)
    {
        var sections = new List<string>();

        if (report.KeyTakeaways.Count > 0)
        {
            sections.Add(
                "Key takeaways:\n"
                    + string.Join(
                        "\n",
                        report.KeyTakeaways.Take(4).Select(item => $"- {item}")
                    )
            );
        }

        if (report.ActionMoves.Count > 0)
        {
            sections.Add(
                "Suggested next moves:\n"
                    + string.Join(
                        "\n",
                        report.ActionMoves.Take(4).Select(item => $"- {item}")
                    )
            );
        }

        return string.Join("\n\n", sections);
    }

    private void ApplyDailyRun(DailyRunTemplate? dailyRun)
    {
        if (dailyRun is null)
        {
            DailyRunSummary = "No daily operator plan generated yet.";
            DailyObjective = "No daily operator plan generated yet.";
            DailyMorningPlan = "Run a chief pass to create a focused morning plan.";
            DailyStudyBlock = "No study block routed yet.";
            DailyRepoBlock = "No repo block routed yet.";
            DailyMiddayCheckpoint = "No midday checkpoint defined yet.";
            DailyEndOfDayReview = "No end-of-day review defined yet.";
            return;
        }

        DailyRunSummary = dailyRun.DisplaySummary;
        DailyObjective = dailyRun.Objective;
        DailyMorningPlan = dailyRun.MorningPlan;
        DailyStudyBlock = dailyRun.StudyBlock;
        DailyRepoBlock = dailyRun.RepoBlock;
        DailyMiddayCheckpoint = dailyRun.MiddayCheckpoint;
        DailyEndOfDayReview = dailyRun.EndOfDayReview;
    }

    private void RefreshDashboardState()
    {
        RefreshAgentOfficeState();
        Replace(
            FocusCards,
            BuildOperatorFocusCards(
                _suiteSnapshot,
                _trainingHistorySummary,
                _learningProfile,
                _operatorMemoryState
            )
        );
        Replace(
            QueueItems,
            BuildOperatorQueueItems(
                _suiteSnapshot,
                _trainingHistorySummary,
                _learningProfile,
                _operatorMemoryState
            )
        );
    }

    private AgentPolicy? ResolveAgentPolicy(string role) =>
        AgentPolicies.FirstOrDefault(item =>
            item.Role.Equals(role, StringComparison.OrdinalIgnoreCase)
        );

    private async Task RecordActivityAsync(OperatorActivityRecord activity)
    {
        _operatorMemoryState = await _operatorMemoryStore.RecordActivityAsync(activity);
        ApplyOperatorState(_operatorMemoryState);
    }

    private static OperatorActivityRecord CreateActivity(
        string eventType,
        string agent,
        string topic,
        string summary
    ) =>
        new()
        {
            EventType = eventType,
            Agent = agent,
            Topic = topic,
            Summary = Truncate(summary, 220),
            OccurredAt = DateTimeOffset.Now,
        };

    private static bool DefaultRequiresApproval(string perspective) =>
        perspective is "Repo Coach" or "Business Strategist";

    private string ResolveSuggestionModel(SuggestedAction suggestion) =>
        suggestion.SourceAgent switch
        {
            "Chief of Staff" => _settings.ChiefModel,
            "Repo Coach" => _settings.RepoModel,
            "Business Strategist" => _settings.BusinessModel,
            "Test Builder" => _settings.TrainingModel,
            _ => _settings.MentorModel,
        };

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
                (action, index) => new SuggestedAction
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

    private IReadOnlyList<AgentCard> BuildOperatorAgents(
        IReadOnlyList<string> installedModels,
        IReadOnlyList<AgentPolicy> policies
    )
    {
        bool HasModel(string model) =>
            installedModels.Contains(model, StringComparer.OrdinalIgnoreCase);

        string PolicyMode(string role, string fallbackMode)
        {
            var policy = policies.FirstOrDefault(item =>
                item.Role.Equals(role, StringComparison.OrdinalIgnoreCase)
            );
            return policy is null
                ? fallbackMode
                : $"Policy: {policy.AutonomyLevel} | {(policy.RequiresApproval ? "approval gate" : "self-serve")} | {policy.ReviewCadence}";
        }

        return
        [
            new AgentCard
            {
                Name = "Chief of Staff",
                Role = "Routes the day, keeps work focused, and synthesizes the desk.",
                Model = _settings.ChiefModel,
                Mode = PolicyMode("Chief of Staff", "Mode: prepare plans and queue approvals."),
                Status = HasModel(_settings.ChiefModel) ? "ready" : "missing",
                Summary = "Ties EE study, Suite progress, career positioning, and business framing into one operating brief.",
            },
            new AgentCard
            {
                Name = "EE Mentor",
                Role = "Turns active work into electrical-engineering growth and challenge drills.",
                Model = _settings.MentorModel,
                Mode = PolicyMode("EE Mentor", "Mode: generate practice and explain tradeoffs."),
                Status = HasModel(_settings.MentorModel) ? "ready" : "missing",
                Summary = "Focuses on grounding, power, standards, drafting reasoning, and operator-safe engineering decisions.",
            },
            new AgentCard
            {
                Name = "Test Builder",
                Role = "Builds structured practice tests that can be scored and trended locally.",
                Model = _settings.TrainingModel,
                Mode = PolicyMode("Test Builder", "Mode: output strict JSON tests and keep the training loop measurable."),
                Status = HasModel(_settings.TrainingModel) ? "ready" : "missing",
                Summary = "Separates test generation from coaching so the training loop is more reliable and easier to score.",
            },
            new AgentCard
            {
                Name = "Repo Coach",
                Role = "Reads Suite, explains hotspots, and proposes the safest next implementation move.",
                Model = _settings.RepoModel,
                Mode = PolicyMode("Repo Coach", "Mode: read-only repo scan and plan patches later."),
                Status = HasModel(_settings.RepoModel) ? "ready" : "missing",
                Summary = "Uses dirty files, commit history, docs, and backlog ordering to suggest high-leverage repo work.",
            },
            new AgentCard
            {
                Name = "Business Strategist",
                Role = "Maps Suite features to realistic pilots, offers, and packaging.",
                Model = _settings.BusinessModel,
                Mode = PolicyMode("Business Strategist", "Mode: packaging only, avoid hype, stay product-first."),
                Status = HasModel(_settings.BusinessModel) ? "ready" : "missing",
                Summary = "Keeps monetization tied to production control, reliability, and measurable operator value.",
            },
        ];
    }

    private static IReadOnlyList<FocusCard> BuildOperatorFocusCards(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        OperatorMemoryState operatorState
    )
    {
        var dailyRun = operatorState.LatestDailyRun;
        var reviewTarget = historySummary.ReviewRecommendations.FirstOrDefault();
        var suiteSuggestion = operatorState.RecentSuggestions.FirstOrDefault(item =>
            item.SourceAgent.Equals("Repo Coach", StringComparison.OrdinalIgnoreCase)
        );
        var businessSuggestion = operatorState.RecentSuggestions.FirstOrDefault(item =>
            item.SourceAgent.Equals("Business Strategist", StringComparison.OrdinalIgnoreCase)
        );

        return
        [
            new FocusCard
            {
                Tag = "STUDY",
                Title = "Close one technical gap",
                Summary = dailyRun?.StudyBlock
                    ?? (reviewTarget is null
                        ? learningProfile.CurrentNeed
                        : $"Current review target: {reviewTarget.Topic} is {reviewTarget.Priority}. {reviewTarget.Reason}"),
            },
            new FocusCard
            {
                Tag = "SUITE",
                Title = "Work the next clear Suite move",
                Summary = suiteSuggestion?.Title
                    ?? snapshot.NextSessionTasks.FirstOrDefault()
                    ?? "Review the current repo hotspot and split the next move into a proposal-sized unit.",
            },
            new FocusCard
            {
                Tag = "CAREER",
                Title = "Turn work into career proof",
                Summary = businessSuggestion?.CareerValue
                    ?? dailyRun?.Objective
                    ?? "Capture one portfolio bullet that proves operator-first electrical automation judgment.",
            },
        ];
    }

    private static IReadOnlyList<QueueItem> BuildOperatorQueueItems(
        SuiteSnapshot snapshot,
        TrainingHistorySummary historySummary,
        LearningProfile learningProfile,
        OperatorMemoryState operatorState
    )
    {
        var dailyRun = operatorState.LatestDailyRun;
        if (dailyRun is not null && dailyRun.CarryoverQueue.Count > 0)
        {
            return dailyRun.CarryoverQueue
                .Select(
                    (item, index) => new QueueItem
                    {
                        Title = $"Carryover {index + 1}",
                        Detail = item,
                    }
                )
                .ToList();
        }

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

    private static string BuildAutonomySummary(OperatorMemoryState state)
    {
        var autonomousPrep = state.Policies.Count(item =>
            item.AutonomyLevel.Equals("Autonomous Prep", StringComparison.OrdinalIgnoreCase)
        );
        var prepare = state.Policies.Count(item =>
            item.AutonomyLevel.Equals("Prepare", StringComparison.OrdinalIgnoreCase)
        );
        var gated = state.Policies.Count(item => item.RequiresApproval);
        return $"{state.Policies.Count} roles configured | {autonomousPrep} autonomous-prep | {prepare} prepare | {gated} approval-gated. Suite remains read-only from DailyDesk.";
    }

    private static string BuildApprovalInboxSummary(OperatorMemoryState state)
    {
        var pending = state.PendingApprovalSuggestions.Count(IsVisibleOfficeSuggestion);
        var resolved = state.Suggestions.Count(item =>
            item.RequiresApproval && !item.IsPending && IsVisibleOfficeSuggestion(item)
        );

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
        var open = state.OpenSuggestions.Count(IsVisibleOfficeSuggestion);
        var approved = state.ApprovedSuggestions.Count(IsVisibleOfficeSuggestion);

        return $"{open} open suggestion{(open == 1 ? string.Empty : "s")} | {approved} approved next step{(approved == 1 ? string.Empty : "s")}.";
    }

    private static string BuildApprovedSuggestionsSummary(OperatorMemoryState state)
    {
        var approved = state.ApprovedSuggestions.Count(IsVisibleOfficeSuggestion);
        return approved switch
        {
            0 => "No approved next steps. Approve only records the decision; Queue or Run now starts the work.",
            1 => "1 approved next step. Approve only records the decision; Queue or Run now starts the work.",
            _ => $"{approved} approved next steps. Approve only records the decision; Queue or Run now starts the work.",
        };
    }

    private static string BuildQueuedWorkSummary(OperatorMemoryState state)
    {
        var queued = state.QueuedWorkSuggestions.Count(item =>
            IsVisibleOfficeSuggestion(item) && (item.IsQueued || item.IsRunning)
        );
        var retry = state.QueuedWorkSuggestions.Count(item =>
            IsVisibleOfficeSuggestion(item) && item.IsFailed
        );

        return $"{queued} active queued item{(queued == 1 ? string.Empty : "s")} | {retry} need retry.";
    }

    private static string BuildWatchlistSummary(OperatorMemoryState state)
    {
        if (state.Watchlists.Count == 0)
        {
            return "No watchlists configured yet.";
        }

        var due = state.DueWatchlists.Count;
        var next = state.Watchlists
            .Where(item => item.IsEnabled)
            .OrderBy(item => item.NextDueAt)
            .FirstOrDefault();

        return next is null
            ? $"{state.Watchlists.Count} watchlists configured."
            : $"{due} due now | next: {next.Topic} ({next.DueSummary}).";
    }

    private static string BuildSuggestionMemorySummary(OperatorMemoryState state)
    {
        var visible = state.Suggestions.Where(IsVisibleOfficeSuggestion).ToList();
        var resolved = visible.Count(item => !item.IsPending);
        var pending = visible.Count(item => item.IsPending);
        var latest = visible.FirstOrDefault(item => !item.IsPending);
        return latest is null
            ? $"{pending} pending suggestions | {resolved} resolved suggestions."
            : $"{pending} pending suggestions | {resolved} resolved suggestions | latest: {latest.Title} => {latest.StatusSummary}.";
    }

    private static string BuildSuiteCoachSummary(
        OperatorMemoryState state,
        SuiteSnapshot snapshot
    )
    {
        return BuildQuietSuiteContextSummary(snapshot);
    }

    private static bool IsVisibleOfficeSuggestion(SuggestedAction suggestion) =>
        !suggestion.SourceAgent.Equals("Repo Coach", StringComparison.OrdinalIgnoreCase);

    private string ResolveSuggestionPerspective(SuggestedAction suggestion)
    {
        var perspective = string.IsNullOrWhiteSpace(suggestion.SourceAgent)
            ? "Chief of Staff"
            : suggestion.SourceAgent.Trim();

        return ResearchModeOptions.Contains(perspective, StringComparer.OrdinalIgnoreCase)
            ? perspective
            : "Chief of Staff";
    }

    private static string ResolveSuggestionResearchQuery(SuggestedAction suggestion)
    {
        if (!string.IsNullOrWhiteSpace(suggestion.LinkedArea))
        {
            return suggestion.LinkedArea.Trim();
        }

        if (!string.IsNullOrWhiteSpace(suggestion.ExpectedBenefit))
        {
            return suggestion.ExpectedBenefit.Trim();
        }

        return suggestion.Title.Trim();
    }

    private void SeedAutonomyLevels()
    {
        Replace(AutonomyLevelOptions, ["Suggest", "Prepare", "Autonomous Prep"]);
    }

    private void SeedWatchlistFrequencies()
    {
        Replace(WatchlistFrequencyOptions, ["Daily", "Twice Weekly", "Weekly"]);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..maxLength].Trim()}...";
    }
}
