using System.Collections.ObjectModel;
using System.Windows.Input;
using DailyDesk.Models;

namespace DailyDesk.ViewModels;

public sealed partial class MainViewModel
{
    private const int OfficeTabIndex = 0;
    private const int OperatorTabIndex = OfficeTabIndex;
    private const int TrainingSessionTabIndex = OfficeTabIndex;
    private const int ResearchTabIndex = OfficeTabIndex;
    private const int RepoTabIndex = OfficeTabIndex;
    private const int InboxTabIndex = 1;
    private const int LibraryTabIndex = 2;
    private const int GuideTabIndex = 3;

    private RelayCommand _toggleCrewPaneCommand = null!;
    private RelayCommand _toggleSignalsPaneCommand = null!;
    private RelayCommand _openSuggestionInInboxCommand = null!;

    private ShellLayoutMode _shellLayoutMode = ShellLayoutMode.Wide;
    private bool _isCrewDrawerOpen;
    private bool _isSignalsDrawerOpen;
    private int _selectedPrimaryTabIndex = OperatorTabIndex;
    private string? _selectedInboxSuggestionId;
    private SuggestedAction? _selectedInboxSuggestion;
    private TrainingSessionState _trainingSession = new();
    private string _historyFilePath = string.Empty;
    private bool _historyExists;
    private string _historyLastWriteSummary =
        "No training history yet. Score a practice test, score a defense answer, and save a reflection to create it.";
    private bool _practiceGeneratedStepComplete;
    private bool _practiceScoredStepComplete;
    private bool _defenseGeneratedStepComplete;
    private bool _defenseScoredStepComplete;
    private bool _reflectionSavedStepComplete;
    private string _trainingFocusReason =
        "Set a focus manually or start from a review target to begin a guided session.";
    private string _activeJobSummary = "No active jobs.";
    private string _activeJobMeta = string.Empty;
    private bool _hasActiveJob;
    private bool _showBlockingOverlay;

    public ObservableCollection<SuggestedAction> InboxSuggestions { get; } = new();
    public ObservableCollection<OperatorActivityRecord> SuggestionExecutionHistory { get; } = new();

    public ObservableCollection<JobActivityItem> JobActivities { get; } = new();

    public ShellLayoutMode ShellLayoutMode
    {
        get => _shellLayoutMode;
        private set
        {
            if (!SetProperty(ref _shellLayoutMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowCrewPaneColumn));
            OnPropertyChanged(nameof(ShowSignalsPaneColumn));
            OnPropertyChanged(nameof(CanToggleCrewPane));
            OnPropertyChanged(nameof(CanToggleSignalsPane));
            OnPropertyChanged(nameof(ShowCrewDrawer));
            OnPropertyChanged(nameof(ShowSignalsDrawer));
        }
    }

    public bool ShowCrewPaneColumn => ShellLayoutMode == ShellLayoutMode.Wide;

    public bool ShowSignalsPaneColumn => ShellLayoutMode is not ShellLayoutMode.Focused;

    public bool CanToggleCrewPane => !ShowCrewPaneColumn;

    public bool CanToggleSignalsPane => !ShowSignalsPaneColumn;

    public bool ShowCrewDrawer => CanToggleCrewPane && _isCrewDrawerOpen;

    public bool ShowSignalsDrawer => CanToggleSignalsPane && _isSignalsDrawerOpen;

    public int SelectedPrimaryTabIndex
    {
        get => _selectedPrimaryTabIndex;
        set => SetProperty(ref _selectedPrimaryTabIndex, value);
    }

    public SuggestedAction? SelectedInboxSuggestion
    {
        get => _selectedInboxSuggestion;
        set
        {
            if (!SetProperty(ref _selectedInboxSuggestion, value))
            {
                return;
            }

            var nextId = value?.Id;
            if (_selectedInboxSuggestionId != nextId)
            {
                _selectedInboxSuggestionId = nextId;
                OnPropertyChanged(nameof(SelectedInboxSuggestionId));
            }

            OnPropertyChanged(nameof(HasSelectedInboxSuggestion));
            OnPropertyChanged(nameof(ShowApprovalDecisionActions));
            OnPropertyChanged(nameof(ShowApproveAndQueueAction));
            OnPropertyChanged(nameof(ShowApproveAndRunAction));
            OnPropertyChanged(nameof(ShowSuggestionQueueActions));
            OnPropertyChanged(nameof(ShowApprovedFollowThroughActions));
            OnPropertyChanged(nameof(ShowQueuedWorkActions));
            OnPropertyChanged(nameof(SelectedInboxLaneSummary));
            RefreshSelectedSuggestionExecutionHistory();
        }
    }

