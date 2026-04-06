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

### 14. Semantic Search via Ollama Embeddings + Qdrant (Phase 5)

**Where:** `EmbeddingService.cs`, `VectorStoreService.cs`, `KnowledgeIndexStore.cs`, `KnowledgePromptContextBuilder.cs`.

Knowledge documents are indexed into a Qdrant vector database via Ollama-generated embeddings:
- `EmbeddingService` calls Ollama `/api/embed` endpoint via OllamaSharp (graceful null fallback).
- `VectorStoreService` wraps Qdrant client with `UpsertAsync`, `SearchAsync`, `DeleteAsync`, `GetCollectionInfoAsync` (graceful empty fallback).
- `KnowledgeIndexStore` tracks indexed document hashes in LiteDB to avoid re-indexing unchanged documents.
- `KnowledgePromptContextBuilder.BuildRelevantContextWithSemanticSearchAsync()` queries Qdrant first, then fills remaining slots with keyword search.
- New job type `knowledge-index` for background document indexing.
- Endpoints: `POST /api/ml/index-knowledge`, `GET /api/knowledge/index-status`, `POST /api/knowledge/search`.

### 15. Agent Orchestration via Semantic Kernel (Phase 6)

**Where:** `OfficeKernelFactory.cs`, `DeskAgent.cs`, `DailyDesk/Services/Agents/`.

`Microsoft.SemanticKernel` 1.71.0 powers all five desk agents:
- `OfficeKernelFactory` builds an SK `Kernel` configured for the local Ollama endpoint.
- `DeskAgent` base class wraps an SK `ChatCompletionAgent` with system prompt and tool registration.
- Five desk agents (`ChiefOfStaffAgent`, `EngineeringDeskAgent`, `SuiteContextAgent`, `GrowthOpsAgent`, `MLEngineerAgent`) each have desk-specific tools and personality.
- Agent dispatch in `SendChatAsync` routes to the correct agent based on route, with fallback to direct `IModelProvider`.
- `DeskThreadState.Summary` enables multi-turn memory across conversations.
- `DeskMessageRecord.ToolCalls` records tool invocations per message.

### 16. Document Extraction via Docling (Phase 7)

**Where:** `DailyDesk/Scripts/extract_document_text.py`, `KnowledgeImportService.cs`, `LearningDocument.cs`.

- Python extraction script uses Docling when installed (PDF tables/figures, DOCX, PPTX, HTML, OCR).
- Falls back to `pypdf`/`python-docx` when Docling is not installed.
- Output format extended to `{ ok, text, metadata, tables, figures }`.
- `KnowledgeImportService.ExtractViaPythonRichAsync` returns full extraction response.
- `LearningDocument` model includes optional `ExtractedTable` and `ExtractedFigure` fields.

### 17. Scheduled Automation & Workflows (Phase 8)

**Where:** `JobSchedule.cs`, `JobSchedulerStore.cs`, `JobSchedulerWorker.cs`, `WorkflowTemplate.cs`, `WorkflowStore.cs`.

- `JobSchedulerStore` (LiteDB `job_schedules`) stores scheduled job definitions with interval, enabled flag, and last run time.
- `JobSchedulerWorker : BackgroundService` checks schedules every minute and enqueues jobs via `OfficeJobStore`.
- `daily-run` job type orchestrates state refresh → ML pipeline → artifact export → operator suggestions.
- `WorkflowStore` (LiteDB `workflow_templates`) stores operator-defined workflow templates with 3 built-ins: "Daily Run", "Exam Prep", "Knowledge Refresh".
- Endpoints: `/api/schedules` CRUD, `/api/daily-run/latest`, `/api/workflows` CRUD + `/api/workflows/{id}/run`.

### 18. WPF Client Async Integration (Phase 9)

**Where:** `JobPollingService.cs`, `KnowledgeSearchService.cs`, `KnowledgeSearchResult.cs`, `DeskMessageRecord.cs`.

- `JobPollingService` submits ML requests, polls `GET /api/jobs/{jobId}` every 2 seconds, and updates ViewModels on completion.
- `KnowledgeSearchService` calls `POST /api/knowledge/search` for semantic search with similarity scores and text fallback.
- `KnowledgeSearchResult` model carries document metadata and relevance score.
- `ToolCallRecord` in `DeskMessageRecord` surfaces agent tool invocations to the WPF UI.

---

## Current Dependencies

