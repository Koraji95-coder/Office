using DailyDesk.Models;

namespace DailyDesk.ViewModels;

public sealed partial class MainViewModel
{
    private IReadOnlyList<AgentCard> BuildOfficeDesks(
        IReadOnlyList<string> installedModels,
        IReadOnlyList<AgentPolicy> policies,
        OperatorMemoryState state
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

        string ThreadSummary(string deskId) =>
            state.FindDeskThread(deskId)?.DisplaySummary ?? "No thread activity yet.";

        return
        [
            new AgentCard
            {
                Id = ChiefDeskId,
                Name = "Chief of Staff",
                Role = "Routes the office, sets the day, and keeps approvals and handoffs coherent.",
                Model = _settings.ChiefModel,
                Mode = PolicyMode("Chief of Staff", "Policy: autonomous prep"),
                Status = HasModel(_settings.ChiefModel) ? "ready" : "missing",
                Accent = "#C6A46D",
                Summary = "Owns daily routing across Suite, engineering, CAD, and business so work turns into real progress instead of parallel noise.",
                Focus = _settings.CareerFocus,
                ThreadSummary = ThreadSummary(ChiefDeskId),
            },
            new AgentCard
            {
                Id = EngineeringDeskId,
                Name = "Engineering Desk",
                Role = "Combines EE coaching, CAD workflow strategy, testing, and explanation drills.",
                Model = $"{_settings.MentorModel} + {_settings.TrainingModel}",
                Mode = PolicyMode("EE Mentor", "Policy: autonomous prep"),
                Status = HasModel(_settings.MentorModel) && HasModel(_settings.TrainingModel)
                    ? "ready"
                    : HasModel(_settings.MentorModel) || HasModel(_settings.TrainingModel)
                        ? "partial"
                        : "missing",
                Accent = "#5B8B7D",
                Summary = "Turns current work into electrical reasoning, drafting judgment, practice loops, and review-first CAD discipline.",
                Focus = $"{_settings.EngineeringFocus} {_settings.CadFocus}",
                ThreadSummary = ThreadSummary(EngineeringDeskId),
            },
            new AgentCard
            {
                Id = SuiteDeskId,
                Name = "Suite Context",
                Role = "Keeps the office aware of Suite trust, availability, and workflow context without pushing repo work.",
                Model = _settings.RepoModel,
                Mode = PolicyMode("Repo Coach", "Policy: prepare proposals only"),
                Status = HasModel(_settings.RepoModel) ? "ready" : "missing",
                Accent = "#7390FF",
                Summary = "Keeps the desks aligned to Suite as quiet background context so the office stays aware without becoming a repo console.",
                Focus = _settings.SuiteFocus,
                ThreadSummary = ThreadSummary(SuiteDeskId),
            },
            new AgentCard
            {
                Id = BusinessDeskId,
                Name = "Business Ops",
                Role = "Frames offers, internal ops, monetization paths, and measurable proof without hype.",
                Model = _settings.BusinessModel,
                Mode = PolicyMode("Business Strategist", "Policy: packaging only"),
                Status = HasModel(_settings.BusinessModel) ? "ready" : "missing",
                Accent = "#B7844E",
                Summary = "Translates current capability into operator value, pilot-shaped offers, and business moves that fit your actual position.",
                Focus = $"{_settings.BusinessFocus} {_settings.CareerFocus}",
                ThreadSummary = ThreadSummary(BusinessDeskId),
            },
        ];
    }

    private IReadOnlyList<DeskAction> BuildDeskActions(string deskId) =>
        deskId switch
        {
            ChiefDeskId =>
            [
                new DeskAction { Id = "chief.morning-pass", Label = "Daily Brief", Summary = "Refresh the chief brief and route the day." },
                new DeskAction { Id = "chief.daily-plan", Label = "Daily Plan", Summary = "Build the current operator plan." },
                new DeskAction { Id = "chief.refresh", Label = "Refresh Office", Summary = "Rescan models, Suite, knowledge, and memory." },
            ],
            EngineeringDeskId =>
            [
                new DeskAction { Id = "engineering.study_guide", Label = "Study Guide", Summary = "Build a notebook-grounded study guide." },
                new DeskAction { Id = "engineering.challenge", Label = "Challenge", Summary = "Generate the next EE/CAD challenge." },
                new DeskAction { Id = "engineering.practice", Label = "Practice", Summary = "Build the next practice set." },
                new DeskAction { Id = "engineering.defense", Label = "Defense", Summary = "Generate the next oral-defense drill." },
            ],
            SuiteDeskId =>
            [
                new DeskAction { Id = "suite.context", Label = "Suite Context", Summary = "Refresh quiet Suite awareness for the office." },
                new DeskAction { Id = "suite.research", Label = "Workflow Research", Summary = "Run research tied to Suite-adjacent workflow context." },
                new DeskAction { Id = "suite.refresh", Label = "Refresh Office", Summary = "Rescan Office context without surfacing repo guidance." },
            ],
            BusinessDeskId =>
            [
                new DeskAction { Id = "business.map", Label = "Business Map", Summary = "Frame the next monetization move." },
                new DeskAction { Id = "business.research", Label = "Market Research", Summary = "Run live research for offer or market proof." },
                new DeskAction { Id = "business.plan", Label = "Operator Plan", Summary = "Tie business moves back to the day plan." },
            ],
            _ => Array.Empty<DeskAction>(),
        };

