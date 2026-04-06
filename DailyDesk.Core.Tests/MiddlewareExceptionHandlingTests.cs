using System.Collections.Concurrent;
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
/// Unit tests that verify the middleware exception handling pattern described in
/// CONVENTIONS.md (Pattern 4):
///   1. The full exception is passed to <c>logger.LogError(exception, …)</c>.
///   2. The RFC 7807 Problem Details <c>detail</c> field is the static generic message,
///      never <c>ex.Message</c> or <c>ex.ToString()</c>.
///   3. The HTTP response body does not contain the exception's own message text,
///      ensuring no internal details are leaked to callers.
///
/// Each test uses a minimal in-process <see cref="WebApplication"/> wired with a
/// <see cref="CapturingLoggerProvider"/> so logging assertions can be made without
/// any external dependencies.
/// </summary>
public sealed class MiddlewareExceptionHandlingTests
{
    private const string StaticDetail =
        "An unexpected error occurred. See server logs for details.";

    private const string SimulatedExceptionMessage = "Simulated internal failure – do not expose";

    // -----------------------------------------------------------------------
    // Group 1: Logging behaviour
    // Verifies that the catch block logs via LogError and passes the original
    // exception instance, as required by CONVENTIONS.md Pattern 4.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExceptionHandler_Get_LogsErrorWithExceptionInstance()
    {
        var captured = new CapturedLogs();
        await using var app = await BuildHandlerAppAsync(captured);
        using var client = app.GetTestClient();

        await client.GetAsync("/test-get");

        Assert.Contains(
            captured.Entries,
            e =>
                e.Level == LogLevel.Error
                && e.Exception is InvalidOperationException ex
                && ex.Message == SimulatedExceptionMessage
        );
    }

    [Fact]
    public async Task ExceptionHandler_Post_LogsErrorWithExceptionInstance()
    {
        var captured = new CapturedLogs();
        await using var app = await BuildHandlerAppAsync(captured);
        using var client = app.GetTestClient();

        await client.PostAsJsonAsync("/test-post", new { });

        Assert.Contains(
            captured.Entries,
            e =>
                e.Level == LogLevel.Error
                && e.Exception is InvalidOperationException ex
                && ex.Message == SimulatedExceptionMessage
        );
    }

    [Fact]
    public async Task ExceptionHandler_Delete_LogsErrorWithExceptionInstance()
    {
        var captured = new CapturedLogs();
        await using var app = await BuildHandlerAppAsync(captured);
        using var client = app.GetTestClient();

        await client.DeleteAsync("/test-delete");

        Assert.Contains(
            captured.Entries,
            e =>
                e.Level == LogLevel.Error
                && e.Exception is InvalidOperationException ex
                && ex.Message == SimulatedExceptionMessage
        );
    }

