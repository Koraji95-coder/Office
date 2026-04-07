using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration compliance tests that verify every broker endpoint protected by a
/// <c>Results.Problem()</c> catch block returns the static generic detail string required by
/// <c>Docs/CONVENTIONS.md</c> §Error Handling (Pattern 4) and
/// <c>Docs/stack-trace-exposure-remediation.md</c>.
///
/// The static detail policy states:
///   - The <c>detail</c> field of any HTTP 500 <c>Results.Problem()</c> response must be the
///     literal string "An unexpected error occurred. See server logs for details."
///   - The response body must never contain <c>exception.Message</c>, stack trace text, or
///     any other dynamic runtime information.
///   - The full exception is preserved server-side via <c>logger.LogError(exception, …)</c>.
///
/// For each of the 29 endpoints listed in <see cref="AllEndpointsWithResultsProblem"/>, this
/// test class:
///   (a) Sends a minimally-valid request so that FluentValidation 400s are avoided where
///       possible, allowing the request to reach the orchestrator layer.
///   (b) If the response is HTTP 500, asserts the <c>detail</c> field equals the static
///       generic string and that no exception text appears in the response body.
///   (c) If the response is not HTTP 500 (200/201/400/404), accepts it as valid – validation
///       errors and successful responses are both compliant with the policy.
/// </summary>
[Collection("BrokerIntegrationTests")]
public sealed class StaticErrorMessageComplianceTests : IClassFixture<BrokerWebApplicationFactory>
{
    /// <summary>
    /// The exact static detail string required by CONVENTIONS.md Pattern 4 and
    /// Docs/stack-trace-exposure-remediation.md for all HTTP 500 Results.Problem() responses.
    /// </summary>
    private const string ExpectedStaticDetail =
        "An unexpected error occurred. See server logs for details.";

    private readonly HttpClient _client;

