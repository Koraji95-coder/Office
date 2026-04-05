# Phases 3–9 Roadmap

> **Purpose:** A single document showing every phase of work for the Office repo from Phase 3 onward, broken into PRs that can be executed one at a time. Phases 1–2 are complete. This document covers Phase 3 (already implemented) through Phase 9 (future).

---

## Status Summary

| Phase | Title | Status |
|-------|-------|--------|
| 1 | Foundation (Serilog, AngleSharp, FluentValidation, OllamaSharp) | ✅ Complete |
| 2 | Persistence & Resilience (LiteDB, Polly) | ✅ Complete |
| 3 | Async Job Model (Job Store, Worker, Endpoints, Retention) | ✅ Complete |
| 4 | Observability & Health Monitoring | 🔲 Not Started |
| 5 | Semantic Search (Ollama Embeddings + Qdrant) | 🔲 Not Started |
| 6 | Agent Orchestration (Semantic Kernel) | 🔲 Not Started |
| 7 | Document Extraction (Docling) | 🔲 Not Started |
| 8 | Scheduled Automation & Operator Workflows | 🔲 Not Started |
| 9 | WPF Client Async Integration | 🔲 Not Started |

---

## Phase 3 — Async Job Model for ML Work ✅ COMPLETE

**Goal:** Move ML pipeline execution from synchronous broker endpoints to a background job system with persistent state.

### PR 3.1: Job Record Model + OfficeJobStore

**What was done:**
- Created `OfficeJob` model with fields: `Id` (GUID), `Type` (ml-analytics/ml-forecast/ml-embeddings/ml-pipeline/ml-export-artifacts), `Status` (queued/running/succeeded/failed), `CreatedAt`, `StartedAt`, `CompletedAt`, `Error`, `ResultKey`, `RequestedBy`, `RequestPayload`.
- Created `OfficeJobStore` backed by LiteDB `jobs` collection with methods:
  - `Enqueue(type, requestedBy, requestPayload)` — create a queued job.
  - `GetById(jobId)` — retrieve a job by ID.
  - `ListRecent(count)` — list most recent jobs.
  - `DequeueNext()` — atomically claim the oldest queued job.
  - `MarkSucceeded(jobId, resultKey)` — mark job as succeeded with result pointer.
  - `MarkFailed(jobId, error)` — mark job as failed with error message.

**Files touched:**
- `DailyDesk/Models/OfficeJob.cs` — new model.
- `DailyDesk/Services/OfficeJobStore.cs` — new LiteDB-backed store.

---

### PR 3.2: Background Job Worker

**What was done:**
- Created `OfficeJobWorker : BackgroundService` in `DailyDesk.Broker`.
- Worker polls LiteDB every 2 seconds for queued jobs via `DequeueNext()`.
- Executes one job at a time, dispatching to the orchestrator based on job type.
- Updates job lifecycle: `queued` → `running` → `succeeded`/`failed`.
- Stores ML results in LiteDB via `MLResultStore` (keyed by `ResultKey`).
- On failure: captures exception message in `Error` field.
- Registered as `IHostedService` in `Program.cs`.

**Files touched:**
- `DailyDesk.Broker/OfficeJobWorker.cs` — new background service.
- `DailyDesk.Broker/Program.cs` — register hosted service.

---

### PR 3.3: ML Endpoints → Async + Job Status Endpoints

**What was done:**
- Modified ML endpoints (`POST /api/ml/analytics`, `/forecast`, `/embeddings`, `/pipeline`, `/export-artifacts`) to return `{ jobId, status: "queued" }` by default.
- Added `?sync=true` query parameter for backward-compatible blocking behavior.
- Added new endpoints:
  - `GET /api/jobs` — list recent jobs (last 50).
  - `GET /api/jobs/{jobId}` — get job status and metadata.
  - `GET /api/jobs/{jobId}/result` — get job result JSON (succeeded only).

**Files touched:**
- `DailyDesk.Broker/Program.cs` — modified ML endpoints, added job endpoints.

---

### PR 3.4: ML Result Persistence (Restart-Safe)

**What was done:**
- Created `MLResultStore` that persists latest ML analytics/forecast/embeddings results to LiteDB collections (`ml_analytics`, `ml_forecast`, `ml_embeddings`).
- Uses `PersistedMLResult` wrapper with serialized JSON payload.
- `OfficeBrokerOrchestrator` restores from LiteDB on init, persists after each ML run.
- Export-artifacts endpoint is now restart-safe (survives broker restart).

