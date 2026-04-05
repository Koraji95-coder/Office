# Current State — What Already Works

> **Purpose:** Document what the repo already does well, so AI agents and contributors do not re-implement or break working patterns. This is a snapshot of the codebase's strengths and alignment with the incremental architecture direction.

---

## Architecture Patterns Already in Place

### 1. Service Boundary via `IModelProvider`

**Where:** `DailyDesk/Services/OllamaService.cs` implements `IModelProvider`.

The LLM provider is already behind an interface. Every caller (`OfficeBrokerOrchestrator`, `TrainingGeneratorService`, `LiveResearchService`, `OralDefenseService`, etc.) depends on `IModelProvider`, not `OllamaService` directly.

**Implication:** OllamaSharp can be swapped into `OllamaService` internals without touching any caller. Future providers (OpenAI, Anthropic, local GGUF) can implement `IModelProvider`.

### 2. Dual-Semaphore Concurrency Control

**Where:** `OfficeBrokerOrchestrator.cs` — `_gate` (SemaphoreSlim) for shared state, `_mlGate` (SemaphoreSlim) for ML work.

ML methods snapshot state under `_gate`, then release it and acquire `_mlGate` for the actual ML execution. This means:
- State reads (`GetStateAsync`) are not blocked during ML runs.
- Only one ML operation runs at a time.
- The semaphore model is the precursor to a proper job queue.

**Implication:** The async job model (Phase 3) reuses `_mlGate` semantics. The job worker acquires `_mlGate` before executing, preserving the same concurrency guarantee.

### 3. Parallel ML Execution

**Where:** `RunFullMLPipelineAsync` — `Task.WhenAll(analyticsTask, forecastTask, embeddingsTask)`.

The three ML engines (analytics, forecast, embeddings) are already recognized as independent units of work and run in parallel. This is the seed for the job model — each engine becomes a separate job type.

### 4. Graceful Degradation Everywhere

**Where:** Every external call has a fallback path.

| Service | External Call | Fallback |
|---------|--------------|----------|
| `OllamaService.GetInstalledModelsAsync` | HTTP `api/tags` | CLI `ollama list` → empty array |
| `OllamaService.GenerateAsync` (via orchestrator) | HTTP `api/chat` | `BuildDeskFallbackResponse()` deterministic text |
| `LiveResearchService.RunAsync` | Model-generated synthesis | `BuildFallbackReport()` deterministic synthesis |
| `LiveResearchService.EnrichSourcesAsync` | HTTP page fetch | Return source unchanged |
| `MLAnalyticsService` | Python subprocess (sklearn/pytorch/tensorflow) | Heuristic fallback with `Ok=false, Engine="fallback"` |

**Implication:** Polly retry pipelines (Phase 2) add retry before these fallbacks trigger. The fallback logic stays as the last resort.

### 5. ProcessRunner Subprocess Isolation

**Where:** `DailyDesk/Services/ProcessRunner.cs` (47 lines).

Python ML work is already isolated in subprocesses with:
- Redirected stdout/stderr capture.
- Exit code checking.
- `CancellationToken` support via `WaitForExitAsync`.
- Structured error messages from stderr.

**Implication:** Polly can wrap `ProcessRunner.RunAsync` for retry on transient spawn failures. The job model can reuse `ProcessRunner` as the execution mechanism.

### 6. TTL-Based ML Caching

**Where:** `MLAnalyticsService` — 5-minute default TTL for cached results.

ML results are cached to avoid redundant subprocess execution. The cache key includes the input data hash.

**Implication:** LiteDB persistence (Phase 2) can replace in-memory caching with persistent result storage. The job model (Phase 3) stores results by job ID, making the TTL cache unnecessary for ML work.

### 7. Request Models as Immutable Records

**Where:** `DailyDesk.Broker/Program.cs` — 15 `record` types.

All broker request types are `sealed record` with init-only properties. This makes them ideal targets for FluentValidation — validators can target the record type directly without needing mutable state.

### 8. State Normalization on Load

**Where:** `OfficeSessionStateStore.Normalize()`, `OperatorMemoryStore.NormalizeState()`.

Both stores normalize state after loading:
- Default values for missing properties.
- Route normalization via `OfficeRouteCatalog.NormalizeRoute()`.
- Numeric clamping (`Math.Clamp`).
- Legacy data migration (`HydrateLegacyPracticeAttempts`).

