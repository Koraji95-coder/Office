# Incremental Architecture Plan

> **Goal:** Improve reliability, observability, and persistence — then prepare for async job execution. No rewrites. No new UI. No Semantic Kernel or Qdrant until prerequisites exist.

---

## Where the Repo Already Aligns

Before proposing changes, it is important to recognize what the codebase already does well:

| Pattern | Where | Why It Matters |
|---------|-------|----------------|
| **Dual-semaphore concurrency** | `OfficeBrokerOrchestrator._gate` / `_mlGate` | ML work already runs outside the main state lock. This is the seed for a proper job model. |
| **Parallel ML execution** | `RunFullMLPipelineAsync` uses `Task.WhenAll` | Analytics, forecast, and embeddings are independent. The orchestrator already treats them as separate units of work. |
| **Graceful degradation** | `OllamaService`, `MLAnalyticsService`, `LiveResearchService` | Every external call has a fallback path: CLI fallback for model listing, heuristic fallback for ML, deterministic synthesis for research. |
| **ProcessRunner subprocess model** | `ProcessRunner.cs` | Python ML work is already isolated in subprocesses with stdout/stderr capture and exit code checking. |
| **TTL-based caching** | `MLAnalyticsService` (5-min default) | ML results are cached to avoid redundant work. This is a precursor to job result caching. |
| **Request models as records** | `Program.cs` (lines 632–651) | All broker request types are immutable records. FluentValidation can target these directly. |
| **IModelProvider abstraction** | `OllamaService : IModelProvider` | The LLM provider is already behind an interface. OllamaSharp can be swapped in without touching callers. |
| **State normalization on load** | `OfficeSessionStateStore`, `OperatorMemoryStore` | Stores already normalize/migrate state on load. LiteDB migration can follow this same pattern. |

---

## Phase 1 — Foundation (Logging, Parsing, Validation, Ollama Client)

**Goal:** Improve observability, correctness, and client reliability without changing architecture.

### 1.1 Serilog Structured Logging

**Scope:** `DailyDesk.Broker` (primary), `DailyDesk.Core` (secondary).

**What changes:**
- Add `Serilog.AspNetCore` and `Serilog.Sinks.File` to `DailyDesk.Broker.csproj`.
- Configure in `Program.cs`: console sink + rolling file sink (`State/logs/office-broker-.log`).
- Replace `builder.WebHost` logger with `UseSerilog()`.
- No changes to existing `logger.LogError()` call sites — Serilog plugs into `ILogger`.
- Add `ILogger` injection to `OllamaService`, `ProcessRunner`, and `MLAnalyticsService` for structured diagnostics (elapsed time, model name, exit codes).

**What stays the same:**
- All 20+ endpoint catch blocks remain unchanged.
- No new log levels forced into services that currently use fallback patterns.

**Smallest safe PR:** Serilog setup in Broker `Program.cs` + rolling file config only. Service-level logging can follow.

### 1.2 AngleSharp HTML Extraction

**Scope:** `DailyDesk/Services/LiveResearchService.cs`.

**What changes:**
- Add `AngleSharp` NuGet to `DailyDesk.Core.csproj` (since `LiveResearchService` is linked into Core).
- Replace the four compiled `Regex` patterns (`ResultLinkRegex`, `ResultSnippetRegex`, `DescriptionMetaRegex`, `OgDescriptionMetaRegex`) with AngleSharp DOM queries.
- Replace `ExtractPreview` regex pipeline with AngleSharp document parsing.
- `CleanHtml` becomes `document.Body.TextContent` (built-in text extraction).

**What stays the same:**
- `SearchAsync` public API unchanged.
- `EnrichSourcesAsync` parallel pattern unchanged.
- HTTP client configuration unchanged.
- Fallback patterns unchanged.

**Why not HtmlAgilityPack:** AngleSharp provides a full DOM with `querySelector` / `querySelectorAll` that matches browser behavior. HAP is lighter but more fragile for dynamic content patterns.

### 1.3 FluentValidation for Broker Requests

**Scope:** `DailyDesk.Broker/Program.cs` request records.

**What changes:**
- Add `FluentValidation` NuGet to `DailyDesk.Broker.csproj`.
- Create validators for request records that currently rely on orchestrator `ArgumentException` throws.
- Call `validator.Validate()` at the top of each endpoint, returning `400 BadRequest` with structured error details.
- Remove duplicated validation from orchestrator methods where the broker now handles it.

**What stays the same:**
- Orchestrator state validation (`InvalidOperationException`) stays in the orchestrator.
- Numeric clamping (`Math.Clamp`) stays in the orchestrator.
- Request record definitions stay in `Program.cs` (can move later).