**Files touched:**
- `DailyDesk/Services/MLResultStore.cs` — new persistence service.
- `DailyDesk/Models/PersistedMLResult.cs` — new wrapper model.
- `DailyDesk.Core/Services/OfficeBrokerOrchestrator.cs` — restore/persist on init and after ML runs.

---

### PR 3.5: Job Worker Hardening (Timeout + Stale Recovery)

**What was done:**
- Added per-job timeout to `OfficeJobWorker` (prevents a hanging job from blocking the worker indefinitely).
- Added `RecoverStaleJobs()` to `OfficeJobStore` — on startup, marks any jobs stuck in `running` status (from a previous crash) as `failed` with a recovery message.
- Worker calls `RecoverStaleJobs()` on initialization before processing new jobs.

**Files touched:**
- `DailyDesk.Broker/OfficeJobWorker.cs` — timeout + recovery on startup.
- `DailyDesk/Services/OfficeJobStore.cs` — `RecoverStaleJobs()` method.

---

### PR 3.6: Job Management & Retention

**What was done:**
- Added `OfficeJobStore.DeleteById(jobId)` — delete a completed (succeeded/failed) job. Queued/running jobs protected.
- Added `OfficeJobStore.DeleteOlderThan(cutoff)` — bulk-delete completed jobs older than threshold.
- Added `OfficeJobStore.ListByStatus(status, limit)` — filter jobs for monitoring.
- Added `OfficeJobStore.GetTotalCount()` — total job count for observability.
- Added `DELETE /api/jobs/{jobId}` endpoint (204/404/400 with status guard).
- Added `GET /api/jobs?status=...&type=...` — filtered listing with query params.

**Files touched:**
- `DailyDesk/Services/OfficeJobStore.cs` — 4 new methods.
- `DailyDesk.Broker/Program.cs` — new DELETE endpoint, updated GET with filters.

---

### Phase 3 Test Coverage (22 tests)

| Area | Tests | What's Covered |
|------|-------|----------------|
| Job model unit tests | 8 | Enqueue, retrieve, dequeue, mark succeeded/failed, list recent |
| Stale job recovery | 4 | Old running → failed, recent running preserved, queued/completed ignored, count |
| Job integration (PR 5) | 13 | FIFO ordering, full lifecycle, edge cases, payload round-trip, ListRecent limits, idempotent recovery, multi-iteration, dequeue skips |
| Job management (PR 6) | 9 | DeleteById across statuses, DeleteOlderThan with mixed active/expired, ListByStatus filter/limit, GetTotalCount |

---

## Phase 4 — Observability & Health Monitoring

**Goal:** Add structured health checks, metrics, and operational endpoints so you can tell at a glance whether Ollama, Python, LiteDB, and the job worker are healthy.

### PR 4.1: Health Check Endpoints

**Problem:** The only way to know if Ollama is reachable or Python is installed is to trigger an ML endpoint and wait for it to fail or fall back.

**What to do:**
- Add `GET /api/health` endpoint that returns structured status for each subsystem.
- Check Ollama reachability (HTTP ping to `/api/tags`).
- Check Python availability (`python --version` via ProcessRunner).
- Check LiteDB connectivity (open/close a test query).
- Check job worker status (is the BackgroundService running, any stuck jobs).
- Return a JSON object with per-subsystem status (`ok`/`degraded`/`unavailable`) and overall status.

**Files to touch:**
- `DailyDesk.Broker/Program.cs` — new endpoint.
- `DailyDesk/Services/OllamaService.cs` — add `PingAsync()` method behind `IModelProvider`.
- `DailyDesk/Services/ProcessRunner.cs` — add `CheckPythonAsync()` convenience method.

**Tests to add:**
- Health endpoint returns expected shape.
- Health endpoint handles Ollama unreachable gracefully.

---

### PR 4.2: Job Metrics & Dashboard Endpoint

**Problem:** No way to see job throughput, failure rates, or queue depth without querying the job list endpoint and counting manually.

**What to do:**
- Add `GET /api/jobs/metrics` endpoint returning:
  - Total jobs by status (queued, running, succeeded, failed).
  - Average job duration (succeeded jobs).
  - Jobs completed in the last hour/day.
  - Current queue depth.
