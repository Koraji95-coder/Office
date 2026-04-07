# Refactor Pressure Notes

> **Purpose:** Track areas of the codebase under known refactor pressure, document the technical debt they represent, and provide prioritized guidance for future cleanup. This document is the canonical source for the "Refactor pressure notes" surface referenced in the Developer Portal storyboard.

---

## How to Use This Document

Each entry describes a **pressure area**: a part of the codebase that is functional today but is accumulating structural debt. Entries are grouped by priority — High, Medium, and Low — based on how much they affect future development velocity. When a pressure area is resolved, move its entry to the **Resolved Pressure** archive table at the bottom of this document. If the resolution corresponds to a new phase of work, also mark that phase complete in `PHASES-ROADMAP.md`.

---

## High Pressure

These areas actively slow down new feature development and increase the risk of regression.

### 1. OfficeBrokerOrchestrator — Monolithic Coordinator

| | |
|---|---|
| **Files** | `DailyDesk.Core/Services/OfficeBrokerOrchestrator.cs`, `DailyDesk.Core/Services/MLPipelineCoordinator.cs` |
| **Size** | ~3,850 lines |
| **Phase introduced** | Phase 1 (grew through Phase 9) |

**What it does now:**
The orchestrator is the single entry point for every operation — study sessions, ML pipelines, research jobs, knowledge indexing, agent dispatch, operator memory, and workspace snapshots. It holds references to 15+ injected services and coordinates state transitions under a shared `_gate` semaphore.

**Why it is under pressure:**
- Adding a new desk workflow requires modifying this file regardless of whether the change is related to other areas.
- All state reads go through the same `SemaphoreSlim`, making selective locking impossible without touching the orchestrator.
- Test coverage requires constructing the entire orchestrator graph, even for tests that only exercise one domain (e.g., study session scoring).

**Refactor direction:**
Split into domain coordinators:
- `StudySessionCoordinator` — practice, defense, reflection, scoring.
- `ResearchCoordinator` — research jobs, watchlist, enrichment.
- `MLPipelineCoordinator` — ML job dispatch, result retrieval, export artifacts. ✅ Extracted and wired: `OfficeBrokerOrchestrator` now holds `_mlPipelineCoordinator` and delegates all ML pipeline methods to it.
- `KnowledgeCoordinator` — import, indexing, context building.

Keep `OfficeBrokerOrchestrator` as a thin facade that delegates to these coordinators. The facade boundary means `Program.cs` endpoints do not change callers.

**Prerequisite:** No blocking prerequisite. This is a refactor of existing code. Start with the study session domain — it is the most self-contained.

---

### 2. Broker Program.cs — All Endpoints in One File

| | |
|---|---|
| **File** | `DailyDesk.Broker/Program.cs` |
| **Size** | ~1,120 lines |
| **Phase introduced** | Phase 1 (grew through Phase 9) |

**What it does now:**
All 30+ API endpoints are defined inline in `Program.cs`. Shared infrastructure (request records, service registration, middleware, and logging) is mixed with endpoint handler logic in the same file. Request record types are defined at the top of `Program.cs` while their corresponding validators live in the `DailyDesk.Broker/Validators/` folder, which means navigating between a record definition and its validation rules requires crossing files.

**Why it is under pressure:**
- Finding a specific endpoint requires searching through 1,000+ lines.
- Adding a new endpoint family (e.g., a future customer API) means extending an already-dense file.
- Service registration, middleware setup, and endpoint handlers are all interleaved, making each section harder to scan in isolation.

**Refactor direction:**
Extract endpoint groups into separate files using `IEndpointRouteBuilder` extension methods:
- `StudyEndpoints.cs` — `/api/study/*`
- `MLEndpoints.cs` — `/api/ml/*`, `/api/jobs/*`
- `KnowledgeEndpoints.cs` — `/api/knowledge/*`, `/api/library/*`
- `ScheduleEndpoints.cs` — `/api/schedules/*`, `/api/workflows/*`, `/api/daily-run/*`
- `OperatorEndpoints.cs` — `/api/inbox/*`, `/api/operator/*`, `/api/workspace/*`

Keep shared setup (Serilog, DI, middleware) in `Program.cs`. Endpoint groups call `app.MapStudyEndpoints()` etc.

**Prerequisite:** None. This is additive restructuring. The test suite is not affected because tests call the orchestrator directly, not the endpoint handlers.

---

## Medium Pressure

These areas add maintenance overhead but do not block current development.

### 3. MLAnalyticsService — Redundant In-Memory TTL Cache

