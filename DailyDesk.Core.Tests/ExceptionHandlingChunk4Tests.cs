using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DailyDesk.Core.Tests;

/// <summary>
/// Unit tests verifying that CONVENTIONS.md chunk4 (Pattern 4) exception handling produces
/// correct response shapes for all three exception paths:
///   1. <c>catch (ArgumentException ex)</c> → 400 Bad Request with <c>{ error: message }</c>
///   2. <c>catch (InvalidOperationException ex)</c> → 400 Bad Request with <c>{ error: message }</c>
///   3. <c>catch (Exception ex)</c> → 500 Problem Details with static generic <c>detail</c>
///
/// Groups 1–3 use a minimal in-process <see cref="WebApplication"/> that mirrors the broker's
/// Pattern 4 structure, isolating response-shape and logging assertions from external service
/// availability.
///
/// Group 4 tests verify the convention against the real broker via
/// <see cref="BrokerWebApplicationFactory"/> for endpoints whose ArgumentException and
/// InvalidOperationException paths are reliably exercisable without external services.
/// </summary>
[Collection("BrokerIntegrationTests")]
public sealed class ExceptionHandlingChunk4Tests : IClassFixture<BrokerWebApplicationFactory>
{
    private const string StaticDetail =
        "An unexpected error occurred. See server logs for details.";

    private const string SimulatedArgumentMessage = "Simulated argument validation failure";
    private const string SimulatedInvalidOpMessage = "Simulated invalid operation state";
    private const string SimulatedUnhandledMessage = "Simulated unhandled server error";

    private readonly HttpClient _client;