    private IReadOnlyList<FocusCard> BuildOfficeParameterCards() =>
    [
        new FocusCard
        {
            Tag = "SUITE",
            Title = "Suite parameter",
            Summary = $"{_settings.SuiteFocus} Runtime feed: {_settings.SuiteRuntimeStatusEndpoint}",
            Accent = "#7390FF",
        },
        new FocusCard
        {
            Tag = "ENGINEERING",
            Title = "EE parameter",
            Summary = _settings.EngineeringFocus,
            Accent = "#5B8B7D",
        },
        new FocusCard
        {
            Tag = "CAD",
            Title = "CAD parameter",
            Summary = _settings.CadFocus,
            Accent = "#4E7C99",
        },
        new FocusCard
        {
            Tag = "BUSINESS",
            Title = "Business parameter",
            Summary = $"{_settings.BusinessFocus} Career proof: {_settings.CareerFocus}",
            Accent = "#C6A46D",
        },
    ];

    private IReadOnlyList<FocusCard> BuildDeskPanels(string deskId) =>
        deskId switch
        {
            ChiefDeskId =>
            [
                new FocusCard
                {
                    Tag = "TODAY",
                    Title = "Operator routing",
                    Summary = $"{DailyObjective} {DailyMorningPlan}",
                    Accent = "#C6A46D",
                },
                new FocusCard
                {
                    Tag = "INBOX",
                    Title = "Approval pressure",
                    Summary = ApprovalInboxSummary,
                    Accent = "#7390FF",
                },
                new FocusCard
                {
                    Tag = "PROGRESS",
                    Title = "Career engine",
                    Summary = CareerEngineProgressSummary,
                    Accent = "#5B8B7D",
                },
            ],
            EngineeringDeskId =>
            [
                new FocusCard
                {
                    Tag = "TRAINING",
                    Title = "Next engineering move",
                    Summary = TrainingNextActionSummary,
                    Accent = "#5B8B7D",
                },
                new FocusCard
                {
                    Tag = "DEFENSE",
                    Title = "Oral-defense state",
                    Summary = $"{DefenseHistorySummary} {ChallengeBrief}",
                    Accent = "#7390FF",
                },
                new FocusCard
                {
                    Tag = "CAD",
                    Title = "Current engineering context",
                    Summary = _settings.CadFocus,
                    Accent = "#4E7C99",
                },
            ],
            SuiteDeskId =>
            [
                new FocusCard
                {
                    Tag = "SUITE",
                    Title = "Quiet Suite context",
                    Summary = BuildQuietSuiteContextSummary(_suiteSnapshot),
                    Accent = "#7390FF",
                },
                new FocusCard
                {
                    Tag = "TRUST",
                    Title = "Runtime trust",
                    Summary = BuildQuietSuiteTrustSummary(_suiteSnapshot),
                    Accent = "#C6A46D",
                },
                new FocusCard
                {
                    Tag = "ROLE",
                    Title = "How this helps",
                    Summary = "The office uses Suite as read-only background context for decisions, not as a code-change queue.",
                    Accent = "#5B8B7D",
                },
            ],
            BusinessDeskId =>
            [
                new FocusCard
                {
                    Tag = "OFFER",
                    Title = "Current packaging move",
                    Summary = MonetizationBrief,
                    Accent = "#C6A46D",
                },
                new FocusCard
                {
                    Tag = "PROOF",
                    Title = "What this work proves",
                    Summary = _settings.CareerFocus,
                    Accent = "#7390FF",
                },
                new FocusCard
                {
                    Tag = "MARKET",
                    Title = "Business watch",
                    Summary = WatchlistSummary,
                    Accent = "#5B8B7D",
                },
            ],
            _ => Array.Empty<FocusCard>(),
        };