- Use existing `OfficeJobStore` methods (`ListByStatus`, `GetTotalCount`) plus a new `GetMetrics()` method.
- Add `OfficeJobStore.GetAverageDuration()` — average elapsed time for succeeded jobs.

**Files to touch:**
- `DailyDesk/Services/OfficeJobStore.cs` — new `GetMetrics()` method.
- `DailyDesk.Broker/Program.cs` — new endpoint.

**Tests to add:**
- Metrics calculation with mixed job statuses.
- Metrics with empty store.

---

### PR 4.3: Automated Job Retention Cleanup

**Problem:** `DeleteOlderThan` exists but is never called automatically. Jobs accumulate until someone manually cleans up.

**What to do:**
- Add a `JobRetentionWorker : BackgroundService` that runs once per day.
- Calls `OfficeJobStore.DeleteOlderThan(DateTimeOffset.Now.AddDays(-30))` for completed jobs.
- Logs the count of deleted jobs at `Information` level.
- Make retention period configurable via `DailySettings` (default: 30 days).

**Files to touch:**
- `DailyDesk.Broker/JobRetentionWorker.cs` — new file.
- `DailyDesk.Broker/Program.cs` — register the hosted service.
- `DailyDesk/Models/DailySettings.cs` — add `JobRetentionDays` property (default 30).

**Tests to add:**
- Retention worker deletes old completed jobs.
- Retention worker preserves recent completed jobs.
- Retention worker ignores queued/running jobs.

---

## Phase 5 — Semantic Search (Ollama Embeddings + Qdrant)

**Goal:** Replace the TF-IDF/keyword fallback in `KnowledgePromptContextBuilder` with real vector embeddings for semantic search across the knowledge library.

**Prerequisite:** Phase 3 complete (async jobs exist to generate embeddings).

### PR 5.1: Ollama Embeddings via OllamaSharp

**Problem:** Document embeddings are generated by `ml_document_embeddings.py` (PyTorch subprocess). This is slow, requires PyTorch installed, and stores embeddings in a flat JSON file that can't be searched.

**What to do:**
- Add `EmbeddingService` in `DailyDesk/Services/` that calls Ollama's `/api/embeddings` endpoint via OllamaSharp.
- Use an embedding model (e.g., `nomic-embed-text` or `mxbai-embed-large`).
- Accept a text string, return a `float[]` vector.
- Wrap in the `ollama` Polly resilience pipeline.
- Add heuristic fallback (return null if Ollama is unavailable — caller handles it).

**Files to touch:**
- `DailyDesk/Services/EmbeddingService.cs` — new file.
- `DailyDesk/Services/IModelProvider.cs` — add optional `GenerateEmbeddingAsync()` method (or create separate `IEmbeddingProvider` interface).
- `DailyDesk.Core/Services/OfficeBrokerOrchestrator.cs` — wire up.

**Tests to add:**
- EmbeddingService returns vector of expected dimension.
- EmbeddingService falls back gracefully when Ollama is unavailable.

---

### PR 5.2: Qdrant Local Vector Store

**Problem:** There is no persistent vector store. Embeddings are generated and thrown away. Semantic search requires comparing against all documents every time.

**What to do:**
- Add `Qdrant.Client` NuGet to `DailyDesk.Core.csproj`.
- Create `VectorStoreService` in `DailyDesk/Services/` that wraps Qdrant client.
- Methods: `UpsertAsync(docId, vector, metadata)`, `SearchAsync(queryVector, topK)`, `DeleteAsync(docId)`, `GetCollectionInfoAsync()`.
- Create collection `office-knowledge` on first use.
- Qdrant runs as local Docker container (document setup in README).
- Add graceful fallback: if Qdrant is unreachable, return empty results (existing TF-IDF fallback continues to work).

**Files to touch:**
- `DailyDesk/Services/VectorStoreService.cs` — new file.
- `DailyDesk.Core.csproj` — add `Qdrant.Client` NuGet.
- `Docs/LIBRARY-DECISIONS.md` — document Qdrant decision.
- `README.md` — add Qdrant Docker setup instructions.

**Tests to add:**
- VectorStoreService handles connection failure gracefully.
- Upsert and search round-trip (may need integration test with Docker).

---

### PR 5.3: Knowledge Indexing Job

**Problem:** Knowledge documents are only processed when explicitly imported. There's no background indexing.

