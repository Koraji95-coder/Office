using FluentValidation;

namespace DailyDesk.Broker;

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