**Implication:** LiteDB migration (Phase 2) uses the same normalization logic. On first LiteDB load, data is imported from JSON and normalized.

### 9. Consistent Error Response Pattern

**Where:** `DailyDesk.Broker/Program.cs` — all endpoints follow the same pattern:

```
try { ... Results.Ok(...) }
catch (ArgumentException) { Results.BadRequest({ error }) }
catch (InvalidOperationException) { Results.BadRequest({ error }) }
catch (Exception) { logger.LogError(); Results.Problem() }
```

**Implication:** FluentValidation (Phase 1) adds a validation step before the try block. The catch pattern stays the same for orchestrator-level errors.

### 10. Shared Code via Compile-Link

**Where:** `DailyDesk.Core.csproj` — `<Compile Include="..\DailyDesk\Models\**\*.cs" Link="..." />`.

Models and Services from `DailyDesk/` are compiled into `DailyDesk.Core` via linked compilation. NuGet packages added to `DailyDesk.Core.csproj` are available to both the WPF app and the Broker.

### 11. LiteDB Local Persistence (Phase 2)

**Where:** `OfficeDatabase.cs`, `TrainingStore.cs`, `OperatorMemoryStore.cs`, `OfficeSessionStateStore.cs`.

All three stores now support LiteDB as the primary persistence backend with JSON file fallback:
- Single `office.db` file in the state root directory (shared connection mode).
- Automatic migration from JSON files on first run (renames originals with `.migrated` suffix).
- Collection-based storage with indexes on query fields.
- Max-item limits enforced at the collection level (120 attempts, 240 suggestions, 600 activities).

### 12. Polly Resilience Pipelines (Phase 2)

**Where:** `OfficeResiliencePipelines.cs`, wired into `OllamaService`, `LiveResearchService`, `MLAnalyticsService`.

Three named pipelines:
- **ollama**: 3× retry (exponential 2s/4s/8s) + circuit breaker (5 failures → 30s open) + 90s timeout.
- **web-research**: 2× retry (exponential 1s/2s) + 25s timeout.
- **python-subprocess**: 1× retry (1s constant) + 90s timeout.

### 13. Async Job Model (Phase 3)

**Where:** `OfficeJob.cs`, `OfficeJobStore.cs`, `OfficeJobWorker.cs`, `Program.cs`.

ML endpoints now return a job ID immediately instead of blocking:
- Job lifecycle: `queued` → `running` → `succeeded`/`failed`.
- Background worker (`IHostedService`) polls for queued jobs and executes one at a time.
- Status endpoints: `GET /api/jobs`, `GET /api/jobs/{jobId}`, `GET /api/jobs/{jobId}/result`.
- Backward compatibility: `?sync=true` query parameter on ML endpoints for blocking behavior.

---

## Current Dependencies

| Project | NuGet Packages |
|---------|---------------|
| `DailyDesk` | None (project ref to Core only) |
| `DailyDesk.Core` | `Microsoft.ML.OnnxRuntime` 1.24.4, `AngleSharp` 1.4.0, `OllamaSharp` 5.4.25, `LiteDB` 5.0.21, `Polly.Core` 8.6.6 |
| `DailyDesk.Broker` | `FluentValidation` 12.1.1, `Serilog.AspNetCore` 10.0.0 |
| `DailyDesk.Core.Tests` | `xunit` 2.9.3, `xunit.runner.visualstudio` 2.8.2, `Microsoft.NET.Test.Sdk` 17.14.1, `coverlet.collector` 6.0.4 |

---

## Build & Test Commands

```bash
# Build Core + Tests (works on Linux)
dotnet build DailyDesk.Core.Tests/DailyDesk.Core.Tests.csproj

# Run tests
dotnet test DailyDesk.Core.Tests

# Build WPF (Windows-only, but can cross-compile on Linux)
dotnet build DailyDesk/DailyDesk.csproj -p:EnableWindowsTargeting=true

# Build Broker (works on Linux)
dotnet build DailyDesk.Broker/DailyDesk.Broker.csproj
```

**Known issue:** `ResolveOfficeRootPath` test fails on Linux (uses Windows path conventions).

---

## File Locations