**What to do:**
- Add new job type `knowledge-index` to `OfficeJobType`.
- When triggered (via `POST /api/ml/index-knowledge` or as part of the pipeline):
  1. Scan all documents in the knowledge library.
  2. For each document not yet indexed (or modified since last index), generate embedding via `EmbeddingService`.
  3. Upsert embedding + metadata into Qdrant via `VectorStoreService`.
  4. Track indexed document hashes in LiteDB to avoid re-indexing.
- Add `GET /api/knowledge/index-status` — show how many documents are indexed vs. total.

**Files to touch:**
- `DailyDesk/Models/OfficeJob.cs` — add `KnowledgeIndex` type.
- `DailyDesk.Broker/OfficeJobWorker.cs` — add handler.
- `DailyDesk.Broker/Program.cs` — new endpoints.
- `DailyDesk/Services/KnowledgeIndexStore.cs` — new file (tracks indexed docs in LiteDB).

**Tests to add:**
- Indexing job creates entries in tracking store.
- Re-indexing skips unchanged documents.
- Index status endpoint returns correct counts.

---

### PR 5.4: Semantic Knowledge Search

**Problem:** `KnowledgePromptContextBuilder` uses keyword matching to find relevant documents for context. This misses semantic relationships.

**What to do:**
- Modify `KnowledgePromptContextBuilder` to:
  1. Generate embedding for the user's query via `EmbeddingService`.
  2. Search Qdrant via `VectorStoreService.SearchAsync(queryVector, topK: 5)`.
  3. Fall back to existing keyword/TF-IDF search if Qdrant is unavailable.
  4. Merge results: Qdrant results first, then keyword results to fill remaining slots.
- The rest of the prompt composition pipeline stays the same.

**Files to touch:**
- `DailyDesk/Services/KnowledgePromptContextBuilder.cs` — add semantic search path.
- `DailyDesk.Core/Services/OfficeBrokerOrchestrator.cs` — inject `EmbeddingService` and `VectorStoreService`.

**Tests to add:**
- KnowledgePromptContextBuilder falls back to keyword search when Qdrant is unavailable.
- Semantic search results are preferred over keyword results.

---

## Phase 6 — Agent Orchestration (Semantic Kernel)

**Goal:** Replace hand-rolled prompt composition with Semantic Kernel agents that have tool-calling capabilities.

**Prerequisite:** Phase 5 complete (semantic search exists) + stable tool/plugin boundary.

### PR 6.1: Semantic Kernel Core Integration

**Problem:** `PromptComposer` manually concatenates system prompts, context, and user input. There's no support for tool calling, function chaining, or multi-turn memory beyond thread state.

**What to do:**
- Add `Microsoft.SemanticKernel` NuGet to `DailyDesk.Core.csproj`.
- Create `OfficeKernelFactory` in `DailyDesk/Services/` that builds an SK `Kernel` configured for the local Ollama endpoint.
- Register Ollama as a chat completion provider in the kernel (via OllamaSharp connector or OpenAI-compatible endpoint).
- Create a base `DeskAgent` class that wraps an SK `ChatCompletionAgent` with:
  - A system prompt (from existing `PromptComposer` templates).
  - Tool registration mechanism.
  - Conversation history tracking.

**Files to touch:**
- `DailyDesk/Services/OfficeKernelFactory.cs` — new file.
- `DailyDesk/Services/DeskAgent.cs` — new file.
- `DailyDesk.Core.csproj` — add Semantic Kernel NuGet.
- `Docs/LIBRARY-DECISIONS.md` — document SK decision.

**Tests to add:**
- OfficeKernelFactory creates a configured kernel.
- DeskAgent generates response with system prompt.

---

### PR 6.2: Desk-Specific Agents

**Problem:** All five desk routes share the same code path (prompt composition → Ollama → response). There's no desk-specific tooling or behavior.

**What to do:**
- Create five agent classes, one per desk route:
  - `ChiefOfStaffAgent` — tools: get state, list jobs, get schedule.
  - `EngineeringDeskAgent` — tools: start practice test, run defense, get training history.
  - `SuiteContextAgent` — tools: get Suite snapshot, get library docs.
  - `GrowthOpsAgent` — tools: get operator memory, get suggestions.
  - `MLEngineerAgent` — tools: trigger ML pipeline, get analytics, get forecast.