| | |
|---|---|
| **File** | `DailyDesk/Services/MLAnalyticsService.cs` |
| **Phase introduced** | Phase 1 |
| **Made redundant by** | Phase 3 (`MLResultStore`, LiteDB) |

**What it does now:**
`MLAnalyticsService` maintains an in-memory 5-minute TTL cache for analytics, forecast, and embeddings results (`_cachedAnalytics`, `_cachedEmbeddings`, `_cachedForecast`). This was added before the async job model existed.

**Why it is under pressure:**
- `MLResultStore` (Phase 3) now persists the latest result for each ML type in LiteDB. Callers can retrieve a persistent, restart-safe result from `MLResultStore` at any time.
- The in-memory cache and `MLResultStore` can diverge: a fresh run persisted to LiteDB will not be reflected in the in-memory cache until the TTL expires.
- `InvalidateCache()` must be called explicitly after a job completes, creating a coordination requirement between `OfficeJobWorker` and `MLAnalyticsService`.

**Refactor direction:**
Remove the in-memory TTL cache from `MLAnalyticsService`. Replace the three `_cached*` field groups with a single call to `MLResultStore.GetLatest*(type)` where callers currently access cached results. Keep the `InvalidateCache()` method as a no-op stub temporarily so call sites compile without change, then remove it once all callers are updated.

**Prerequisite:** Confirm `MLResultStore` returns a non-null result for all three ML types after a job completes. Covered by existing Phase 3 integration tests.

---

### 4. ML Endpoints — `?sync=true` Backward Compatibility Path

| | |
|---|---|
| **Files** | `DailyDesk.Broker/Program.cs` |
| **Phase introduced** | Phase 3 (async job model) |
| **Removal condition** | WPF client fully migrated to job polling (Phase 9 complete) |

**What it does now:**
Six ML endpoints (`/api/ml/analytics`, `/forecast`, `/embeddings`, `/pipeline`, `/export-artifacts`, `/ml/index-knowledge`) accept a `?sync=true` query parameter that switches them back to synchronous blocking behavior. This was added to allow gradual migration of callers during the Phase 3 async transition.

**Why it is under pressure:**
- The synchronous code path is a duplicate of the pre-Phase-3 blocking logic. Both paths must be kept in sync when the ML execution logic changes.
- `sync=true` callers bypass job lifecycle tracking (no job ID, no status polling, no retention).
- Phase 9 (WPF async integration) adds `JobPollingService` to the WPF client. Once the client migrates to polling, the sync path has no remaining legitimate callers.

**Refactor direction:**
After confirming no active callers use `?sync=true`:
1. Remove the `sync` query parameter check from each endpoint.
2. Remove the inline blocking code path in each handler.
3. Update `CURRENT-STATE.md` to reflect that ML endpoints are async-only.

**Prerequisite:** Phase 9 WPF client migration complete and validated on the target workstation. Run a one-time audit to confirm no callers pass `?sync=true`.

---

### 5. PHASES-ROADMAP.md — Stale Phase Status

| | |
|---|---|
| **File** | `Docs/PHASES-ROADMAP.md` |
| **Phase introduced** | N/A — documentation-only |

**What it does now:**
The roadmap status table shows Phase 4 (Observability & Health Monitoring) and Phase 5 (Semantic Search) as not started. Phase 6 through Phase 9 are marked complete.

**Why it is under pressure:**
- AI agents and contributors use the roadmap to understand what exists and what does not. Stale status creates misdirected work — agents may re-implement health monitoring or semantic search thinking they are greenfield tasks.
- `CURRENT-STATE.md` fully documents Phase 5 (semantic search, Qdrant, knowledge indexing) but the roadmap does not reflect this.

**Refactor direction:**
Update the status table in `PHASES-ROADMAP.md`:
- Phase 4 → ✅ Complete (health monitoring endpoints, `JobRetentionWorker`, `ProcessRunner.CheckPythonAsync`, `PingAsync` on `IModelProvider`).
- Phase 5 → ✅ Complete (EmbeddingService, VectorStoreService, KnowledgeIndexStore, semantic context builder).

Add a brief "what was done" summary for Phase 4 and Phase 5 consistent with the existing Phase 3–9 format.

**Prerequisite:** None. Documentation-only change.

---

## Low Pressure

These areas are well-understood technical debt that does not need immediate action but should be tracked.

### 6. Store JSON Export — Dropbox Compatibility Holdover

| | |
|---|---|
| **Files** | `DailyDesk/Services/TrainingStore.cs`, `DailyDesk/Services/OperatorMemoryStore.cs`, `DailyDesk.Core/Services/OfficeSessionStateStore.cs` |
| **Phase introduced** | Phase 2 (LiteDB migration) |

