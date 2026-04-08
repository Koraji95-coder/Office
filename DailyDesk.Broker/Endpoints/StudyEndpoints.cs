using DailyDesk.Models;
using DailyDesk.Services;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DailyDesk.Broker;

internal static class StudyEndpoints
{
    public static void MapStudyEndpoints(this IEndpointRouteBuilder app, ILogger logger)
    {
        app.MapPost("/api/study/start", async (StudyStartRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
        {
            try
            {
                var session = await orchestrator.StartStudyAsync(request.Focus, request.Difficulty, request.QuestionCount, ct);
                var state = await orchestrator.GetStateAsync(ct);
                return Results.Ok(new { session, state });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Office broker study start endpoint failed.");
                return Results.Problem(
                    detail: "An unexpected error occurred. See server logs for details.",
                    title: "Failed to start study session",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        });

        app.MapPost("/api/study/generate-practice", async (StudyStartRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
        {
            try
            {
                var practice = await orchestrator.GeneratePracticeAsync(request.Focus, request.Difficulty, request.QuestionCount, ct);
                var state = await orchestrator.GetStateAsync(ct);
                return Results.Ok(new { practice, state });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Office broker practice generation endpoint failed.");
                return Results.Problem(
                    detail: "An unexpected error occurred. See server logs for details.",
                    title: "Failed to generate practice",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        });

        app.MapPost("/api/study/score-practice", async (StudyScorePracticeRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
        {
            try
            {
                var attempt = await orchestrator.ScorePracticeAsync(request.Answers ?? Array.Empty<OfficePracticeAnswerInput>(), ct);
                var state = await orchestrator.GetStateAsync(ct);
                return Results.Ok(new { attempt, state });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { error = "The requested operation could not be completed." });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Office broker practice scoring endpoint failed.");
                return Results.Problem(
                    detail: "An unexpected error occurred. See server logs for details.",
                    title: "Failed to score practice",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        });

        app.MapPost("/api/study/generate-defense", async (StudyGenerateDefenseRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
        {
            try
            {
                var scenario = await orchestrator.GenerateDefenseAsync(request.Topic, ct);
                var state = await orchestrator.GetStateAsync(ct);
                return Results.Ok(new { scenario, state });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Office broker defense generation endpoint failed.");
                return Results.Problem(
                    detail: "An unexpected error occurred. See server logs for details.",
                    title: "Failed to generate defense",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        });

        app.MapPost("/api/study/score-defense", async (StudyScoreDefenseRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
        {
            var validation = new StudyScoreDefenseRequestValidator().Validate(request);
            if (!validation.IsValid)
            {
                return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
            }

            try
            {
                var attempt = await orchestrator.ScoreDefenseAsync(request.Answer, ct);
                var state = await orchestrator.GetStateAsync(ct);
                return Results.Ok(new { attempt, state });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { error = "The requested operation could not be completed." });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Office broker defense scoring endpoint failed.");
                return Results.Problem(
                    detail: "An unexpected error occurred. See server logs for details.",
                    title: "Failed to score defense",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        });

        app.MapPost("/api/study/save-reflection", async (StudySaveReflectionRequest request, OfficeBrokerOrchestrator orchestrator, CancellationToken ct) =>
        {
            var validation = new StudySaveReflectionRequestValidator().Validate(request);
            if (!validation.IsValid)
            {
                return Results.BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
            }

            try
            {
                var reflection = await orchestrator.SaveReflectionAsync(request.Reflection, ct);
                var state = await orchestrator.GetStateAsync(ct);
                return Results.Ok(new { reflection, state });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { error = "The requested operation could not be completed." });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Office broker reflection save endpoint failed.");
                return Results.Problem(
                    detail: "An unexpected error occurred. See server logs for details.",
                    title: "Failed to save reflection",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        });
    }
}

internal sealed record StudyStartRequest(string? Focus, string? Difficulty, int? QuestionCount);
internal sealed record StudyScorePracticeRequest(IReadOnlyList<OfficePracticeAnswerInput>? Answers);
internal sealed record StudyGenerateDefenseRequest(string? Topic);
internal sealed record StudyScoreDefenseRequest(string Answer);
internal sealed record StudySaveReflectionRequest(string Reflection);

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