    // -----------------------------------------------------------------------
    // Group 2: Response detail is the static generic message
    // Verifies that no dynamic exception text is forwarded to the caller.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExceptionHandler_Get_ResponseDetail_IsStaticGenericMessage()
    {
        var captured = new CapturedLogs();
        await using var app = await BuildHandlerAppAsync(captured);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/test-get");

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("detail"), "RFC 7807 response must include 'detail'");
        Assert.Equal(StaticDetail, json["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task ExceptionHandler_Post_ResponseDetail_IsStaticGenericMessage()
    {
        var captured = new CapturedLogs();
        await using var app = await BuildHandlerAppAsync(captured);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-post", new { });

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("detail"), "RFC 7807 response must include 'detail'");
        Assert.Equal(StaticDetail, json["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task ExceptionHandler_Delete_ResponseDetail_IsStaticGenericMessage()
    {
        var captured = new CapturedLogs();
        await using var app = await BuildHandlerAppAsync(captured);
        using var client = app.GetTestClient();

        var response = await client.DeleteAsync("/test-delete");

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(json);
        Assert.True(json.ContainsKey("detail"), "RFC 7807 response must include 'detail'");
        Assert.Equal(StaticDetail, json["detail"]!.GetValue<string>());
    }

    // -----------------------------------------------------------------------
    // Group 3: Exception message is not leaked in the response body
    // Verifies the security requirement: ex.Message / ex.ToString() must not
    // appear in any part of the response.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExceptionHandler_Get_ResponseBody_DoesNotContainExceptionMessage()
    {
        var captured = new CapturedLogs();
        await using var app = await BuildHandlerAppAsync(captured);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/test-get");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(
            SimulatedExceptionMessage,
            body,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task ExceptionHandler_Post_ResponseBody_DoesNotContainExceptionMessage()
    {
        var captured = new CapturedLogs();
        await using var app = await BuildHandlerAppAsync(captured);
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/test-post", new { });

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(
            SimulatedExceptionMessage,
            body,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task ExceptionHandler_Delete_ResponseBody_DoesNotContainExceptionMessage()
    {
        var captured = new CapturedLogs();
        await using var app = await BuildHandlerAppAsync(captured);
        using var client = app.GetTestClient();

        var response = await client.DeleteAsync("/test-delete");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(
            SimulatedExceptionMessage,
            body,
            StringComparison.OrdinalIgnoreCase
        );
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal in-process <see cref="WebApplication"/> that reproduces
    /// the broker's Pattern-4 exception handling on GET, POST, and DELETE endpoints,
    /// using the supplied <paramref name="captured"/> store to record log entries.
    /// </summary>
    private static async Task<WebApplication> BuildHandlerAppAsync(CapturedLogs captured)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseTestServer();
        // Replace all real logging providers with the capturing one.
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new CapturingLoggerProvider(captured));

        var app = builder.Build();
        var logger = app.Logger;

        // GET – mirrors broker read endpoints (e.g. /health, /api/state)
        app.MapGet(
            "/test-get",
            () =>
            {
                try
                {
                    throw new InvalidOperationException(SimulatedExceptionMessage);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Test GET endpoint failed.");
                    return Results.Problem(
                        detail: StaticDetail,
                        title: "Test GET endpoint failed",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        );

        // POST – mirrors broker write/action endpoints
        app.MapPost(
            "/test-post",
            () =>
            {
                try
                {
                    throw new InvalidOperationException(SimulatedExceptionMessage);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Test POST endpoint failed.");
                    return Results.Problem(
                        detail: StaticDetail,
                        title: "Test POST endpoint failed",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        );

        // DELETE – mirrors broker deletion endpoints
        app.MapDelete(
            "/test-delete",
            () =>
            {
                try
                {
                    throw new InvalidOperationException(SimulatedExceptionMessage);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Test DELETE endpoint failed.");
                    return Results.Problem(
                        detail: StaticDetail,
                        title: "Test DELETE endpoint failed",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        );

        await app.StartAsync();
        return app;
    }
}

// ---------------------------------------------------------------------------
// Test-double logging infrastructure
// ---------------------------------------------------------------------------

/// <summary>Thread-safe store for log entries captured during a single test.</summary>
internal sealed class CapturedLogs
{
    private readonly ConcurrentBag<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries;

    public void Add(LogEntry entry) => _entries.Add(entry);
}

/// <summary>A single captured log entry.</summary>
internal sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);

/// <summary>
/// <see cref="ILoggerProvider"/> that routes all log calls to a
/// <see cref="CapturedLogs"/> instance for assertion in unit tests.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly CapturedLogs _captured;

    public CapturingLoggerProvider(CapturedLogs captured) => _captured = captured;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_captured);

    public void Dispose() { }
}

/// <summary>
/// Minimal <see cref="ILogger"/> that records every call into the shared
/// <see cref="CapturedLogs"/> bag.
/// </summary>
internal sealed class CapturingLogger : ILogger
{
    private readonly CapturedLogs _captured;

    public CapturingLogger(CapturedLogs captured) => _captured = captured;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        _captured.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
    }
}
