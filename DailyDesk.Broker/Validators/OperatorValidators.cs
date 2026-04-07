using FluentValidation;

namespace DailyDesk.Broker;

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
