using FluentValidation;

namespace DailyDesk.Broker;

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
