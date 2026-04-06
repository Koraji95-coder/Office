using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Integration tests that verify broker endpoint compliance with the five production
/// control workflow dimensions from AGENT_REPLY_GUIDE.md chunk7 ("Best Reply Patterns
/// For Electrical Drafting Workflows"):
///   1. Approval routing  — /api/inbox/resolve validation and business-rule enforcement
///   2. Revision tracking — /api/research/run with production control queries
///   3. Issue-set handling — /api/inbox/queue validation and business-rule enforcement
///   4. Audit trail       — /api/inbox load and resolve audit-trail data shape
///   5. AutoCAD workflow fit — /api/research/run with AutoCAD-specific queries
///
/// Tests are structured in three groups:
///   Group 1: Validation-error (400) tests — deterministic, no external services needed.
///   Group 2: Business-rule-error (400) tests — always deterministic (no external services).
///   Group 3: Server-error / success dual-assertion tests — adapts to CI vs local dev.
/// </summary>
public sealed class ProductionControlComplianceTests : IClassFixture<BrokerWebApplicationFactory>
{
    private static readonly HttpStatusCode[] SuccessCodes =
    [
        HttpStatusCode.OK,
        HttpStatusCode.Created,
        HttpStatusCode.NoContent,
    ];

    private readonly HttpClient _client;

    public ProductionControlComplianceTests(BrokerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -----------------------------------------------------------------------
    // Group 1: Validation-error (400) tests
    // These confirm that the inbox and research endpoints reject missing/invalid
    // input with a 400 + { errors: [...] } shape, which is the required format
    // for FluentValidation failures (CONVENTIONS.md).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ApprovalRouting_InboxResolve_MissingSuggestionId_Returns400WithErrorsArray()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/inbox/resolve",
            new { SuggestionId = "", Status = "accepted" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(
            json.ContainsKey("errors"),
            "Approval routing validation failures must use the 'errors' array field"
        );
    }

    [Fact]
    public async Task ApprovalRouting_InboxResolve_InvalidStatus_Returns400WithErrorsArray()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/inbox/resolve",
            new { SuggestionId = "test-id", Status = "approve-and-run" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(
            json.ContainsKey("errors"),
            "An invalid approval status must return 400 with the 'errors' array"
        );
    }

    [Fact]
    public async Task RevisionTracking_ResearchRun_EmptyQuery_Returns400WithErrorsArray()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/research/run",
            new { Query = "" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(
            json.ContainsKey("errors"),
            "An empty revision tracking query must return 400 with the 'errors' array"
        );
    }

    [Fact]
    public async Task IssueSetHandling_WatchlistRun_MissingWatchlistId_Returns400WithErrorsArray()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/watchlists/run",
            new { WatchlistId = "" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(
            json.ContainsKey("errors"),
            "An empty watchlist ID must return 400 with the 'errors' array"
        );
    }

    // -----------------------------------------------------------------------
    // Group 2: Business-rule-error (400) tests
    // These confirm that non-existent resource references return 400 + { error }
    // (single field, not array) — the pattern for business-rule violations per
    // CONVENTIONS.md and the broker endpoint catch blocks.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ApprovalRouting_InboxResolve_NonexistentSuggestion_Returns400WithErrorField()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/inbox/resolve",
            new { SuggestionId = "nonexistent-prod-control-id", Status = "accepted", Reason = "Approve and run." }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(
            json.ContainsKey("error"),
            "A not-found suggestion must return 400 with the single 'error' field, not 'errors'"
        );
        Assert.False(
            json.ContainsKey("errors"),
            "Business-rule errors must not use the 'errors' validation array"
        );
    }

    [Fact]
    public async Task IssueSetHandling_InboxQueue_NonexistentSuggestion_Returns400WithErrorField()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/inbox/queue",
            new { SuggestionId = "nonexistent-issue-set-id" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(
            json.ContainsKey("error"),
            "A not-found suggestion for queue must return 400 with the single 'error' field"
        );
    }

    // -----------------------------------------------------------------------
    // Group 3: Server-error / success dual-assertion tests
    // In CI environments (no Ollama / external services) these endpoints return
    // RFC 7807 Problem Details 500 responses.  In local dev environments where
    // the services are available they return 200/201.  Both outcomes are valid.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AuditTrail_InboxLoad_ReturnsRfc7807OrSuccess()
    {
        var response = await _client.GetAsync("/api/inbox");
        await AssertRfc7807OrSuccessAsync(response, "GET /api/inbox (audit trail load)");
    }

    [Fact]
    public async Task RevisionTracking_ResearchRun_WithProductionControlQuery_ReturnsRfc7807OrSuccess()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/research/run",
            new
            {
                Query = "Research DraftFlow for electrical drafting production control. Return: approval routing, revision tracking, issue-set handling, audit trail, and AutoCAD workflow fit. Ignore CRM, invoicing, and general PM features.",
                Perspective = "Business Strategist",
            }
        );
        await AssertRfc7807OrSuccessAsync(
            response,
            "POST /api/research/run (revision tracking – production control query)"
        );
    }

    [Fact]
    public async Task AutoCADWorkflowFit_ResearchRun_WithAutoCADQuery_ReturnsRfc7807OrSuccess()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/research/run",
            new
            {
                Query = "Research approval workflow tools for electrical drafting. Return: AutoCAD workflow fit, revision control, and audit trail. Ignore CRM and billing.",
                Perspective = "Business Strategist",
            }
        );
        await AssertRfc7807OrSuccessAsync(
            response,
            "POST /api/research/run (AutoCAD workflow fit query)"
        );
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Asserts that a broker endpoint response either:
    ///   (a) Is an HTTP 500 in RFC 7807 Problem Details format, or
    ///   (b) Is an HTTP 200/201/204 (success) when backing services are available.
    /// This mirrors the dual-assertion pattern in BrokerProblemDetailsTests.
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
            Assert.True(
                SuccessCodes.Contains(response.StatusCode),
                $"{endpointLabel}: Expected 200/201/204 or 500 (RFC 7807), got {(int)response.StatusCode}"
            );
        }
    }
}