    public string? SelectedInboxSuggestionId
    {
        get => _selectedInboxSuggestionId;
        set
        {
            if (!SetProperty(ref _selectedInboxSuggestionId, value))
            {
                return;
            }

            var nextSuggestion = FindInboxSuggestionById(value);
            if (!ReferenceEquals(_selectedInboxSuggestion, nextSuggestion))
            {
                _selectedInboxSuggestion = nextSuggestion;
                OnPropertyChanged(nameof(SelectedInboxSuggestion));
                OnPropertyChanged(nameof(HasSelectedInboxSuggestion));
                OnPropertyChanged(nameof(ShowApprovalDecisionActions));
                OnPropertyChanged(nameof(ShowApproveAndQueueAction));
                OnPropertyChanged(nameof(ShowApproveAndRunAction));
                OnPropertyChanged(nameof(ShowSuggestionQueueActions));
                OnPropertyChanged(nameof(ShowApprovedFollowThroughActions));
                OnPropertyChanged(nameof(ShowQueuedWorkActions));
                OnPropertyChanged(nameof(SelectedInboxLaneSummary));
            }
        }
    }

    public bool HasSelectedInboxSuggestion => SelectedInboxSuggestion is not null;

    public bool ShowApprovalDecisionActions =>
        SelectedInboxSuggestion?.RequiresApproval == true && SelectedInboxSuggestion.IsPending;

    public bool ShowApproveAndQueueAction => ShowApprovalDecisionActions;

    public bool ShowApproveAndRunAction =>
        ShowApprovalDecisionActions
        && SelectedInboxSuggestion?.ActionType.Equals("research_followup", StringComparison.OrdinalIgnoreCase) == true;

    public bool ShowSuggestionQueueActions =>
        SelectedInboxSuggestion is not null
        && !SelectedInboxSuggestion.RequiresApproval
        && SelectedInboxSuggestion.IsPending
        && !SelectedInboxSuggestion.HasExecution;

    public bool ShowApprovedFollowThroughActions =>
        SelectedInboxSuggestion?.NeedsFollowThrough == true;

    public bool ShowQueuedWorkActions =>
        SelectedInboxSuggestion is not null
        && (SelectedInboxSuggestion.IsQueued || SelectedInboxSuggestion.IsFailed);

    public bool HasSuggestionExecutionHistory => SuggestionExecutionHistory.Count > 0;

    public string SelectedInboxLaneSummary =>
        SelectedInboxSuggestion switch
        {
            null => "Select an inbox item to review its context and decide the next step.",
            { RequiresApproval: true, IsPending: true } =>
                "Approval required before this suggestion can move into queued work.",
            { NeedsFollowThrough: true } =>
                "Approved only. Queue it or Run now to actually start the follow-through.",
            { IsQueued: true } => "Queued work. Run it when you want to execute the next step.",
            { IsRunning: true } => "This suggestion is running now.",
            { IsFailed: true } => "The last run failed. Review the note and retry when ready.",
            { IsCompleted: true } => "This suggestion already produced a completed work run.",
            _ => "Self-serve suggestion. Queue it, run it now, or dismiss it."
        };

    public string SuggestionExecutionHistorySummary =>
        SelectedInboxSuggestion switch
        {
            null => "Select an inbox item to review its queue and run history.",
            _ when HasSuggestionExecutionHistory =>
                $"{SuggestionExecutionHistory.Count} recent execution event{(SuggestionExecutionHistory.Count == 1 ? string.Empty : "s")} for this item.",
            _ => "No queue or run events have been recorded for this item yet.",
        };

    public TrainingSessionState TrainingSession
    {
        get => _trainingSession;
        private set => SetProperty(ref _trainingSession, value);
    }

    public string HistoryFilePath
    {
        get => _historyFilePath;
        private set => SetProperty(ref _historyFilePath, value);
    }

    public bool HistoryExists
    {
        get => _historyExists;
        private set => SetProperty(ref _historyExists, value);
    }