    public ExceptionHandlingChunk4Tests(BrokerWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -----------------------------------------------------------------------
    // Group 1: ArgumentException → 400 response shape
    // Verifies that broker endpoints implementing CONVENTIONS.md Pattern 4
    // return 400 with { error: message } when ArgumentException escapes the
    // service layer.  The exception message IS returned (user-authored, safe
    // to expose) per the convention.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ArgumentException_Get_Returns400StatusCode()
    {
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/test-arg-get");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ArgumentException_Post_Returns400StatusCode()
    {
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-arg-post", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ArgumentException_Post_ResponseHasSingleErrorField()
    {
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-arg-post", new { });

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(
            json!.ContainsKey("error"),
            "Pattern 4: ArgumentException must produce response with single 'error' field"
        );
    }

    [Fact]
    public async Task ArgumentException_Post_ErrorFieldContainsExceptionMessage()
    {
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-arg-post", new { });

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        var errorValue = json!["error"]!.GetValue<string>();
        Assert.Contains(
            SimulatedArgumentMessage,
            errorValue,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task ArgumentException_Post_ResponseDoesNotUseErrorsArray()
    {
        // CONVENTIONS.md Pattern 4 returns { error: message } (singular) for ArgumentException.
        // Only FluentValidation uses the { errors: [...] } array shape.
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-arg-post", new { });

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.False(
            json!.ContainsKey("errors"),
            "ArgumentException must not produce an 'errors' array; use singular 'error' field"
        );
    }

    [Fact]
    public async Task ArgumentException_Post_ResponseDoesNotHaveRfc7807Fields()
    {
        // 400 responses from ArgumentException are plain JSON { error: message }.
        // They must NOT be RFC 7807 Problem Details (which are reserved for 500 responses).
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-arg-post", new { });

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.False(
            json!.ContainsKey("status"),
            "400 ArgumentException response must not contain RFC 7807 'status' field"
        );
        Assert.False(
            json.ContainsKey("title"),
            "400 ArgumentException response must not contain RFC 7807 'title' field"
        );
        Assert.False(
            json.ContainsKey("detail"),
            "400 ArgumentException response must not contain RFC 7807 'detail' field"
        );
    }

    // -----------------------------------------------------------------------
    // Group 2: InvalidOperationException → 400 response shape
    // Verifies that InvalidOperationException (state validation error per
    // CONVENTIONS.md Pattern 3) produces a 400 Bad Request with { error: message }
    // when caught by the Pattern 4 endpoint handler.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InvalidOperationException_Post_Returns400StatusCode()
    {
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-invalid-op", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidOperationException_Post_ResponseHasSingleErrorField()
    {
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-invalid-op", new { });

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(
            json!.ContainsKey("error"),
            "Pattern 4: InvalidOperationException must produce response with single 'error' field"
        );
    }

    [Fact]
    public async Task InvalidOperationException_Post_ErrorFieldContainsExceptionMessage()
    {
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-invalid-op", new { });

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        var errorValue = json!["error"]!.GetValue<string>();
        Assert.Contains(
            SimulatedInvalidOpMessage,
            errorValue,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task InvalidOperationException_Post_ResponseDoesNotUseErrorsArray()
    {
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-invalid-op", new { });

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.False(
            json!.ContainsKey("errors"),
            "InvalidOperationException must not produce an 'errors' array"
        );
    }

    // -----------------------------------------------------------------------
    // Group 3: Asymmetry – user-authored exceptions expose message;
    //           unhandled exceptions use the static generic string.
    // Verifies the key CONVENTIONS.md invariant: the same endpoint (with
    // both specific and general catch blocks) routes exceptions correctly.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnhandledException_Returns500WithStaticDetail_WhileArgumentException_Returns400WithMessage()
    {
        await using var app = await BuildPatternFourAppAsync();
        using var client = app.GetTestClient();

        // ArgumentException path → 400, dynamic message exposed
        var argResponse = await client.PostAsJsonAsync("/test-arg-post", new { });
        Assert.Equal(HttpStatusCode.BadRequest, argResponse.StatusCode);
        var argJson = await argResponse.Content.ReadFromJsonAsync<JsonObject>();
        var argError = argJson!["error"]!.GetValue<string>();
        Assert.Contains(SimulatedArgumentMessage, argError, StringComparison.OrdinalIgnoreCase);

        // General Exception path → 500, static detail (dynamic message hidden)
        var exResponse = await client.PostAsJsonAsync("/test-unhandled", new { });
        Assert.Equal(HttpStatusCode.InternalServerError, exResponse.StatusCode);
        var exJson = await exResponse.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(StaticDetail, exJson!["detail"]!.GetValue<string>());

        // Unhandled exception message must not appear in the 500 response body
        var exBody = await exResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SimulatedUnhandledMessage, exBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArgumentException_DoesNotTriggerLogError_UnhandledException_Does()
    {
        // CONVENTIONS.md Pattern 4: only the catch (Exception ex) block calls LogError.
        // ArgumentException and InvalidOperationException are user-authored errors and
        // must not produce error-level log entries that would pollute the server error log.
        var captured = new CapturedLogs();
        await using var app = await BuildPatternFourAppAsync(captured);
        using var client = app.GetTestClient();

        // Trigger ArgumentException path – must NOT produce a LogError entry.
        await client.PostAsJsonAsync("/test-arg-post", new { });
        Assert.DoesNotContain(
            captured.Entries,
            e => e.Level == LogLevel.Error
        );

        // Trigger unhandled Exception path – MUST produce a LogError entry with the exception.
        await client.PostAsJsonAsync("/test-unhandled", new { });
        Assert.Contains(
            captured.Entries,
            e =>
                e.Level == LogLevel.Error
                && e.Exception is InvalidOperationException ex
                && ex.Message == SimulatedUnhandledMessage
        );
    }

    [Fact]
    public async Task InvalidOperationException_DoesNotTriggerLogError_UnhandledException_Does()
    {
        // Same logging-asymmetry check for InvalidOperationException: it is a state-validation
        // error per CONVENTIONS.md Pattern 3 and must not log at Error level.
        var captured = new CapturedLogs();
        await using var app = await BuildPatternFourAppAsync(captured);
        using var client = app.GetTestClient();

        // Trigger InvalidOperationException path – must NOT produce a LogError entry.
        await client.PostAsJsonAsync("/test-invalid-op", new { });
        Assert.DoesNotContain(
            captured.Entries,
            e => e.Level == LogLevel.Error
        );

        // Trigger unhandled Exception path – MUST produce a LogError entry.
        await client.PostAsJsonAsync("/test-unhandled", new { });
        Assert.Contains(
            captured.Entries,
            e => e.Level == LogLevel.Error
        );
    }

    // -----------------------------------------------------------------------
    // Group 4: Real broker endpoints – ArgumentException / InvalidOperationException paths.
    // These endpoints have no FluentValidation guard so the service-layer exceptions
    // reach the Pattern 4 catch blocks directly.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BrokerInboxQueue_EmptySuggestionId_Returns400WithFluentValidationErrors()
    {
        // POST /api/inbox/queue now has FluentValidation via InboxQueueRequestValidator.
        // An empty SuggestionId is caught by the validator before reaching the orchestrator,
        // returning 400 with { errors: ["SuggestionId is required."] }.
        var response = await _client.PostAsJsonAsync(
            "/api/inbox/queue",
            new { SuggestionId = "" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(
            json!.ContainsKey("errors"),
            "FluentValidation must produce { errors: [...] } with plural 'errors' field"
        );
        Assert.False(
            json.ContainsKey("error"),
            "FluentValidation response must not use the singular 'error' field"
        );
    }

    [Fact]
    public async Task BrokerResearchSave_NoActiveResearch_Returns400WithSingleErrorField()
    {
        // POST /api/research/save has no FluentValidation.  With no active research in the
        // store, OfficeBrokerOrchestrator.SaveResearchReportAsync throws
        // InvalidOperationException("No live research report is available to save.").
        var response = await _client.PostAsJsonAsync("/api/research/save", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(
            json!.ContainsKey("error"),
            "InvalidOperationException from orchestrator must produce { error: message } with singular 'error' field"
        );
        Assert.False(
            json.ContainsKey("errors"),
            "InvalidOperationException must not produce the FluentValidation 'errors' array"
        );
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal in-process <see cref="WebApplication"/> that reproduces
    /// CONVENTIONS.md Pattern 4 on four endpoints, covering all three exception paths:
    /// <list type="bullet">
    ///   <item><c>GET  /test-arg-get</c>   – ArgumentException (→ 400 via specific catch)</item>
    ///   <item><c>POST /test-arg-post</c>  – ArgumentException (→ 400 via specific catch)</item>
    ///   <item><c>POST /test-invalid-op</c> – InvalidOperationException (→ 400 via specific catch)</item>
    ///   <item><c>POST /test-unhandled</c> – general Exception (→ 500 via general catch)</item>
    /// </list>
    /// </summary>
    private static async Task<WebApplication> BuildPatternFourAppAsync(
        CapturedLogs? captured = null
    )
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        if (captured is not null)
            builder.Logging.AddProvider(new CapturingLoggerProvider(captured));

        var app = builder.Build();
        var logger = app.Logger;

        // GET – ArgumentException only (mirrors simple read-only broker endpoints)
        app.MapGet(
            "/test-arg-get",
            () =>
            {
                try
                {
                    throw new ArgumentException(SimulatedArgumentMessage);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Test GET endpoint failed.");
                    return Results.Problem(
                        detail: StaticDetail,
                        title: "Test GET endpoint failed",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        );

        // POST – ArgumentException + general Exception (mirrors write/action broker endpoints)
        app.MapPost(
            "/test-arg-post",
            () =>
            {
                try
                {
                    throw new ArgumentException(SimulatedArgumentMessage, "param");
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Test POST endpoint failed.");
                    return Results.Problem(
                        detail: StaticDetail,
                        title: "Test POST endpoint failed",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        );

        // POST – InvalidOperationException + general Exception
        app.MapPost(
            "/test-invalid-op",
            () =>
            {
                try
                {
                    throw new InvalidOperationException(SimulatedInvalidOpMessage);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Test POST invalid-op endpoint failed.");
                    return Results.Problem(
                        detail: StaticDetail,
                        title: "Test POST invalid-op endpoint failed",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        );

        // POST – only the general Exception catch block (no specific handler for the thrown type)
        app.MapPost(
            "/test-unhandled",
            () =>
            {
                try
                {
                    throw new InvalidOperationException(SimulatedUnhandledMessage);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Test POST unhandled endpoint failed.");
                    return Results.Problem(
                        detail: StaticDetail,
                        title: "Test POST unhandled endpoint failed",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        );

        await app.StartAsync();
        return app;
    }
}