- Each agent registers its tools as SK functions.
- Replace `PromptComposer.ComposeChat()` → agent dispatch based on route.
- Keep `IModelProvider` as the LLM backend (agents call through the kernel which uses Ollama).

**Files to touch:**
- `DailyDesk/Services/Agents/` — new folder with 5 agent files.
- `DailyDesk/Services/PromptComposer.cs` — gradually replaced (keep as fallback initially).
- `DailyDesk.Core/Services/OfficeBrokerOrchestrator.cs` — agent dispatch in chat methods.
- `DailyDesk.Broker/Program.cs` — register agents in DI.

**Tests to add:**
- Each agent responds to a basic prompt.
- Agent tools are callable and return expected types.
- Agent dispatch routes correctly based on route name.

---

### PR 6.3: Multi-Turn Agent Memory

**Problem:** Chat conversations are stateless per request. The desk thread state tracks messages but doesn't provide context window management or summarization.

**What to do:**
- Add conversation memory to each `DeskAgent` using SK's `ChatHistory`.
- On each chat request, load the desk thread state into `ChatHistory`.
- Implement context window management:
  - Keep the last N messages in full.
  - Summarize older messages into a condensed context block.
  - Use Ollama to generate summaries (via the kernel).
- Persist updated thread state back to `DeskThreadState` in LiteDB.

**Files to touch:**
- `DailyDesk/Services/DeskAgent.cs` — add memory management.
- `DailyDesk/Models/DeskThreadState.cs` — add summary field.
- `DailyDesk/Services/OfficeDatabase.cs` — add `desk_threads` collection if not present.

**Tests to add:**
- Agent maintains context across multiple turns.
- Old messages are summarized when context window is exceeded.
- Thread state persists to LiteDB.

---

## Phase 7 — Document Extraction (Docling)

**Goal:** Replace the basic `extract_document_text.py` with Docling for richer document extraction (tables, images, OCR).

**Prerequisite:** None (can be done at any phase, but benefits from Phase 5 indexing).

### PR 7.1: Docling Python Integration

**Problem:** `extract_document_text.py` uses `pypdf` for PDFs (text-only, no tables) and basic text reading for other formats. PowerPoint extraction is claimed but not fully implemented. No OCR for scanned documents.

**What to do:**
- Replace `extract_document_text.py` with a Docling-based script.
- Docling handles: PDF (with tables and figures), DOCX, PPTX, HTML, images (OCR).
- Output format stays the same: JSON to stdout with `{ text, metadata }`.
- Keep the same `ProcessRunner` subprocess model.
- Keep heuristic fallback: if Docling is not installed, fall back to the existing basic extraction.

**Files to touch:**
- `DailyDesk/Scripts/extract_document_text.py` — rewrite internals, keep CLI interface.
- `README.md` — add Docling setup instructions (`pip install docling`).

**Tests to add:**
- Document extraction produces expected output format.
- Fallback works when Docling is not installed.

---

### PR 7.2: Table and Figure Extraction

**Problem:** PDF tables are extracted as garbled text. Figures are ignored entirely.

**What to do:**
- Extend the extraction output format to include structured tables (as JSON arrays) and figure descriptions.
- Modify `KnowledgeImportService` to handle the richer output:
  - Tables → formatted markdown tables in the document text.
  - Figures → alt-text descriptions appended to the document.
- Update `LearningDocument` model to include optional `tables` and `figures` fields.

**Files to touch:**
- `DailyDesk/Scripts/extract_document_text.py` — add table/figure output.
- `DailyDesk/Services/KnowledgeImportService.cs` — handle richer output.
- `DailyDesk/Models/LearningDocument.cs` — add optional fields.

**Tests to add:**
- Table extraction produces valid markdown.
- Figure descriptions are included in document text.
- Backward compatibility with old extraction format.

---

## Phase 8 — Scheduled Automation & Operator Workflows

**Goal:** Add scheduled job execution and operator-defined workflows so the system can run ML pipelines, knowledge indexing, and maintenance tasks automatically.

### PR 8.1: Cron-Style Job Scheduler

**Problem:** All ML pipeline runs and knowledge indexing must be triggered manually via API. There's no way to schedule recurring work.