| Data | Path | Format |
|------|------|--------|
| LiteDB database | `{state-root}/office.db` | LiteDB |
| Training history (legacy) | `%LOCALAPPDATA%\DailyDesk\training-history.json` | JSON |
| Operator memory (legacy) | `%LOCALAPPDATA%\DailyDesk\operator-memory.json` | JSON |
| Session state (legacy) | `%LOCALAPPDATA%\DailyDesk\broker-live-session.json` | JSON |
| Knowledge library | `%USERPROFILE%\Dropbox\SuiteWorkspace\Office\Knowledge` | Mixed (md, txt, pdf, docx) |
| State root | `%USERPROFILE%\Dropbox\SuiteWorkspace\Office\State` | Mixed |
| ML artifacts | `State/ml-artifacts/` | JSON |
| Python scripts | `DailyDesk/Scripts/` | Python |
| ONNX models | `DailyDesk/Models/onnx/` | ONNX |

---

## API Endpoints

### Core Endpoints (25 existing)
- Health, state, chat, study, research, watchlist, inbox, library, history, workspace, ML.

### Job Endpoints (Phase 3 — 4 endpoints)
| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/jobs` | List recent jobs (last 50), supports `?status=...&type=...` filters |
| GET | `/api/jobs/{jobId}` | Get job status and metadata |
| GET | `/api/jobs/{jobId}/result` | Get job result JSON (succeeded only) |
| DELETE | `/api/jobs/{jobId}` | Delete a completed job (succeeded/failed only) |

### ML Endpoints (Updated)
| Method | Endpoint | Default | `?sync=true` |
|--------|----------|---------|-------------|
| POST | `/api/ml/analytics` | Returns `{ jobId, status }` | Blocks and returns result |
| POST | `/api/ml/forecast` | Returns `{ jobId, status }` | Blocks and returns result |
| POST | `/api/ml/embeddings` | Returns `{ jobId, status }` | Blocks and returns result |
| POST | `/api/ml/pipeline` | Returns `{ jobId, status }` | Blocks and returns result |
| POST | `/api/ml/export-artifacts` | Returns `{ jobId, status }` | Blocks and returns result |

---

## Test Coverage (89 tests)

| Area | Tests | Coverage |
|------|-------|----------|
| Route normalization & display | 10 | `NormalizeRoute`, `ResolveRouteDisplayTitle`, `KnownRoutes` |
| Study session stages | 2 | `ResolveStage` transitions |
| ML system (prompts, settings, caching) | 12 | `MLAnalyticsService`, `OnnxMLEngine`, `DailySettings` |
| LiteDB persistence | 3 | `OfficeDatabase`, migration tracking |
| Polly resilience pipelines | 3 | Ollama, web-research, python-subprocess pipelines |
| TrainingStore LiteDB | 2 | Save/load/reset practice attempts |
| MLResultStore LiteDB | 3 | Analytics, forecast, embeddings persistence |
| Job model unit tests | 8 | Enqueue, retrieve, dequeue, mark succeeded/failed, list recent |
| Stale job recovery | 4 | Old running → failed, recent running preserved, queued/completed ignored, count |
| Job model integration tests (PR 5) | 13 | FIFO ordering, full lifecycle succeed/fail, edge cases, payload round-trip, ListRecent limits/mixed statuses, idempotent recovery, multi-iteration, dequeue skips |
| **Job management & retention (PR 6)** | **9** | **DeleteById (completed/nonexistent/queued/failed), DeleteOlderThan (expired/active), ListByStatus (filter/limit), GetTotalCount** |

---

## Phase 4 — Future Evaluation (Not Started)

### Qdrant for Persistent Semantic Retrieval
- **Prerequisite:** Phase 3 complete (async jobs running, LiteDB storing results).
- **Integration point:** Replace `ml_document_embeddings.py` with Ollama embeddings API (via OllamaSharp) + Qdrant (local Docker) for vector storage. `KnowledgePromptContextBuilder` queries Qdrant instead of in-memory search.
- **Evaluate when:** Document embeddings are being generated regularly via the job model.

### Semantic Kernel for Agent Orchestration
- **Prerequisite:** Phase 3 complete + stable tool/plugin boundary in the broker.
- **Integration point:** Replace `PromptComposer` + `OfficeRouteCatalog` with SK agents. Each desk becomes an SK agent with its own tools and system prompt.
- **Evaluate when:** The 5 agent desks need tool-calling capabilities.

### Docling for Richer Document Extraction
- **Prerequisite:** None (optional at any phase).
- **Integration point:** Replace `extract_document_text.py` with Docling pipeline. Keep the same `ProcessRunner` subprocess model.
- **Evaluate when:** PDF table extraction, PowerPoint content extraction, or OCR for scanned documents is needed.
