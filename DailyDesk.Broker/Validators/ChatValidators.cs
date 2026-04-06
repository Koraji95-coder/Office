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