**What to do:**
- Create `JobSchedulerService` in `DailyDesk/Services/` that manages scheduled job definitions.
- Store schedules in LiteDB `job_schedules` collection.
- Each schedule defines: job type, cron expression (or simple interval), enabled/disabled, last run time.
- `JobSchedulerWorker : BackgroundService` checks schedules every minute and enqueues jobs via `OfficeJobStore.Enqueue()`.
- Add endpoints:
  - `GET /api/schedules` — list all schedules.
  - `POST /api/schedules` — create a schedule.
  - `PUT /api/schedules/{id}` — update a schedule (enable/disable, change interval).
  - `DELETE /api/schedules/{id}` — remove a schedule.

**Files to touch:**
- `DailyDesk/Models/JobSchedule.cs` — new model.
- `DailyDesk/Services/JobSchedulerStore.cs` — new LiteDB-backed store.
- `DailyDesk.Broker/JobSchedulerWorker.cs` — new background service.
- `DailyDesk.Broker/Program.cs` — register service, add endpoints.
- `DailyDesk.Broker/Validators.cs` — add schedule validators.

**Tests to add:**
- Schedule CRUD operations.
- Scheduler enqueues job at correct time.
- Disabled schedule is skipped.
- Schedule handles cron parsing correctly.

---

### PR 8.2: Daily Run Automation

**Problem:** The `DailyRunTemplate` model exists but there's no automated daily workflow. The operator must manually trigger the pipeline, review suggestions, and export artifacts.

**What to do:**
- Create a `daily-run` job type that orchestrates a full daily workflow:
  1. Refresh state (Ollama models, Suite snapshot, training history, knowledge library).
  2. Run ML pipeline (analytics, forecast, embeddings in parallel).
  3. Export Suite artifacts.
  4. Generate operator suggestions based on ML results.
  5. Log the daily run summary to `OperatorMemoryStore`.
- Wire to the job scheduler so it runs automatically (e.g., every morning at 8 AM).
- Add `GET /api/daily-run/latest` — show the most recent daily run summary.

**Files to touch:**
- `DailyDesk/Models/OfficeJob.cs` — add `DailyRun` job type.
- `DailyDesk.Broker/OfficeJobWorker.cs` — add daily run handler.
- `DailyDesk.Core/Services/OfficeBrokerOrchestrator.cs` — add `RunDailyWorkflowAsync()`.
- `DailyDesk.Broker/Program.cs` — new endpoint.

**Tests to add:**
- Daily run job completes all steps.
- Daily run handles individual step failures gracefully.
- Daily run summary is persisted.

---

### PR 8.3: Operator Workflow Templates

**Problem:** The operator can't define custom workflows beyond the built-in daily run. Different scenarios (exam prep week, project deadline, knowledge refresh) need different workflow compositions.

**What to do:**
- Create `WorkflowTemplate` model: name, description, ordered list of steps (job types + parameters).
- Store templates in LiteDB `workflow_templates` collection.
- Add endpoints:
  - `GET /api/workflows` — list templates.
  - `POST /api/workflows` — create template.
  - `POST /api/workflows/{id}/run` — execute a template (enqueues each step as a job).
  - `DELETE /api/workflows/{id}` — remove template.
- Built-in templates: "Daily Run", "Exam Prep", "Knowledge Refresh".

**Files to touch:**
- `DailyDesk/Models/WorkflowTemplate.cs` — new model.
- `DailyDesk/Services/WorkflowStore.cs` — new LiteDB-backed store.
- `DailyDesk.Broker/Program.cs` — new endpoints.
- `DailyDesk.Broker/Validators.cs` — add workflow validators.

**Tests to add:**
- Workflow template CRUD.
- Workflow execution enqueues jobs in order.
- Workflow handles step failure (continue vs. abort policy).

---

## Phase 9 — WPF Client Async Integration

**Goal:** Update the WPF desktop client to use the async job model and semantic search instead of blocking API calls.

### PR 9.1: Job Polling in ViewModels

**Problem:** The WPF client calls ML endpoints and blocks the UI thread waiting for results. With the async job model, it should submit and poll.

**What to do:**
- Add `JobPollingService` to WPF that:
  - Submits ML requests (gets back job ID).
  - Polls `GET /api/jobs/{jobId}` every 2 seconds.
  - Updates ViewModel properties when job completes.
  - Shows progress indicator while job is running.
- Update ML-related ViewModels to use polling instead of blocking calls.