| Project | NuGet Packages |
|---------|---------------|
| `DailyDesk` | None (project ref to Core only) |
| `DailyDesk.Core` | `Microsoft.ML.OnnxRuntime` 1.24.4, `AngleSharp` 1.4.0, `OllamaSharp` 5.4.25, `LiteDB` 5.0.21, `Polly.Core` 8.6.6, `Qdrant.Client` 1.17.0, `Microsoft.SemanticKernel` 1.71.0 |
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

### Core Endpoints
- Health, state, chat, study, research, watchlist, inbox, library, history, workspace, ML.

### Job Endpoints (Phase 3 — 5 endpoints)
| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/jobs` | List recent jobs (last 50), supports `?status=...&type=...` filters |
| GET | `/api/jobs/{jobId}` | Get job status and metadata |
| GET | `/api/jobs/{jobId}/result` | Get job result JSON (succeeded only) |
| GET | `/api/jobs/metrics` | Get job metrics: counts by status, average duration, queue depth |
| DELETE | `/api/jobs/{jobId}` | Delete a completed job (succeeded/failed only) |

### ML Endpoints (Updated — all async by default)
| Method | Endpoint | Default | `?sync=true` |
|--------|----------|---------|-------------|
| POST | `/api/ml/analytics` | Returns `{ jobId, status }` | Blocks and returns result |
| POST | `/api/ml/forecast` | Returns `{ jobId, status }` | Blocks and returns result |
| POST | `/api/ml/embeddings` | Returns `{ jobId, status }` | Blocks and returns result |
| POST | `/api/ml/pipeline` | Returns `{ jobId, status }` | Blocks and returns result |
| POST | `/api/ml/export-artifacts` | Returns `{ jobId, status }` | Blocks and returns result |
| POST | `/api/ml/index-knowledge` | Returns `{ jobId, status }` | Blocks and returns result |

### Knowledge Endpoints (Phase 5 — 3 endpoints)
| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/knowledge/index-status` | Get indexed vs. total document count and vector store status |
| POST | `/api/knowledge/search` | Semantic search across knowledge library |

### Health Endpoint (Phase 4)
| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/health` | Structured subsystem health (Ollama, Python, LiteDB, job worker) |

### Schedule Endpoints (Phase 8 — 4 endpoints)
| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/schedules` | List all job schedules |
| POST | `/api/schedules` | Create a new schedule |
| PUT | `/api/schedules/{id}` | Update a schedule (enable/disable, change interval) |
| DELETE | `/api/schedules/{id}` | Remove a schedule |

### Daily Run & Workflow Endpoints (Phase 8 — 5 endpoints)
| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/daily-run/latest` | Get most recent daily run summary |
| GET | `/api/workflows` | List workflow templates |
| POST | `/api/workflows` | Create a workflow template |
| POST | `/api/workflows/{id}/run` | Execute a workflow template |
| DELETE | `/api/workflows/{id}` | Remove a workflow template |

---

## Test Coverage (243 tests)

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
| Job model integration tests | 13 | FIFO ordering, full lifecycle succeed/fail, edge cases, payload round-trip, ListRecent limits/mixed statuses, idempotent recovery, multi-iteration, dequeue skips |
| Job management & retention | 9 | DeleteById (completed/nonexistent/queued/failed), DeleteOlderThan (expired/active), ListByStatus (filter/limit), GetTotalCount |
| Phase 4: Health & Metrics | 6 | Health report model defaults, job metrics calculation, average duration, retention worker |
| Phase 5: Semantic search | 28 | EmbeddingService (model defaults, empty/null/unreachable), VectorSearchResult/CollectionInfo defaults, KnowledgeIndexStore (CRUD, hash, indexing lifecycle), OllamaService embedding fallback, KnowledgePromptContextBuilder semantic fallback, IndexedDocumentRecord defaults, OfficeDatabase KnowledgeIndex collection |
| Phase 6: Agent orchestration | 30 | OfficeKernelFactory, DeskAgent base class, 5 desk agents (dispatch, fallback, summary), DeskThreadState summary, DeskMessageRecord ToolCalls, OperatorMemoryStore CloneDeskMessage |
| Phase 7: Docling extraction | 10 | ExtractedTable/Figure models, KnowledgeImportService rich extraction, LearningDocument fields |
| Phase 8: Scheduling & workflows | 60 | JobSchedule CRUD, JobSchedulerStore (enabled/disabled/due), WorkflowStore (built-ins, CRUD), WorkflowTemplate step execution, DailyRunTemplate model, OfficeJobType daily-run |
| Phase 9: WPF async integration | 40 | JobPollingService state transitions, KnowledgeSearchService (semantic/fallback/empty), KnowledgeSearchResult model, ToolCallRecord in messages |