    public string HistoryLastWriteSummary
    {
        get => _historyLastWriteSummary;
        private set => SetProperty(ref _historyLastWriteSummary, value);
    }

    public bool PracticeGeneratedStepComplete
    {
        get => _practiceGeneratedStepComplete;
        private set => SetProperty(ref _practiceGeneratedStepComplete, value);
    }

    public bool PracticeScoredStepComplete
    {
        get => _practiceScoredStepComplete;
        private set => SetProperty(ref _practiceScoredStepComplete, value);
    }

    public bool DefenseGeneratedStepComplete
    {
        get => _defenseGeneratedStepComplete;
        private set => SetProperty(ref _defenseGeneratedStepComplete, value);
    }

    public bool DefenseScoredStepComplete
    {
        get => _defenseScoredStepComplete;
        private set => SetProperty(ref _defenseScoredStepComplete, value);
    }

    public bool ReflectionSavedStepComplete
    {
        get => _reflectionSavedStepComplete;
        private set => SetProperty(ref _reflectionSavedStepComplete, value);
    }

    public string ActiveJobSummary
    {
        get => _activeJobSummary;
        private set => SetProperty(ref _activeJobSummary, value);
    }

    public string ActiveJobMeta
    {
        get => _activeJobMeta;
        private set => SetProperty(ref _activeJobMeta, value);
    }

    public bool HasActiveJob
    {
        get => _hasActiveJob;
        private set => SetProperty(ref _hasActiveJob, value);
    }

    public bool HasJobActivities => JobActivities.Count > 0;

    public bool ShowBlockingOverlay
    {
        get => _showBlockingOverlay;
        private set => SetProperty(ref _showBlockingOverlay, value);
    }

    public int PendingApprovalCount => PendingApprovalSuggestions.Count;

    public string PendingApprovalBadge =>
        PendingApprovalCount == 0
            ? "Inbox clear"
            : $"{PendingApprovalCount} pending approval{(PendingApprovalCount == 1 ? string.Empty : "s")}";

    public ICommand ToggleCrewPaneCommand => _toggleCrewPaneCommand;

    public ICommand ToggleSignalsPaneCommand => _toggleSignalsPaneCommand;

    public ICommand OpenSuggestionInInboxCommand => _openSuggestionInInboxCommand;

    private void InitializeWorkflowLayer()
    {
        _toggleCrewPaneCommand = new RelayCommand(_ => ToggleCrewPane(), _ => CanToggleCrewPane);
        _toggleSignalsPaneCommand = new RelayCommand(
            _ => ToggleSignalsPane(),
            _ => CanToggleSignalsPane
        );
        _openSuggestionInInboxCommand = new RelayCommand(
            parameter => OpenSuggestionInInbox(parameter as SuggestedAction),
            parameter => parameter is SuggestedAction
        );

        RefreshTrainingHistoryMetadata();
        RefreshTrainingSessionState();
    }

    private void RaiseWorkflowCommandState()
    {
        _toggleCrewPaneCommand.RaiseCanExecuteChanged();
        _toggleSignalsPaneCommand.RaiseCanExecuteChanged();
        _openSuggestionInInboxCommand.RaiseCanExecuteChanged();
    }

    public void UpdateShellLayout(double windowWidth)
    {
        var nextMode = windowWidth switch
        {
            >= 1650 => ShellLayoutMode.Wide,
            >= 1320 => ShellLayoutMode.Medium,
            _ => ShellLayoutMode.Focused,
        };

        ShellLayoutMode = nextMode;

        if (ShowCrewPaneColumn)
        {
            _isCrewDrawerOpen = false;
        }

        if (ShowSignalsPaneColumn)
        {
            _isSignalsDrawerOpen = false;
        }

        OnPropertyChanged(nameof(ShowCrewDrawer));
        OnPropertyChanged(nameof(ShowSignalsDrawer));
        RaiseWorkflowCommandState();
    }

    private void ToggleCrewPane()
    {
        if (!CanToggleCrewPane)
        {
            return;
        }

        _isCrewDrawerOpen = !_isCrewDrawerOpen;
        if (_isCrewDrawerOpen)
        {
            _isSignalsDrawerOpen = false;
        }

        OnPropertyChanged(nameof(ShowCrewDrawer));
        OnPropertyChanged(nameof(ShowSignalsDrawer));
    }