**Target validators:**
- `ChatRouteRequestValidator` — Route not empty, Route in known catalog.
- `ChatSendRequestValidator` — Prompt not empty.
- `StudyScoreDefenseRequestValidator` — Answer not empty.
- `StudySaveReflectionRequestValidator` — Reflection not empty.
- `ResearchRunRequestValidator` — Query not empty.
- `WatchlistRunRequestValidator` — WatchlistId not empty.
- `InboxResolveRequestValidator` — SuggestionId not empty, Status in `[accepted, deferred, rejected]`.

### 1.4 OllamaSharp Client Replacement

**Scope:** `DailyDesk/Services/OllamaService.cs`.

**What changes:**
- Add `OllamaSharp` NuGet to `DailyDesk.Core.csproj`.
- Replace internal `HttpClient` + manual JSON serialization with `OllamaApiClient`.
- Replace `OllamaChatRequest/Response` records with OllamaSharp's typed API.
- Replace `GetInstalledModelsAsync` HTTP+CLI dual path with OllamaSharp's `ListLocalModelsAsync`.
- Replace `GenerateAsync` / `GenerateJsonAsync` with OllamaSharp chat API.

**What stays the same:**
- `IModelProvider` interface unchanged.
- `OllamaService` class name and constructor signature unchanged.
- `ProcessRunner` fallback for model listing stays as defensive backup.
- 90-second timeout behavior preserved.
- No streaming added (can be added later as a separate PR).

**Why OllamaSharp and not Semantic Kernel:** OllamaSharp is a thin, typed HTTP client. It replaces the exact code we wrote by hand. Semantic Kernel is an orchestration framework that would change the architecture — that comes later (Phase 4) after async jobs exist.

---

## Phase 2 — Persistence & Resilience (LiteDB, Polly)

**Goal:** Replace fragile JSON file I/O with a proper embedded database. Add retry/circuit-breaker patterns to all external calls.

### 2.1 LiteDB Local Persistence

**Scope:** `TrainingStore.cs`, `OperatorMemoryStore.cs`, `OfficeSessionStateStore.cs`.

**What changes:**
- Add `LiteDB` NuGet to `DailyDesk.Core.csproj`.
- Create `OfficeDatabase` wrapper class that manages a single `LiteDatabase` instance (`office.db`).
- Migrate each store to use LiteDB collections instead of JSON files:
  - `TrainingStore` → `training_attempts`, `defense_attempts`, `reflections` collections.
  - `OperatorMemoryStore` → `policies`, `watchlists`, `suggestions`, `activities` collections.
  - `OfficeSessionStateStore` → `session_state` collection (single document).
- Preserve existing load/save semantics and normalization logic.
- Add a `jobs` collection (empty schema) for Phase 3.
- Keep JSON export capability for Dropbox sync compatibility.

**What stays the same:**
- All store public APIs remain identical.
- Normalization/migration logic stays.
- Dropbox path configuration stays.
- Max-item limits (120 attempts, 240 suggestions) stay as collection-level logic.

**Migration path:** On first run with LiteDB, check for existing JSON files and import them. Mark JSON files as migrated (rename with `.migrated` suffix). Fall back to JSON if LiteDB fails to open.

### 2.2 Polly Resilience Pipelines

**Scope:** `OllamaService`, `LiveResearchService`, `ProcessRunner`, `MLAnalyticsService`.

**What changes:**
- Add `Polly` NuGet to `DailyDesk.Core.csproj`.
- Define named resilience pipelines:
  - **`ollama`**: Retry 3× with exponential backoff (2s, 4s, 8s) + circuit breaker (5 failures → 30s open).
  - **`web-research`**: Retry 2× with 1s delay + timeout at 25s (current timeout preserved).
  - **`python-subprocess`**: Retry 1× (for transient process spawn failures) + timeout at 90s.
- Wrap `OllamaService` HTTP calls in the `ollama` pipeline.
- Wrap `LiveResearchService.SearchAsync` and `EnrichSourcesAsync` calls in the `web-research` pipeline.
- Wrap `ProcessRunner.RunAsync` calls in the `python-subprocess` pipeline.

**What stays the same:**
- Fallback patterns in services remain (Polly adds retry before the existing fallback).
- `_mlGate` semaphore stays (Polly handles transient faults, not concurrency control).
- Existing timeout values preserved as Polly timeout policies.
- `ProcessRunner` exit code checking unchanged.

**Why Polly and not `Microsoft.Extensions.Http.Resilience`:** Polly works for both HTTP and non-HTTP calls (Python subprocesses). The extensions package is HTTP-only. We need both.

---

## Phase 3 — Async Job Model for ML Work

**Goal:** Move ML pipeline execution from synchronous broker endpoints to a background job system with persistent state.

### 3.1 Job Record Model

