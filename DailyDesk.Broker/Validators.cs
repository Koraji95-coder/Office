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
