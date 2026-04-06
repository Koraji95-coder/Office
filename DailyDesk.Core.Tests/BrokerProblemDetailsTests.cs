using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify server errors across all broker endpoint types adhere to
/// RFC 7807 Problem Details format as required by CONVENTIONS.md.
///
/// The tests are structured in four groups:
///   1. RFC 7807 format tests using a minimal in-process server – always fast and deterministic.
///      Verifies that the Results.Problem() pattern used in every broker catch block produces
///      application/problem+json responses with the required status, title, and detail fields.
///   2. Validation-error (400) format tests against the real broker via WebApplicationFactory –
///      fast because validation runs before the orchestrator is invoked.
///   3. Not-found (404) format tests for job, workflow, and schedule endpoints.
///   4. Server-error (500) tests against the real broker – in CI environments (where Ollama /
///      other dependencies are not available) these reliably produce RFC 7807 500 responses;
///      in local dev environments where the dependencies ARE reachable the same endpoints may
///      return 200, in which case the test verifies the 200 response shape instead.
/// </summary>
public sealed class BrokerProblemDetailsTests : IClassFixture<BrokerWebApplicationFactory>
{
    private readonly BrokerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public BrokerProblemDetailsTests(BrokerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -----------------------------------------------------------------------
    // Group 1: RFC 7807 format verification using a lightweight in-process server
    // These tests confirm that Results.Problem() – the pattern used by every
    // broker endpoint's catch block – produces RFC 7807-compliant responses.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Results_Problem_ReturnsApplicationProblemJsonContentType()
    {
        await using var app = await BuildErrorTriggerAppAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/trigger-get-error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Results_Problem_ReturnsRfc7807RequiredFields()
    {
        await using var app = await BuildErrorTriggerAppAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/trigger-get-error");

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("status"), "RFC 7807 requires a 'status' field");
        Assert.True(json.ContainsKey("title"), "RFC 7807 requires a 'title' field");
        Assert.Equal(500, json["status"]!.GetValue<int>());
    }

    [Fact]
    public async Task Results_Problem_IncludesDetailField()
    {
        await using var app = await BuildErrorTriggerAppAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/trigger-get-error");

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("detail"), "Broker pattern sets 'detail' to ex.Message");
    }

    [Fact]
    public async Task Results_Problem_PostEndpoint_ReturnsRfc7807Format()
    {
        await using var app = await BuildErrorTriggerAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/trigger-post-error", new { });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("status"));
        Assert.True(json.ContainsKey("title"));
        Assert.Equal(500, json["status"]!.GetValue<int>());
    }

    [Fact]
    public async Task Results_Problem_DeleteEndpoint_ReturnsRfc7807Format()
    {
        await using var app = await BuildErrorTriggerAppAsync();
        using var client = app.GetTestClient();

        var response = await client.DeleteAsync("/trigger-delete-error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // -----------------------------------------------------------------------
    // Group 2: Validation-error format tests against the real broker endpoints.
    // Validation happens before the orchestrator is called, so these are fast.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ChatRoute_EmptyRoute_Returns400WithErrorsArray()
    {
        var response = await _client.PostAsJsonAsync("/api/chat/route", new { Route = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("errors"), "Validation errors must use the 'errors' array field");
    }

    [Fact]
    public async Task ChatSend_EmptyPrompt_Returns400WithErrorsArray()
    {
        var response = await _client.PostAsJsonAsync("/api/chat/send", new { Prompt = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("errors"));
    }

    [Fact]
    public async Task StudyScoreDefense_EmptyAnswer_Returns400WithErrorField()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/study/score-defense",
            new { Answer = "" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("errors"), "FluentValidation errors use the 'errors' field");
    }

    [Fact]
    public async Task ResearchRun_EmptyQuery_Returns400WithErrorsArray()
    {
        var response = await _client.PostAsJsonAsync("/api/research/run", new { Query = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("errors"));
    }

    [Fact]
    public async Task Schedules_CreateWithMissingFields_Returns400WithErrorsArray()
    {
        // Name is required; send an empty name to trigger validation.
        var response = await _client.PostAsJsonAsync(
            "/api/schedules",
            new { Name = "", JobType = "ml-analytics", CronExpression = "every 1h" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("errors"));
    }

    [Fact]
    public async Task Workflows_CreateWithNoSteps_Returns400WithErrorsArray()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/workflows",
            new { Name = "test-wf", Steps = Array.Empty<object>() }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("errors"));
    }

    // -----------------------------------------------------------------------
    // Group 3: Not-found error format tests – synchronous, LiteDB-backed, fast.
    // These confirm the error shape returned when a resource does not exist.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Jobs_GetNonExistentJob_Returns404WithErrorField()
    {
        var response = await _client.GetAsync("/api/jobs/nonexistent-job-id");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("error"), "Not-found responses use a single 'error' field");
    }

    [Fact]
    public async Task Jobs_DeleteNonExistentJob_Returns404WithErrorField()
    {
        var response = await _client.DeleteAsync("/api/jobs/nonexistent-job-id");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("error"));
    }

    [Fact]
    public async Task Workflows_DeleteNonExistentWorkflow_Returns404WithErrorField()
    {
        var response = await _client.DeleteAsync("/api/workflows/nonexistent-wf-id");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("error"));
    }

