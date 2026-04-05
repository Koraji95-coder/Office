using DailyDesk.Services;
using FluentValidation;

namespace DailyDesk.Broker;

internal sealed class ChatRouteRequestValidator : AbstractValidator<ChatRouteRequest>
{
    public ChatRouteRequestValidator()
    {
        RuleFor(x => x.Route)
            .NotEmpty()
            .WithMessage("Route is required.")
            .Must(route =>
            {
                var trimmed = route?.Trim().ToLowerInvariant();
                return !string.IsNullOrWhiteSpace(trimmed)
                    && OfficeRouteCatalog.KnownRoutes.Contains(trimmed, StringComparer.OrdinalIgnoreCase);
            })
            .WithMessage($"Route must be one of: {string.Join(", ", OfficeRouteCatalog.KnownRoutes)}.");
    }
}

internal sealed class ChatSendRequestValidator : AbstractValidator<ChatSendRequest>
{
    public ChatSendRequestValidator()
    {
        RuleFor(x => x.Prompt)
            .NotEmpty()
            .WithMessage("Prompt is required.");
    }
}

internal sealed class StudyScoreDefenseRequestValidator : AbstractValidator<StudyScoreDefenseRequest>
{
    public StudyScoreDefenseRequestValidator()
    {
        RuleFor(x => x.Answer)
            .NotEmpty()
            .WithMessage("Answer is required.");
    }
}

internal sealed class StudySaveReflectionRequestValidator : AbstractValidator<StudySaveReflectionRequest>
{
    public StudySaveReflectionRequestValidator()
    {
        RuleFor(x => x.Reflection)
            .NotEmpty()
            .WithMessage("Reflection is required.");
    }
}

internal sealed class ResearchRunRequestValidator : AbstractValidator<ResearchRunRequest>
{
    public ResearchRunRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query is required.");
    }
}

internal sealed class WatchlistRunRequestValidator : AbstractValidator<WatchlistRunRequest>
{
    public WatchlistRunRequestValidator()
    {
        RuleFor(x => x.WatchlistId)
            .NotEmpty()
            .WithMessage("WatchlistId is required.");
    }
}

internal sealed class InboxResolveRequestValidator : AbstractValidator<InboxResolveRequest>
{
    private static readonly string[] ValidStatuses = ["accepted", "deferred", "rejected"];

    public InboxResolveRequestValidator()
    {
        RuleFor(x => x.SuggestionId)
            .NotEmpty()
            .WithMessage("SuggestionId is required.");

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required.")
            .Must(status => ValidStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status must be one of: accepted, deferred, rejected.");
    }
}

internal sealed class CreateScheduleRequestValidator : AbstractValidator<CreateScheduleRequest>
{
    public CreateScheduleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.");

        RuleFor(x => x.JobType)
            .NotEmpty()
            .WithMessage("JobType is required.");

        RuleFor(x => x.CronExpression)
            .NotEmpty()
            .WithMessage("CronExpression is required.")
            .Must(cron => DailyDesk.Services.JobSchedulerStore.ComputeNextRun(cron, DateTimeOffset.Now) is not null)
            .WithMessage("CronExpression is not a valid cron expression or simple interval (e.g. 'every 30m', 'every 2h', '0 8 * * *').");
    }
}

internal sealed class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
    public UpdateScheduleRequestValidator()
    {
        RuleFor(x => x.CronExpression)
            .Must(cron => cron is null || DailyDesk.Services.JobSchedulerStore.ComputeNextRun(cron, DateTimeOffset.Now) is not null)
            .WithMessage("CronExpression is not a valid cron expression or simple interval.");
    }
}

internal sealed class CreateWorkflowRequestValidator : AbstractValidator<CreateWorkflowRequest>
{
    public CreateWorkflowRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.");

        RuleFor(x => x.Steps)
            .NotEmpty()
            .WithMessage("At least one step is required.");

        RuleForEach(x => x.Steps)
            .ChildRules(step =>
            {
                step.RuleFor(s => s.JobType)
                    .NotEmpty()
                    .WithMessage("Step JobType is required.");
            });

        RuleFor(x => x.FailurePolicy)
            .Must(p => p is null
                       || p.Equals(DailyDesk.Models.WorkflowFailurePolicy.Abort, StringComparison.OrdinalIgnoreCase)
                       || p.Equals(DailyDesk.Models.WorkflowFailurePolicy.Continue, StringComparison.OrdinalIgnoreCase))
            .WithMessage("FailurePolicy must be 'abort' or 'continue'.");
    }
}