**Files to touch:**
- `DailyDesk/Services/JobPollingService.cs` — new file (WPF-specific, not linked to Core).
- `DailyDesk/ViewModels/` — update ML-related ViewModels.

**Tests to add:**
- Polling service correctly transitions through job states.
- ViewModel updates on job completion.

---

### PR 9.2: Semantic Search in Knowledge View

**Problem:** The knowledge browser uses basic text search. With Qdrant semantic search available via the broker, the WPF client should offer semantic search.

**What to do:**
- Add a search box to the knowledge view that calls `POST /api/knowledge/search` (or the existing embeddings endpoint with a query).
- Display results ranked by semantic relevance.
- Show similarity scores.
- Fall back to text search if the broker reports Qdrant is unavailable.

**Files to touch:**
- `DailyDesk/ViewModels/` — update knowledge view model.
- `DailyDesk/Views/` — update knowledge XAML.

**Tests to add:**
- Search view handles empty results.
- Search view handles broker unavailable.

---

### PR 9.3: Agent Chat with Tool Feedback

**Problem:** The chat interface shows plain text responses. With SK agents having tool-calling capabilities (Phase 6), the UI should show when tools are being used and their results.

**What to do:**
- Extend the chat response format to include tool invocation metadata.
- Update the chat view to show:
  - Tool calls as expandable cards (e.g., "📊 Retrieved training history").
  - Tool results as formatted data.
  - Agent thinking/reasoning steps.
- Keep backward compatibility with plain text responses.

**Files to touch:**
- `DailyDesk/ViewModels/` — update chat view model.
- `DailyDesk/Views/` — update chat XAML.
- `DailyDesk/Models/DeskMessageRecord.cs` — add optional tool metadata.

**Tests to add:**
- Chat view renders tool calls correctly.
- Chat view handles plain text responses (no tools).

---

## Quick Reference: PR Execution Order

| PR | Phase | Title | Dependencies |
|----|-------|-------|-------------|
| 3.1 | 3 | Job Record Model + OfficeJobStore | Phase 2 (LiteDB) |
| 3.2 | 3 | Background Job Worker | 3.1 |
| 3.3 | 3 | ML Endpoints → Async + Job Status Endpoints | 3.1, 3.2 |
| 3.4 | 3 | ML Result Persistence (Restart-Safe) | 3.1 |
| 3.5 | 3 | Job Worker Hardening (Timeout + Stale Recovery) | 3.2 |
| 3.6 | 3 | Job Management & Retention | 3.1 |
| 4.1 | 4 | Health Check Endpoints | None |
| 4.2 | 4 | Job Metrics & Dashboard Endpoint | None |
| 4.3 | 4 | Automated Job Retention Cleanup | None |
| 5.1 | 5 | Ollama Embeddings via OllamaSharp | None |
| 5.2 | 5 | Qdrant Local Vector Store | 5.1 |
| 5.3 | 5 | Knowledge Indexing Job | 5.1, 5.2 |
| 5.4 | 5 | Semantic Knowledge Search | 5.1, 5.2, 5.3 |
| 6.1 | 6 | Semantic Kernel Core Integration | None |
| 6.2 | 6 | Desk-Specific Agents | 6.1 |
| 6.3 | 6 | Multi-Turn Agent Memory | 6.1, 6.2 |
| 7.1 | 7 | Docling Python Integration | None |
| 7.2 | 7 | Table and Figure Extraction | 7.1 |
| 8.1 | 8 | Cron-Style Job Scheduler | None |
| 8.2 | 8 | Daily Run Automation | 8.1 |
| 8.3 | 8 | Operator Workflow Templates | 8.1, 8.2 |
| 9.1 | 9 | Job Polling in ViewModels | None (uses existing job API) |
| 9.2 | 9 | Semantic Search in Knowledge View | 5.4 |
| 9.3 | 9 | Agent Chat with Tool Feedback | 6.2 |

**Total: 24 PRs across 7 phases (Phase 3: 6 PRs ✅ complete, Phases 4–9: 18 PRs remaining).**

Phase 3 PRs are sequential (each builds on the previous).
Phase 4 PRs are independent of each other and can be done in any order.
Phases 5 and 6 are independent of each other (can be parallelized).
Phase 7 is independent of all others.
Phase 8 depends on Phase 3 (already complete).
Phase 9 depends on Phases 5 and 6 for full functionality but PR 9.1 can be done immediately.