    private IReadOnlyList<SuggestedAction> BuildDeskSuggestions(string deskId)
    {
        var recent = _operatorMemoryState.RecentSuggestions
            .Where(IsVisibleOfficeSuggestion)
            .ToList();
        return deskId switch
        {
            ChiefDeskId => recent
                .Where(item =>
                    item.SourceAgent.Equals("Chief of Staff", StringComparison.OrdinalIgnoreCase)
                    || item.RequiresApproval
                )
                .Take(4)
                .ToList(),
            EngineeringDeskId => recent
                .Where(item =>
                    item.SourceAgent.Equals("EE Mentor", StringComparison.OrdinalIgnoreCase)
                    || item.SourceAgent.Equals("Test Builder", StringComparison.OrdinalIgnoreCase)
                )
                .Take(4)
                .ToList(),
            SuiteDeskId => Array.Empty<SuggestedAction>(),
            BusinessDeskId => recent
                .Where(item =>
                    item.SourceAgent.Equals("Business Strategist", StringComparison.OrdinalIgnoreCase)
                )
                .Take(4)
                .ToList(),
            _ => Array.Empty<SuggestedAction>(),
        };
    }

    private string BuildDeskContextSummary(string deskId, DeskThreadState thread)
    {
        return deskId switch
        {
            ChiefDeskId =>
                $"Suite: {BuildQuietSuiteContextSummary(_suiteSnapshot)} Training: {_trainingHistorySummary.OverallSummary} Inbox: {ApprovalInboxSummary} Thread: {thread.DisplaySummary}",
            EngineeringDeskId =>
                $"Need: {_learningProfile.CurrentNeed} Review: {_trainingHistorySummary.ReviewQueueSummary} CAD: {_settings.CadFocus} Thread: {thread.DisplaySummary}",
            SuiteDeskId =>
                $"Suite context: {BuildQuietSuiteContextSummary(_suiteSnapshot)} Trust: {BuildQuietSuiteTrustSummary(_suiteSnapshot)} Thread: {thread.DisplaySummary}",
            BusinessDeskId =>
                $"Business: {_settings.BusinessFocus} Monetization: {JoinOrNone(_suiteSnapshot.MonetizationMoves)} Career: {_settings.CareerFocus} Thread: {thread.DisplaySummary}",
            _ => thread.DisplaySummary,
        };
    }

    private string BuildDeskPromptHint(string deskId) =>
        deskId switch
        {
            ChiefDeskId => "Ask for routing, prioritization, a decision summary, or type /research <query>.",
            EngineeringDeskId => "Ask for an EE explanation, CAD review logic, practice help, or type /research <query>.",
            SuiteDeskId => "Ask for quiet Suite awareness, runtime trust interpretation, workflow context, or type /research <query>.",
            BusinessDeskId => "Ask for offer framing, pilot ideas, internal ops guidance, or type /research <query>.",
            _ => "Ask the selected desk for the next move.",
        };

    private string ResolveResearchPerspective(string deskId) =>
        deskId switch
        {
            ChiefDeskId => "Chief of Staff",
            SuiteDeskId => "Chief of Staff",
            BusinessDeskId => "Business Strategist",
            _ => "EE Mentor",
        };

    private string ResolveDeskIdFromPerspective(string perspective) =>
        perspective switch
        {
            "Chief of Staff" => ChiefDeskId,
            "Repo Coach" => SuiteDeskId,
            "Business Strategist" => BusinessDeskId,
            _ => EngineeringDeskId,
        };

    private string ResolveDeskModel(string deskId) =>
        deskId switch
        {
            ChiefDeskId => _settings.ChiefModel,
            EngineeringDeskId => _settings.MentorModel,
            SuiteDeskId => _settings.RepoModel,
            BusinessDeskId => _settings.BusinessModel,
            _ => _settings.ChiefModel,
        };

    private string ResolveDeskTitle(string deskId) =>
        deskId switch
        {
            ChiefDeskId => "Chief of Staff",
            EngineeringDeskId => "Engineering Desk",
            SuiteDeskId => "Suite Context",
            BusinessDeskId => "Business Ops",
            _ => "Desk",
        };

    private static string JoinOrNone(IReadOnlyList<string> items) =>
        items.Count == 0 ? "none recorded" : string.Join("; ", items);
}