    private void ToggleSignalsPane()
    {
        if (!CanToggleSignalsPane)
        {
            return;
        }

        _isSignalsDrawerOpen = !_isSignalsDrawerOpen;
        if (_isSignalsDrawerOpen)
        {
            _isCrewDrawerOpen = false;
        }

        OnPropertyChanged(nameof(ShowCrewDrawer));
        OnPropertyChanged(nameof(ShowSignalsDrawer));
    }

    private void OpenSuggestionInInbox(SuggestedAction? suggestion)
    {
        if (suggestion is null)
        {
            return;
        }

        SelectedPrimaryTabIndex = InboxTabIndex;
        SelectedInboxSuggestion = suggestion;
    }

    private SuggestedAction? FindInboxSuggestionById(string? suggestionId)
    {
        if (string.IsNullOrWhiteSpace(suggestionId))
        {
            return null;
        }

        return PendingApprovalSuggestions.FirstOrDefault(item => item.Id == suggestionId)
            ?? OpenSuggestions.FirstOrDefault(item => item.Id == suggestionId)
            ?? ApprovedSuggestions.FirstOrDefault(item => item.Id == suggestionId)
            ?? QueuedWorkSuggestions.FirstOrDefault(item => item.Id == suggestionId)
            ?? SuggestedActions.FirstOrDefault(item => item.Id == suggestionId);
    }

    private void RefreshTrainingHistoryMetadata()
    {
        HistoryFilePath = _trainingStore.StorePath;
        HistoryExists = _trainingStore.Exists;
        var lastWriteAt = _trainingStore.GetLastWriteTime();
        HistoryLastWriteSummary = lastWriteAt is null
            ? "No training history yet. Score a practice test, score a defense answer, and save a reflection to create it."
            : $"Last history write: {lastWriteAt:yyyy-MM-dd HH:mm}";
    }

    private void ResetTrainingSessionProgress(string focus, string reason)
    {
        PracticeGeneratedStepComplete = false;
        PracticeScoredStepComplete = false;
        DefenseGeneratedStepComplete = false;
        DefenseScoredStepComplete = false;
        ReflectionSavedStepComplete = false;
        _trainingFocusReason = string.IsNullOrWhiteSpace(reason)
            ? "Manual focus selected for the next guided session."
            : reason.Trim();

        RefreshTrainingSessionState(focus);
    }

    private void MarkPracticeGenerated(string focus, string reason)
    {
        PracticeGeneratedStepComplete = true;
        PracticeScoredStepComplete = false;
        DefenseGeneratedStepComplete = false;
        DefenseScoredStepComplete = false;
        ReflectionSavedStepComplete = false;
        _trainingFocusReason = string.IsNullOrWhiteSpace(reason)
            ? "Practice focus chosen manually."
            : reason.Trim();
        RefreshTrainingSessionState(focus);
    }

    private void MarkPracticeScored(string focus)
    {
        PracticeGeneratedStepComplete = true;
        PracticeScoredStepComplete = true;
        RefreshTrainingSessionState(focus);
    }

    private void MarkDefenseGenerated(string focus, string reason)
    {
        DefenseGeneratedStepComplete = true;
        _trainingFocusReason = string.IsNullOrWhiteSpace(reason)
            ? _trainingFocusReason
            : reason.Trim();
        RefreshTrainingSessionState(focus);
    }

    private void MarkDefenseScored(string focus)
    {
        DefenseGeneratedStepComplete = true;
        DefenseScoredStepComplete = true;
        RefreshTrainingSessionState(focus);
    }

    private void MarkReflectionSaved(string focus)
    {
        ReflectionSavedStepComplete = true;
        RefreshTrainingSessionState(focus);
    }