```
OfficeJob
├── Id: string (GUID)
├── Type: string ("ml-analytics" | "ml-forecast" | "ml-embeddings" | "ml-pipeline")
├── Status: string ("queued" | "running" | "succeeded" | "failed")
├── CreatedAt: DateTimeOffset
├── StartedAt: DateTimeOffset?
├── CompletedAt: DateTimeOffset?
├── Error: string?
├── ResultKey: string? (pointer to result in LiteDB)
└── RequestedBy: string? ("broker" | "operator" | "schedule")
```

**Persisted in:** LiteDB `jobs` collection (created in Phase 2).

### 3.2 Background Worker

**Scope:** `DailyDesk.Broker` — new `IHostedService`.

**What changes:**
- Add `OfficeJobWorker : BackgroundService` to `DailyDesk.Broker`.
- Worker polls LiteDB `jobs` collection for `queued` jobs.
- Executes one job at a time (reuses `_mlGate` concurrency model).
- Updates job status through lifecycle: `queued` → `running` → `succeeded`/`failed`.
- Stores results in LiteDB (keyed by `ResultKey`).
- On failure: captures exception message in `Error` field, sets status to `failed`.

**What stays the same:**
- Existing ML endpoint handlers still work (they now enqueue a job and return the job ID).
- `RunFullMLPipelineAsync` logic moves to the worker but uses the same orchestrator methods.
- `_mlGate` semaphore continues to prevent concurrent ML execution.
- Python subprocess execution unchanged.

### 3.3 Status & Result Endpoints

**New endpoints:**
- `GET /api/jobs/{jobId}` — Return job record (status, timestamps, error).
- `GET /api/jobs/{jobId}/result` — Return job result (if succeeded).
- `GET /api/jobs` — List recent jobs (last 50).

**Modified endpoints:**
- `POST /api/ml/analytics` → Returns `{ jobId, status: "queued" }` instead of blocking.
- `POST /api/ml/forecast` → Same.
- `POST /api/ml/embeddings` → Same.
- `POST /api/ml/pipeline` → Same.

**Backward compatibility:** Add `?sync=true` query parameter to ML endpoints for callers that need the old blocking behavior during migration.

### 3.4 No UI Changes Required

The WPF client currently calls ML endpoints and waits for the response. With the async model:
- Client gets back a job ID immediately.
- Client polls `GET /api/jobs/{jobId}` until status is `succeeded` or `failed`.
- This can be implemented in the WPF ViewModel later without broker changes.

---

## Phase 4 — Future Evaluation (After Job Model Exists)

### 4.1 Qdrant for Persistent Semantic Retrieval

**Prerequisite:** Phase 3 complete (async jobs running, LiteDB storing results).

**Evaluate when:**
- Document embeddings are being generated regularly via the job model.
- The current TF-IDF fallback is demonstrably insufficient for knowledge search.
- Docker is available on the target workstation.

**Integration point:** Replace `ml_document_embeddings.py` with:
1. Ollama embeddings API (via OllamaSharp) for vector generation.
2. Qdrant (local Docker) for vector storage and search.
3. `KnowledgePromptContextBuilder` queries Qdrant instead of in-memory search.

### 4.2 Semantic Kernel for Agent Orchestration

**Prerequisite:** Phase 3 complete + stable tool/plugin boundary in the broker.

**Evaluate when:**
- The 5 agent desks need tool-calling capabilities (execute code, query databases, trigger jobs).
- Prompt composition needs template variables and chaining.
- Multi-turn agent conversations need memory beyond thread state.

**Integration point:** Replace `PromptComposer` + `OfficeRouteCatalog` with SK agents. Each desk becomes an SK agent with its own tools and system prompt.

### 4.3 Docling for Richer Document Extraction

**Prerequisite:** None (optional at any phase).

**Evaluate when:**
- PDF table extraction is needed (current `pypdf` is text-only).
- PowerPoint content extraction is needed (currently claimed but not implemented).
- OCR for scanned documents is needed.

**Integration point:** Replace `extract_document_text.py` with Docling pipeline. Keep the same `ProcessRunner` subprocess model.

---

## Smallest Safe Starting PR

**PR #1: Serilog structured logging in the Broker.**

This is the lowest-risk, highest-value change:
- Adds no new behavior — only observability.
- Plugs into existing `ILogger` interface (zero call site changes).
- Rolling file logs go to `State/logs/` alongside existing state files.
- No service changes needed.
- Fully testable on Linux (Broker builds on Linux).

**Files touched:**
- `DailyDesk.Broker/DailyDesk.Broker.csproj` — add Serilog packages.
- `DailyDesk.Broker/Program.cs` — add `UseSerilog()` and configuration (~10 lines).
- `DailyDesk.Broker/appsettings.json` — add Serilog section (optional).

**Risk:** Near zero. Serilog is the most widely-used .NET logging library and is a drop-in for the built-in logger.
