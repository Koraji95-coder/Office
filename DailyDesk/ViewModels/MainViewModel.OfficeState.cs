using System.Collections.ObjectModel;
using System.Windows.Input;
using DailyDesk.Models;

namespace DailyDesk.ViewModels;

public sealed partial class MainViewModel
{
    private const string ChiefDeskId = "chief";
    private const string EngineeringDeskId = "engineering";
    private const string SuiteDeskId = "suite";
    private const string BusinessDeskId = "business";

    private RelayCommand _selectDeskCommand = null!;
    private RelayCommand _sendDeskMessageCommand = null!;
    private RelayCommand _runDeskActionCommand = null!;

    private AgentCard? _selectedDesk;
    private string _deskMessageDraft = string.Empty;
    private string _selectedDeskSummary = "Choose a desk to begin direct work.";
    private string _selectedDeskThreadSummary = "No desk thread selected.";
    private string _selectedDeskContextSummary =
        "The office syncs Suite, engineering, CAD, and business context into each desk.";
    private string _selectedDeskPromptHint =
        "Ask a desk for a brief, explanation, planning move, research direction, or business framing.";

    public ObservableCollection<DeskMessageRecord> DeskMessages { get; } = new();
    public ObservableCollection<DeskAction> DeskQuickActions { get; } = new();
    public ObservableCollection<FocusCard> DeskPanels { get; } = new();
    public ObservableCollection<FocusCard> OfficeParameterCards { get; } = new();
    public ObservableCollection<SuggestedAction> DeskSuggestions { get; } = new();

    public string OfficeName => _settings.OfficeName;

    public string OfficeSubtitle =>
        "Suite-aware direct desks for engineering, CAD workflow, and business operations.";

    public AgentCard? SelectedDesk
    {
        get => _selectedDesk;
        set
        {
            if (!SetProperty(ref _selectedDesk, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelectedDesk));
            RefreshSelectedDeskState();
            _sendDeskMessageCommand?.RaiseCanExecuteChanged();
            _runDeskActionCommand?.RaiseCanExecuteChanged();
        }
    }

    public bool HasSelectedDesk => SelectedDesk is not null;

    public string DeskMessageDraft
    {
        get => _deskMessageDraft;
        set
        {
            if (!SetProperty(ref _deskMessageDraft, value))
            {
                return;
            }

            _sendDeskMessageCommand?.RaiseCanExecuteChanged();
        }
    }

    public string SelectedDeskSummary
    {
        get => _selectedDeskSummary;
        private set => SetProperty(ref _selectedDeskSummary, value);
    }

    public string SelectedDeskThreadSummary
    {
        get => _selectedDeskThreadSummary;
        private set => SetProperty(ref _selectedDeskThreadSummary, value);
    }

    public string SelectedDeskContextSummary
    {
        get => _selectedDeskContextSummary;
        private set => SetProperty(ref _selectedDeskContextSummary, value);
    }

    public string SelectedDeskPromptHint
    {
        get => _selectedDeskPromptHint;
        private set => SetProperty(ref _selectedDeskPromptHint, value);
    }

    public string SuiteDoctorSummary => BuildQuietSuiteTrustSummary(_suiteSnapshot);

    public string SuiteDoctorLeadDetail => BuildQuietSuiteContextSummary(_suiteSnapshot);

    public string WorkshopSignalSummary =>
        BuildQuietSuiteContextSummary(_suiteSnapshot);

    public ICommand SelectDeskCommand => _selectDeskCommand;

    public ICommand SendDeskMessageCommand => _sendDeskMessageCommand;

    public ICommand RunDeskActionCommand => _runDeskActionCommand;

    private void InitializeAgentOfficeLayer()
    {
        _selectDeskCommand = new RelayCommand(
            parameter => SelectedDesk = parameter as AgentCard,
            parameter => parameter is AgentCard
        );
        _sendDeskMessageCommand = new RelayCommand(
            async _ => await SendDeskMessageAsync(),
            _ => !IsBusy && SelectedDesk is not null && !string.IsNullOrWhiteSpace(DeskMessageDraft)
        );
        _runDeskActionCommand = new RelayCommand(
            async parameter => await RunDeskActionAsync(parameter as DeskAction),
            parameter => !IsBusy && parameter is DeskAction && SelectedDesk is not null
        );

        Replace(OfficeParameterCards, BuildOfficeParameterCards());
    }

    private void RaiseAgentOfficeCommandState()
    {
        _selectDeskCommand.RaiseCanExecuteChanged();
        _sendDeskMessageCommand.RaiseCanExecuteChanged();
        _runDeskActionCommand.RaiseCanExecuteChanged();
    }

    private void RefreshAgentOfficeState()
    {
        Replace(
            Agents,
            BuildOfficeDesks(
                _installedModelCache,
                AgentPolicies.ToList(),
                _operatorMemoryState
            )
        );
        Replace(OfficeParameterCards, BuildOfficeParameterCards());
        EnsureSelectedDesk();
        RefreshSelectedDeskState();
        RaiseAgentOfficeCommandState();
        OnPropertyChanged(nameof(SuiteDoctorSummary));
        OnPropertyChanged(nameof(SuiteDoctorLeadDetail));
        OnPropertyChanged(nameof(WorkshopSignalSummary));
    }

    private void EnsureSelectedDesk()
    {
        var currentId = SelectedDesk?.Id;
        var next =
            Agents.FirstOrDefault(item =>
                item.Id.Equals(currentId, StringComparison.OrdinalIgnoreCase)
            )
            ?? Agents.FirstOrDefault();

        if (!ReferenceEquals(next, SelectedDesk))
        {
            SelectedDesk = next;
        }
    }

    private void RefreshSelectedDeskState()
    {
        if (SelectedDesk is null)
        {
            Replace(DeskMessages, Array.Empty<DeskMessageRecord>());
            Replace(DeskQuickActions, Array.Empty<DeskAction>());
            Replace(DeskPanels, Array.Empty<FocusCard>());
            Replace(DeskSuggestions, Array.Empty<SuggestedAction>());
            SelectedDeskSummary = "Choose a desk to begin direct work.";
            SelectedDeskThreadSummary = "No desk thread selected.";
            SelectedDeskContextSummary =
                "The office syncs Suite, engineering, CAD, and business context into each desk.";
            SelectedDeskPromptHint =
                "Ask a desk for a brief, explanation, planning move, research direction, or business framing.";
            return;
        }

        var thread = ResolveDeskThread(SelectedDesk.Id);
        Replace(DeskMessages, thread.Messages.OrderBy(item => item.CreatedAt));
        Replace(DeskQuickActions, BuildDeskActions(SelectedDesk.Id));
        Replace(DeskPanels, BuildDeskPanels(SelectedDesk.Id));
        Replace(DeskSuggestions, BuildDeskSuggestions(SelectedDesk.Id));

        SelectedDeskSummary = SelectedDesk.Summary;
        SelectedDeskThreadSummary = thread.DisplaySummary;
        SelectedDeskContextSummary = BuildDeskContextSummary(SelectedDesk.Id, thread);
        SelectedDeskPromptHint = BuildDeskPromptHint(SelectedDesk.Id);
    }
}
