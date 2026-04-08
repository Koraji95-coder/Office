using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify FluentValidation request validators in DailyDesk.Broker
/// behave correctly in real API scenarios using the in-process test server.
///
/// Each validator is exercised through its registered endpoint so that the full
/// validation → early-return pipeline is tested end-to-end, not just the validator
/// class in isolation.
///
/// Tests are grouped by endpoint/validator:
///   1. ChatRouteRequestValidator         – POST /api/chat/route
///   2. ChatSendRequestValidator           – POST /api/chat/send
///   3. StudyScoreDefenseRequestValidator  – POST /api/study/score-defense
///   4. StudySaveReflectionRequestValidator– POST /api/study/save-reflection
///   5. ResearchRunRequestValidator        – POST /api/research/run
///   6. WatchlistRunRequestValidator       – POST /api/watchlists/run
///   7. InboxResolveRequestValidator       – POST /api/inbox/resolve
///   8. CreateScheduleRequestValidator     – POST /api/schedules
///   9. UpdateScheduleRequestValidator     – PUT  /api/schedules/{id}
///  10. CreateWorkflowRequestValidator     – POST /api/workflows
/// </summary>
[Collection("BrokerIntegrationTests")]
public sealed class BrokerValidatorIntegrationTests : IClassFixture<BrokerWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BrokerValidatorIntegrationTests(BrokerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -----------------------------------------------------------------------
    // Helper: verify that a 400 response was triggered by FluentValidation
    // (uses an "errors" array) and that the given message is present.
    // -----------------------------------------------------------------------

    private static async Task AssertValidationError(
        HttpResponseMessage response,
        string expectedMessage)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json!.ContainsKey("errors"),
            $"Validation 400 responses must use an 'errors' array. Body: {json}");
        var errors = json["errors"]!.AsArray()
            .Select(e => e!.GetValue<string>())
            .ToList();
        Assert.Contains(expectedMessage, errors);
    }

    private static async Task AssertValidationErrorContaining(
        HttpResponseMessage response,
        string expectedFragment)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json!.ContainsKey("errors"),
            $"Validation 400 responses must use an 'errors' array. Body: {json}");
        var errors = json["errors"]!.AsArray()
            .Select(e => e!.GetValue<string>())
            .ToList();
        Assert.True(
            errors.Any(e => e.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase)),
            $"Expected an error containing '{expectedFragment}' but got: [{string.Join(", ", errors)}]");
    }

    /// <summary>
    /// Asserts that a valid input is not rejected by FluentValidation.
    /// Accepts any non-400 status code, or a 400 that originated from business logic
    /// (uses a single "error" string field) rather than from the validator
    /// (which uses an "errors" array field).
    /// </summary>
    private static async Task AssertNotRejectedByValidator(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var json = await response.Content.ReadFromJsonAsync<JsonObject>();
            Assert.NotNull(json);
            Assert.False(
                json!.ContainsKey("errors"),
                $"Validator must not reject a valid input. Response body: {json}");
        }
        // Any other status code (200, 201, 400-from-business-logic, 404, 500) is acceptable.
    }

    // -----------------------------------------------------------------------
    // 1. ChatRouteRequestValidator  –  POST /api/chat/route
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ChatRoute_EmptyRoute_Returns400WithRouteRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/chat/route", new { Route = "" });
        await AssertValidationError(response, "Route is required.");
    }

    [Fact]
    public async Task ChatRoute_WhitespaceRoute_Returns400WithRouteRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/chat/route", new { Route = "   " });
        await AssertValidationError(response, "Route is required.");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("admin")]
    [InlineData("desk")]
    public async Task ChatRoute_UnknownRoute_Returns400WithKnownRoutesMessage(string route)
    {
        var response = await _client.PostAsJsonAsync("/api/chat/route", new { Route = route });
        await AssertValidationErrorContaining(response, "Route must be one of:");
    }

    [Theory]
    [InlineData("chief")]
    [InlineData("engineering")]
    [InlineData("suite")]
    [InlineData("business")]
    [InlineData("ml")]
    public async Task ChatRoute_KnownRoute_IsNotRejectedByValidator(string route)
    {
        var response = await _client.PostAsJsonAsync("/api/chat/route", new { Route = route });
        await AssertNotRejectedByValidator(response);
    }

    // -----------------------------------------------------------------------
    // 2. ChatSendRequestValidator  –  POST /api/chat/send
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ChatSend_EmptyPrompt_Returns400WithPromptRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/chat/send", new { Prompt = "" });
        await AssertValidationError(response, "Prompt is required.");
    }

    [Fact]
    public async Task ChatSend_WhitespacePrompt_Returns400WithPromptRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/chat/send", new { Prompt = "   " });
        await AssertValidationError(response, "Prompt is required.");
    }

    [Fact]
    public async Task ChatSend_NonEmptyPrompt_IsNotRejectedByValidator()
    {
        var response = await _client.PostAsJsonAsync("/api/chat/send", new { Prompt = "Hello!" });
        await AssertNotRejectedByValidator(response);
    }

    // -----------------------------------------------------------------------
    // 3. StudyScoreDefenseRequestValidator  –  POST /api/study/score-defense
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StudyScoreDefense_EmptyAnswer_Returns400WithAnswerRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/study/score-defense", new { Answer = "" });
        await AssertValidationError(response, "Answer is required.");
    }

    [Fact]
    public async Task StudyScoreDefense_WhitespaceAnswer_Returns400WithAnswerRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/study/score-defense", new { Answer = "   " });
        await AssertValidationError(response, "Answer is required.");
    }

    [Fact]
    public async Task StudyScoreDefense_NonEmptyAnswer_IsNotRejectedByValidator()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/study/score-defense",
            new { Answer = "My well-reasoned answer." }
        );
        await AssertNotRejectedByValidator(response);
    }

    // -----------------------------------------------------------------------
    // 4. StudySaveReflectionRequestValidator  –  POST /api/study/save-reflection
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StudySaveReflection_EmptyReflection_Returns400WithReflectionRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/study/save-reflection",
            new { Reflection = "" }
        );
        await AssertValidationError(response, "Reflection is required.");
    }

    [Fact]
    public async Task StudySaveReflection_WhitespaceReflection_Returns400WithReflectionRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/study/save-reflection",
            new { Reflection = "   " }
        );
        await AssertValidationError(response, "Reflection is required.");
    }

    [Fact]
    public async Task StudySaveReflection_NonEmptyReflection_IsNotRejectedByValidator()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/study/save-reflection",
            new { Reflection = "Today I learned about integration testing." }
        );
        await AssertNotRejectedByValidator(response);
    }

    // -----------------------------------------------------------------------
    // 5. ResearchRunRequestValidator  –  POST /api/research/run
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResearchRun_EmptyQuery_Returns400WithQueryRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/research/run", new { Query = "" });
        await AssertValidationError(response, "Query is required.");
    }

    [Fact]
    public async Task ResearchRun_WhitespaceQuery_Returns400WithQueryRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/research/run", new { Query = "   " });
        await AssertValidationError(response, "Query is required.");
    }

    [Fact]
    public async Task ResearchRun_NonEmptyQuery_IsNotRejectedByValidator()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/research/run",
            new { Query = "AI trends 2025" }
        );
        await AssertNotRejectedByValidator(response);
    }

    // -----------------------------------------------------------------------
    // 6. WatchlistRunRequestValidator  –  POST /api/watchlists/run
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WatchlistRun_EmptyWatchlistId_Returns400WithWatchlistIdRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/watchlists/run", new { WatchlistId = "" });
        await AssertValidationError(response, "WatchlistId is required.");
    }

    [Fact]
    public async Task WatchlistRun_WhitespaceWatchlistId_Returns400WithWatchlistIdRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/watchlists/run",
            new { WatchlistId = "   " }
        );
        await AssertValidationError(response, "WatchlistId is required.");
    }

    [Fact]
    public async Task WatchlistRun_NonEmptyWatchlistId_IsNotRejectedByValidator()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/watchlists/run",
            new { WatchlistId = "wl-001" }
        );
        // Validation passes; the orchestrator may fail for business reasons (400 or 500),
        // but the response must not contain a validator-originated "errors" array.
        await AssertNotRejectedByValidator(response);
    }

    // -----------------------------------------------------------------------
    // 7. InboxResolveRequestValidator  –  POST /api/inbox/resolve
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InboxResolve_EmptySuggestionId_Returns400WithSuggestionIdRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/inbox/resolve",
            new { SuggestionId = "", Status = "accepted" }
        );
        await AssertValidationError(response, "SuggestionId is required.");
    }

    [Fact]
    public async Task InboxResolve_EmptyStatus_Returns400WithStatusRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/inbox/resolve",
            new { SuggestionId = "sg-001", Status = "" }
        );
        await AssertValidationError(response, "Status is required.");
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("deny")]
    [InlineData("pending")]
    public async Task InboxResolve_UnknownStatus_Returns400WithKnownStatusesMessage(string status)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/inbox/resolve",
            new { SuggestionId = "sg-001", Status = status }
        );
        await AssertValidationError(response, "Status must be one of: accepted, deferred, rejected.");
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("deferred")]
    [InlineData("rejected")]
    public async Task InboxResolve_ValidStatus_IsNotRejectedByValidator(string status)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/inbox/resolve",
            new { SuggestionId = "sg-does-not-exist", Status = status }
        );
        await AssertNotRejectedByValidator(response);
    }

    // -----------------------------------------------------------------------
    // 8. CreateScheduleRequestValidator  –  POST /api/schedules
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateSchedule_EmptyName_Returns400WithNameRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/schedules",
            new { Name = "", JobType = "ml-analytics", CronExpression = "every 1h" }
        );
        await AssertValidationError(response, "Name is required.");
    }

    [Fact]
    public async Task CreateSchedule_EmptyJobType_Returns400WithJobTypeRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/schedules",
            new { Name = "Daily report", JobType = "", CronExpression = "every 1h" }
        );
        await AssertValidationError(response, "JobType is required.");
    }

    [Fact]
    public async Task CreateSchedule_EmptyCronExpression_Returns400WithCronRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/schedules",
            new { Name = "Daily report", JobType = "ml-analytics", CronExpression = "" }
        );
        await AssertValidationError(response, "CronExpression is required.");
    }

    [Theory]
    [InlineData("not-a-cron")]
    [InlineData("every xm")]
    [InlineData("1 2 3")]
    public async Task CreateSchedule_InvalidCronExpression_Returns400WithInvalidCronMessage(string cron)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/schedules",
            new { Name = "Daily report", JobType = "ml-analytics", CronExpression = cron }
        );
        await AssertValidationErrorContaining(response, "not a valid cron expression");
    }

    [Theory]
    [InlineData("0 8 * * *")]
    [InlineData("every 30m")]
    [InlineData("every 2h")]
    public async Task CreateSchedule_ValidRequest_IsNotRejectedByValidator(string cron)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/schedules",
            new { Name = "My Schedule", JobType = "ml-analytics", CronExpression = cron }
        );
        await AssertNotRejectedByValidator(response);
    }

    // -----------------------------------------------------------------------
    // 9. UpdateScheduleRequestValidator  –  PUT /api/schedules/{id}
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("not-a-cron")]
    [InlineData("every xm")]
    public async Task UpdateSchedule_InvalidCronExpression_Returns400WithInvalidCronMessage(string cron)
    {
        var response = await _client.PutAsJsonAsync(
            "/api/schedules/any-schedule-id",
            new { CronExpression = cron }
        );
        await AssertValidationErrorContaining(response, "not a valid cron expression");
    }

    [Fact]
    public async Task UpdateSchedule_NullCronExpression_IsNotRejectedByValidator()
    {
        // null CronExpression means "no change"; the validator allows it.
        var response = await _client.PutAsJsonAsync(
            "/api/schedules/nonexistent-schedule-id",
            new { Name = "Renamed", CronExpression = (string?)null }
        );
        await AssertNotRejectedByValidator(response);
    }

    [Theory]
    [InlineData("0 8 * * *")]
    [InlineData("every 1h")]
    public async Task UpdateSchedule_ValidCronExpression_IsNotRejectedByValidator(string cron)
    {
        var response = await _client.PutAsJsonAsync(
            "/api/schedules/nonexistent-schedule-id",
            new { CronExpression = cron }
        );
        await AssertNotRejectedByValidator(response);
    }

    // -----------------------------------------------------------------------
    // 10. CreateWorkflowRequestValidator  –  POST /api/workflows
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateWorkflow_EmptyName_Returns400WithNameRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/workflows",
            new { Name = "", Steps = new[] { new { JobType = "ml-analytics" } } }
        );
        await AssertValidationError(response, "Name is required.");
    }

    [Fact]
    public async Task CreateWorkflow_NoSteps_Returns400WithAtLeastOneStepRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/workflows",
            new { Name = "My workflow", Steps = Array.Empty<object>() }
        );
        await AssertValidationError(response, "At least one step is required.");
    }

    [Fact]
    public async Task CreateWorkflow_StepWithEmptyJobType_Returns400WithStepJobTypeRequiredMessage()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/workflows",
            new { Name = "My workflow", Steps = new[] { new { JobType = "" } } }
        );
        await AssertValidationError(response, "Step JobType is required.");
    }

    [Theory]
    [InlineData("skip")]
    [InlineData("retry")]
    [InlineData("fail")]
    public async Task CreateWorkflow_InvalidFailurePolicy_Returns400WithAbortOrContinueMessage(string policy)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/workflows",
            new
            {
                Name = "My workflow",
                FailurePolicy = policy,
                Steps = new[] { new { JobType = "ml-analytics" } }
            }
        );
        await AssertValidationError(response, "FailurePolicy must be 'abort' or 'continue'.");
    }

    [Theory]
    [InlineData("abort")]
    [InlineData("continue")]
    [InlineData("ABORT")]
    [InlineData("Continue")]
    public async Task CreateWorkflow_ValidFailurePolicy_IsNotRejectedByValidator(string policy)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/workflows",
            new
            {
                Name = "My workflow",
                FailurePolicy = policy,
                Steps = new[] { new { JobType = "ml-analytics" } }
            }
        );
        await AssertNotRejectedByValidator(response);
    }

    [Fact]
    public async Task CreateWorkflow_ValidRequest_IsNotRejectedByValidator()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/workflows",
            new
            {
                Name = "Integration test workflow",
                Steps = new[] { new { JobType = "ml-analytics" } }
            }
        );
        await AssertNotRejectedByValidator(response);
    }
}