    public StaticErrorMessageComplianceTests(BrokerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// All broker endpoints that have a <c>Results.Problem()</c> catch block as documented
    /// in <c>Docs/stack-trace-exposure-remediation.md</c> and CONVENTIONS.md Pattern 4.
    /// Each row contains:
    ///   [0] HTTP method (string)
    ///   [1] Request path (string)
    ///   [2] Optional JSON request body (string?, null for GET/DELETE endpoints)
    /// </summary>
    public static IEnumerable<object?[]> AllEndpointsWithResultsProblem =>
    [
        // --- Health / State ---
        new object?[] { "GET",  "/health",                         null },
        new object?[] { "GET",  "/api/health",                     null },
        new object?[] { "GET",  "/api/state",                      null },

        // --- Chat ---
        new object?[] { "GET",  "/api/chat/threads",               null },
        new object?[] { "POST", "/api/chat/route",                 """{"Route":"ollama"}""" },
        new object?[] { "POST", "/api/chat/send",                  """{"Prompt":"compliance test prompt"}""" },

        // --- Study ---
        new object?[] { "POST", "/api/study/start",                "{}" },
        new object?[] { "POST", "/api/study/generate-practice",    "{}" },
        new object?[] { "POST", "/api/study/score-practice",       "{}" },
        new object?[] { "POST", "/api/study/generate-defense",     "{}" },
        new object?[] { "POST", "/api/study/score-defense",        """{"Answer":"compliance test answer"}""" },
        new object?[] { "POST", "/api/study/save-reflection",      """{"Reflection":"compliance test reflection"}""" },

        // --- Research ---
        new object?[] { "POST", "/api/research/run",               """{"Query":"compliance test query"}""" },
        new object?[] { "POST", "/api/research/save",              "{}" },

        // --- Watchlists ---
        new object?[] { "POST", "/api/watchlists/run",             """{"WatchlistId":"test-watchlist-id"}""" },

        // --- Inbox ---
        new object?[] { "GET",  "/api/inbox",                      null },
        new object?[] { "POST", "/api/inbox/resolve",              "{}" },
        new object?[] { "POST", "/api/inbox/queue",                "{}" },

        // --- Library ---
        new object?[] { "POST", "/api/library/import",             "{}" },

        // --- History / Workspace ---
        new object?[] { "POST", "/api/history/reset",              "{}" },
        new object?[] { "POST", "/api/workspace/reset",            null },

        // --- ML (sync=true forces synchronous execution so the endpoint blocks until done) ---
        new object?[] { "POST", "/api/ml/analytics?sync=true",     null },
        new object?[] { "POST", "/api/ml/forecast?sync=true",      null },
        new object?[] { "POST", "/api/ml/embeddings?sync=true",    """{"ModelId":"nomic-embed-text"}""" },
        new object?[] { "POST", "/api/ml/pipeline?sync=true",      null },
        new object?[] { "POST", "/api/ml/export-artifacts?sync=true", null },
        new object?[] { "POST", "/api/ml/index-knowledge?sync=true",  null },

        // --- Knowledge ---
        new object?[] { "GET",  "/api/knowledge/index-status",     null },
        new object?[] { "POST", "/api/knowledge/search",           """{"Query":"compliance test search","TopK":5}""" },

        // --- Jobs ---
        new object?[] { "GET",    "/api/jobs",                          null },
        new object?[] { "GET",    "/api/jobs/metrics",                  null },
        new object?[] { "GET",    "/api/jobs/nonexistent-job-id",       null },
        new object?[] { "GET",    "/api/jobs/nonexistent-job-id/result",null },
        new object?[] { "DELETE", "/api/jobs/nonexistent-job-id",       null },

        // --- Schedules ---
        new object?[] { "GET",    "/api/schedules",                                                                         null },
        new object?[] { "POST",   "/api/schedules",                     """{"Name":"s","JobType":"analytics","CronExpression":"0 * * * *"}""" },
        new object?[] { "PUT",    "/api/schedules/nonexistent-sched-id","""{"Name":"updated"}""" },
        new object?[] { "DELETE", "/api/schedules/nonexistent-sched-id",null },

        // --- Daily Run ---
        new object?[] { "GET",    "/api/daily-run/latest",              null },

        // --- Workflows ---
        new object?[] { "GET",    "/api/workflows",                     null },
        new object?[] { "POST",   "/api/workflows",                     """{"Name":"w","Steps":[{"JobType":"analytics"}]}""" },
        new object?[] { "POST",   "/api/workflows/nonexistent-wf-id/run",null },
        new object?[] { "DELETE", "/api/workflows/nonexistent-wf-id",   null },
    ];

    /// <summary>
    /// For every endpoint with a <c>Results.Problem()</c> catch block, confirms that:
    /// <list type="bullet">
    ///   <item>If the endpoint returns HTTP 500, the RFC 7807 <c>detail</c> field equals the
    ///   static generic string mandated by CONVENTIONS.md Pattern 4.</item>
    ///   <item>If the endpoint returns HTTP 500, the response body does not contain raw
    ///   exception text or stack trace lines (no <c>at System.</c>, no <c>Exception:</c>).</item>
    ///   <item>Any non-500 response (200 success, 400 validation error, 404 not found) is
    ///   accepted as compliant — those status codes do not leak dynamic error data.</item>
    /// </list>
    /// </summary>
    [Theory]
    [MemberData(nameof(AllEndpointsWithResultsProblem))]
    public async Task Endpoint_WhenServerError_DetailIsStaticString_NotDynamicExceptionMessage(
        string method,
        string url,
        string? jsonBody
    )
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);

        // Non-500 responses (success, validation error, not-found, etc.) are compliant by
        // definition: they either succeed normally or return user-authored error messages
        // (ArgumentException / InvalidOperationException per CONVENTIONS.md Patterns 2 & 3).
        if (response.StatusCode != HttpStatusCode.InternalServerError)
            return;

        // -----------------------------------------------------------------------
        // HTTP 500 compliance assertions (per CONVENTIONS.md Pattern 4 and
        // Docs/stack-trace-exposure-remediation.md):
        // -----------------------------------------------------------------------

        // (1) Must be RFC 7807 application/problem+json
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType
        );

        var body = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonSerializer.Deserialize<JsonObject>(body);
        Assert.NotNull(json);

        // (2) Must contain a 'detail' field
        Assert.True(
            json!.ContainsKey("detail"),
            $"{method} {url}: HTTP 500 RFC 7807 response must contain a 'detail' field"
        );

        // (3) The 'detail' field must be the static generic string — not exception.Message
        var detail = json["detail"]!.GetValue<string>();
        Assert.True(
            detail == ExpectedStaticDetail,
            $"{method} {url}: 'detail' must be the static generic string per "
                + "stack-trace-exposure-remediation.md, but was: \"{detail}\""
        );

        // (4) Raw exception text and stack trace indicators must not appear in the response body
        Assert.DoesNotContain("at System.", body);
        Assert.DoesNotContain("Exception:", body);
    }
}