    private void RefreshTrainingSessionState(string? focusOverride = null)
    {
        RefreshTrainingHistoryMetadata();

        var focus = string.IsNullOrWhiteSpace(focusOverride)
            ? ResolveTrainingSessionFocus()
            : focusOverride.Trim();
        var stage = ResolveTrainingSessionStage();
        var stageSummary = stage switch
        {
            TrainingSessionStage.Plan =>
                "Choose the focus, difficulty, and question count. Starting from a review target will bias both practice and defense.",
            TrainingSessionStage.Practice =>
                "Practice is active. Answer the current question set, then score it to unlock the defense stage.",
            TrainingSessionStage.Defense =>
                "Run the oral defense on the same topic so the desk can test explanation quality and tradeoff reasoning.",
            TrainingSessionStage.Reflection =>
                "Capture what felt weak, what to review next, and how this ties back to Suite or career progress.",
            _ =>
                "This session is complete. The history file has been updated and the next review targets are ready.",
        };

        TrainingSession = new TrainingSessionState
        {
            Stage = stage,
            Focus = focus,
            FocusReason = _trainingFocusReason,
            StageSummary = stageSummary,
            PracticeGenerated = PracticeGeneratedStepComplete,
            PracticeScored = PracticeScoredStepComplete,
            DefenseGenerated = DefenseGeneratedStepComplete,
            DefenseScored = DefenseScoredStepComplete,
            ReflectionSaved = ReflectionSavedStepComplete,
            HistoryFilePath = HistoryFilePath,
            HistoryExists = HistoryExists,
            LastHistoryWriteAt = _trainingStore.GetLastWriteTime(),
        };
    }

    private string ResolveTrainingSessionFocus()
    {
        if (!string.IsNullOrWhiteSpace(_activeReviewTopic))
        {
            return _activeReviewTopic.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_currentPracticeTest?.Focus))
        {
            return _currentPracticeTest.Focus.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_oralDefenseScenario.Topic))
        {
            return _oralDefenseScenario.Topic.Trim();
        }

        if (!string.IsNullOrWhiteSpace(PracticeFocusText))
        {
            return PracticeFocusText.Trim();
        }

        return "Protection, grounding, standards, drafting safety";
    }

    private TrainingSessionStage ResolveTrainingSessionStage()
    {
        if (ReflectionSavedStepComplete)
        {
            return TrainingSessionStage.Complete;
        }

        if (DefenseScoredStepComplete)
        {
            return TrainingSessionStage.Reflection;
        }

        if (PracticeScoredStepComplete || DefenseGeneratedStepComplete)
        {
            return TrainingSessionStage.Defense;
        }

        if (PracticeGeneratedStepComplete)
        {
            return TrainingSessionStage.Practice;
        }

        return TrainingSessionStage.Plan;
    }

    private JobActivityItem StartJob(
        string title,
        string agent,
        string model,
        string summary,
        bool blocking = false
    )
    {
        var job = new JobActivityItem
        {
            Title = title,
            Agent = agent,
            Model = model,
            Status = "running",
            Summary = summary,
            StartedAt = DateTimeOffset.Now,
        };

        JobActivities.Insert(0, job);
        while (JobActivities.Count > 12)
        {
            JobActivities.RemoveAt(JobActivities.Count - 1);
        }

        ShowBlockingOverlay = blocking;
        RefreshJobSummary();
        OnPropertyChanged(nameof(HasJobActivities));
        return job;
    }

    private void CompleteJob(JobActivityItem? job, string? summary = null)
    {
        if (job is null)
        {
            ShowBlockingOverlay = false;
            RefreshJobSummary();
            return;
        }

        job.Status = "succeeded";
        job.CompletedAt = DateTimeOffset.Now;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            job.Summary = summary.Trim();
        }

        ShowBlockingOverlay = false;
        RefreshJobSummary();
    }

    private void FailJob(JobActivityItem? job, string summary)
    {
        if (job is null)
        {
            ShowBlockingOverlay = false;
            RefreshJobSummary();
            return;
        }

        job.Status = "failed";
        job.CompletedAt = DateTimeOffset.Now;
        job.Summary = summary;
        ShowBlockingOverlay = false;
        RefreshJobSummary();
    }

    private void RefreshJobSummary()
    {
        var activeJob = JobActivities.FirstOrDefault(item => item.IsActive);
        HasActiveJob = activeJob is not null;
        ActiveJobSummary = activeJob?.DisplaySummary ?? "No active jobs.";
        ActiveJobMeta = activeJob?.DisplayMeta ?? string.Empty;
        OnPropertyChanged(nameof(HasJobActivities));
    }

    private void RaiseWorkflowGlanceProperties()
    {
        OnPropertyChanged(nameof(PendingApprovalCount));
        OnPropertyChanged(nameof(PendingApprovalBadge));
        OnPropertyChanged(nameof(HasSelectedInboxSuggestion));
    }
}