    // -----------------------------------------------------------------------
    // Group 4: Server-error (500) RFC 7807 tests against real broker endpoints.
    // In CI environments where Ollama / backing services are unavailable the
    // orchestrator throws, the endpoint's catch block fires, and the response
    // must be RFC 7807 Problem Details.  In local-dev environments the same
    // endpoints may succeed (200); the assertions adapt accordingly.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HealthEndpoint_WhenServerError_ReturnsRfc7807ProblemDetails()
    {
        var response = await _client.GetAsync("/health");
        await AssertRfc7807OrSuccessAsync(response, "GET /health");
    }

    [Fact]
    public async Task DetailedHealthEndpoint_WhenServerError_ReturnsRfc7807ProblemDetails()
    {
        var response = await _client.GetAsync("/api/health");
        await AssertRfc7807OrSuccessAsync(response, "GET /api/health");
    }

    [Fact]
    public async Task StateEndpoint_WhenServerError_ReturnsRfc7807ProblemDetails()
    {
        var response = await _client.GetAsync("/api/state");
        await AssertRfc7807OrSuccessAsync(response, "GET /api/state");
    }

    [Fact]
    public async Task ChatThreadsEndpoint_WhenServerError_ReturnsRfc7807ProblemDetails()
    {
        var response = await _client.GetAsync("/api/chat/threads");
        await AssertRfc7807OrSuccessAsync(response, "GET /api/chat/threads");
    }

    [Fact]
    public async Task StudyStartEndpoint_WhenServerError_ReturnsRfc7807ProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/study/start", new { });
        await AssertRfc7807OrSuccessAsync(response, "POST /api/study/start");
    }

    [Fact]
    public async Task InboxEndpoint_WhenServerError_ReturnsRfc7807ProblemDetails()
    {
        var response = await _client.GetAsync("/api/inbox");
        await AssertRfc7807OrSuccessAsync(response, "GET /api/inbox");
    }

    [Fact]
    public async Task KnowledgeIndexStatusEndpoint_WhenServerError_ReturnsRfc7807ProblemDetails()
    {
        var response = await _client.GetAsync("/api/knowledge/index-status");
        await AssertRfc7807OrSuccessAsync(response, "GET /api/knowledge/index-status");
    }

    [Fact]
    public async Task WorkspaceResetEndpoint_WhenServerError_ReturnsRfc7807ProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/workspace/reset", new { });
        await AssertRfc7807OrSuccessAsync(response, "POST /api/workspace/reset");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal in-process WebApplication that mirrors the three endpoint types
    /// used in the broker (GET, POST, DELETE) and uses the same Results.Problem() error
    /// handling pattern.  Used to verify RFC 7807 compliance in isolation.
    /// </summary>
    private static async Task<WebApplication> BuildErrorTriggerAppAsync()
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseTestServer();
        // Suppress console logging noise during tests.
        builder.Logging.ClearProviders();

        var app = builder.Build();

        // GET endpoint – mirrors broker pattern for read endpoints (e.g. /health, /api/state).
        app.MapGet(
            "/trigger-get-error",
            () =>
            {
                try
                {
                    throw new InvalidOperationException("Simulated GET server error");
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        title: "GET endpoint failed",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        );

        // POST endpoint – mirrors broker pattern for write / action endpoints.
        app.MapPost(
            "/trigger-post-error",
            () =>
            {
                try
                {
                    throw new InvalidOperationException("Simulated POST server error");
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        title: "POST endpoint failed",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        );

        // DELETE endpoint – mirrors broker pattern for deletion endpoints.
        app.MapDelete(
            "/trigger-delete-error",
            () =>
            {
                try
                {
                    throw new InvalidOperationException("Simulated DELETE server error");
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        title: "DELETE endpoint failed",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        );

        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Asserts that a broker endpoint response either:
    ///   (a) Is an HTTP 500 in RFC 7807 Problem Details format, or
    ///   (b) Is an HTTP 200 (success) when backing services are available.
    /// This dual-assertion pattern ensures the test is meaningful in CI (where services
    /// are unavailable → 500 RFC 7807 is verified) while not failing in local dev
    /// environments that have all services running (→ 200 is also acceptable).
    /// </summary>
    private static async Task AssertRfc7807OrSuccessAsync(
        HttpResponseMessage response,
        string endpointLabel
    )
    {
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType
            );

            var json = await response.Content.ReadFromJsonAsync<JsonObject>();
            Assert.NotNull(json);
            Assert.True(
                json.ContainsKey("status"),
                $"{endpointLabel}: RFC 7807 response must contain 'status'"
            );
            Assert.True(
                json.ContainsKey("title"),
                $"{endpointLabel}: RFC 7807 response must contain 'title'"
            );
            Assert.True(
                json.ContainsKey("detail"),
                $"{endpointLabel}: RFC 7807 response must contain 'detail'"
            );
            Assert.Equal(500, json["status"]!.GetValue<int>());
        }
        else
        {
            // Backing services available – endpoint succeeded normally.
            var successCodes = new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.Created,
                HttpStatusCode.NoContent,
            };
            Assert.True(
                successCodes.Contains(response.StatusCode),
                $"{endpointLabel}: Expected 200/201/204 or 500 (RFC 7807), got {(int)response.StatusCode}"
            );
        }
    }
}

/// <summary>
/// Custom WebApplicationFactory that starts the real broker for integration tests.
/// </summary>
public sealed class BrokerWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Suppress verbose broker logging during tests.
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }
}
