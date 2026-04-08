# Stack Trace & Raw Error Exposure — Remediation Guide

> **Scope:** `DailyDesk.Broker/Program.cs` and any future API endpoint code.  
> **Related:** `Docs/CONVENTIONS.md` § Error Handling, `Docs/CURRENT-STATE.md` § Resilience

---

## Problem

Returning `exception.Message` (or a full stack trace) directly in an HTTP response can
unintentionally expose sensitive internal details to callers:

| Information leaked | Example |
|--------------------|---------|
| Internal file paths | `Could not find file 'C:\Users\...\office.db'` |
| Class / method names | `Object reference not set … in OfficeBrokerOrchestrator.SendChatAsync` |
| Database internals | `LiteDB: Collection 'training_attempts' has no index on field 'threadId'` |
| Connection strings | Driver-level exceptions sometimes embed partial credentials |

Even though the Broker is **localhost-only**, leaking these details:

1. Aids local debugging in ways that bypass intended log-only observability.
2. Sets a bad precedent that is hard to audit when reviewing endpoints.
3. Makes errors opaque to the caller — a generic message is just as useful to a WPF consumer.

---

## Rule

**Never pass `exception.Message` (or `exception.ToString()`) as the `detail` of a
`Results.Problem(...)` 500-level response.**

The full exception — including message *and* stack trace — is already captured by
`logger.LogError(exception, …)` and written to the rolling log file under `State/logs/`.

---

## Safe Pattern (Pattern 4 — Broker Endpoint Catch)

```csharp
catch (ArgumentException ex)
{
    // ArgumentException messages are authored in the orchestrator for user feedback.
    return Results.BadRequest(new { error = ex.Message });
}
catch (InvalidOperationException)
{
    // ⚠ Never return ex.Message — use a static generic string.
    // InvalidOperationException can originate from internal services whose messages
    // may contain runtime details (e.g. Python subprocess output).
    return Results.BadRequest(new { error = "The requested operation could not be completed." });
}
catch (Exception exception)
{
    logger.LogError(exception, "Office broker {Endpoint} endpoint failed.", endpointName);
    return Results.Problem(
        detail: "An unexpected error occurred. See server logs for details.",
        title: "…",
        statusCode: StatusCodes.Status500InternalServerError
    );
}
```

Key points:

* `ArgumentException.Message` is safe to return because these messages are always
  authored in the orchestrator specifically as user-facing strings.
* `InvalidOperationException.Message` must **not** be returned — use a static generic
  string.  The message can originate from internal services (e.g. Python subprocess,
  LiteDB) and may contain runtime details.
* `logger.LogError(exception, …)` preserves the full exception (message + stack trace)
  in the structured log sink — no information is lost.
* The `detail` field returned to the client is a **static generic string**.
* The `title` field may describe the operation that failed (safe — no runtime data).

---

## What Is Still Safe to Return to the Client

| Source | Safe to return? | Notes |
|--------|-----------------|-------|
| `ArgumentException.Message` (Pattern 2) | ✅ Yes | These messages are authored in the orchestrator specifically to be user-facing |
| `InvalidOperationException.Message` (Pattern 3) | ❌ No | Can originate from internal services (e.g. Python subprocess output); use a static generic string and log the exception |
| FluentValidation error messages | ✅ Yes | Authored in validator constructors |
| `Exception.Message` for unhandled catch-all | ❌ No | Replace with generic string; log full exception |
| `Exception.StackTrace` | ❌ Never | Log only; never return to client |
| `Exception.ToString()` | ❌ Never | Contains stack trace; log only |

---

## Verification Checklist

When reviewing a PR that adds or modifies an API endpoint in `Program.cs`, confirm:

- [ ] The catch-all `catch (Exception …)` block calls `logger.LogError(exception, …)`.
- [ ] `Results.Problem(detail: …)` uses a **string literal**, not `exception.Message`.
- [ ] No `exception.ToString()` or `exception.StackTrace` appears in any response body.
- [ ] `ArgumentException` catches return `ex.Message` only when the message was authored as
      a user-facing string in the orchestrator.
- [ ] `InvalidOperationException` catches return a **static generic string** — never `ex.Message`.

---

## History

This remediation was applied to all 29 `Results.Problem` call-sites in
`DailyDesk.Broker/Program.cs` as part of the issue
*"Improve error handling documentation for email auth API"*.  
Prior to this change every endpoint returned `detail: exception.Message` verbatim.