**What it does now:**
All three stores maintain a JSON export path alongside LiteDB storage. JSON export was kept after the Phase 2 migration to preserve Dropbox sync compatibility (`training-history.json`, `operator-memory.json`, `broker-live-session.json`).

**Why it is under pressure:**
- JSON export is a secondary write on every save, increasing write amplification.
- The export files can be modified out-of-band (e.g., manual edits in Dropbox), and the conflict resolution behavior between the JSON file and LiteDB is not explicit.
- The fallback logic (load JSON if LiteDB fails) remains but has not been exercised since the LiteDB migration stabilized.

**Refactor direction:**
Once LiteDB has been proven stable across multiple workstations:
1. Remove the on-save JSON export from each store.
2. Keep a one-time `ExportToJson()` utility method for manual snapshot/debugging.
3. Remove the `LoadFromJson` fallback path or demote it to a one-time migration-only path.

**Prerequisite:** Confirm LiteDB stability on all active workstations. Keep the export path until then.

---

### 7. WPF MainViewModel — Partial Class Growth

| | |
|---|---|
| **Files** | `DailyDesk/ViewModels/MainViewModel.cs`, `DailyDesk/ViewModels/MainViewModel.Operator.cs`, `DailyDesk/ViewModels/MainViewModel.Workflow.cs`, `DailyDesk/ViewModels/MainViewModel.OfficeChat.cs`, `DailyDesk/ViewModels/MainViewModel.OfficeDesks.cs`, `DailyDesk/ViewModels/MainViewModel.Guide.cs` |
| **Combined size** | ~4,200+ lines |
| **Phase introduced** | Phase 1 (grew through Phase 9) |

**What it does now:**
The WPF ViewModel is split across 6 partial class files. Each file handles a domain (operator memory, workflow automation, chat routing, desk selection, training guide).

**Why it is under pressure:**
- The partial class approach manages file size but does not enforce boundaries. One partial class can freely call or modify state from another, and frequently does.
- The desk-specific chat logic in `MainViewModel.OfficeDesks.cs` and `MainViewModel.OfficeChat.cs` will grow as Phase 9 async polling matures (job polling, streaming responses, status display).

**Refactor direction:**
Convert to ViewModel-per-desk as the SK agent desks mature (post Phase 9):
- `OperatorViewModel` — operator memory, inbox, suggestions.
- `StudyViewModel` — training, practice, defense, reflection.
- `ResearchViewModel` — research jobs, watchlist.
- `WorkflowViewModel` — schedules, daily-run, workflow templates.

Keep `MainViewModel` as a shell that navigates between desk ViewModels. This mirrors the SK agent desk model established in Phase 6.

**Prerequisite:** Phase 9 async integration stabilized. Defer until WPF job polling is confirmed working.

---

## Resolved Pressure (Archive)

Keep a record of pressure areas that have been resolved so contributors understand why certain patterns were adopted.

| Area | Resolved In | Resolution |
|------|------------|-----------|
| Manual JSON parsing for Ollama API | Phase 1 (PR 1.4) | Replaced with OllamaSharp typed client |
| Regex-based HTML parsing in LiveResearchService | Phase 1 (PR 1.2) | Replaced with AngleSharp DOM queries |
| No retry logic on external calls | Phase 2 (PR 2.2) | Added named Polly resilience pipelines |
| JSON file persistence with no migration | Phase 2 (PR 2.1) | Replaced with LiteDB + JSON import path |
| Blocking ML endpoints with no status visibility | Phase 3 | Added async job model with LiteDB backing |
| No job cleanup (accumulating jobs) | Phase 3 (PR 3.4, PR 6) | Added `DeleteOlderThan` + `JobRetentionWorker` |
| TF-IDF keyword search only for knowledge retrieval | Phase 5 | Added Ollama embedding + Qdrant semantic search |
| Direct LLM calls without agent structure | Phase 6 | Added SK agent desks with tool-call support |
| Text-only document extraction | Phase 7 | Added Docling pipeline with table and figure extraction |
| No scheduled automation | Phase 8 | Added cron-style `JobSchedulerStore` + `JobSchedulerWorker` |
| WPF client blocking on ML calls | Phase 9 | Added `JobPollingService` with async poll loop |
| Validators.cs flat file vs. convention | Tech Debt (chunk7) | Completed domain split: added `Validators/MLValidators.cs` and `Validators/ScheduleValidators.cs` alongside pre-existing `ChatValidators.cs` and `StudyValidators.cs`; deleted root-level `Validators.cs` |
