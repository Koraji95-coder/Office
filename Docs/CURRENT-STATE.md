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

**Where:** `DailyDesk.Broker/Program.cs` — 15 `record` types defined at lines 632–651.

All broker request types are `sealed record` with init-only properties. This makes them ideal targets for FluentValidation — validators can target the record type directly without needing mutable state.

### 8. State Normalization on Load

**Where:** `OfficeSessionStateStore.Normalize()`, `OperatorMemoryStore.NormalizeState()`.

Both stores normalize state after loading from JSON:
- Default values for missing properties.
- Route normalization via `OfficeRouteCatalog.NormalizeRoute()`.
- Numeric clamping (`Math.Clamp`).
- Legacy data migration (`HydrateLegacyPracticeAttempts`).

**Implication:** LiteDB migration (Phase 2) can use the same normalization logic. On first LiteDB load, import from JSON and normalize.

### 9. Consistent Error Response Pattern

**Where:** `DailyDesk.Broker/Program.cs` — all 25 endpoints follow the same pattern:

```
try { ... Results.Ok(...) }
catch (ArgumentException) { Results.BadRequest({ error }) }
catch (InvalidOperationException) { Results.BadRequest({ error }) }
catch (Exception) { logger.LogError(); Results.Problem() }
```

**Implication:** FluentValidation (Phase 1) adds a validation step before the try block. The catch pattern stays the same for orchestrator-level errors.

### 10. Shared Code via Compile-Link

**Where:** `DailyDesk.Core.csproj` — `<Compile Include="..\DailyDesk\Models\**\*.cs" Link="Models\%(RecursiveDir)%(Filename)%(Extension)" />`.

Models and Services from `DailyDesk/` are compiled into `DailyDesk.Core` via linked compilation. This means:
- NuGet packages added to `DailyDesk.Core.csproj` are available to both the WPF app and the Broker.
- New libraries (AngleSharp, OllamaSharp, LiteDB, Polly) added to Core are automatically available everywhere.

---

## Current Dependencies (Minimal)

| Project | NuGet Packages |
|---------|---------------|
| `DailyDesk` | None (project ref to Core only) |
| `DailyDesk.Core` | `Microsoft.ML.OnnxRuntime` 1.24.4 |
| `DailyDesk.Broker` | None (inherits from SDK Web, project ref to Core) |
| `DailyDesk.Core.Tests` | `xunit` 2.9.3, `xunit.runner.visualstudio` 2.8.2, `Microsoft.NET.Test.Sdk` 17.14.1, `coverlet.collector` 6.0.4 |

**Implication:** The codebase has a deliberately minimal dependency footprint. Each new library should be justified by a specific problem it solves. Do not add libraries speculatively.

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
| Training history | `%LOCALAPPDATA%\DailyDesk\training-history.json` | JSON |
| Operator memory | `%LOCALAPPDATA%\DailyDesk\operator-memory.json` | JSON |
| Session state | `%LOCALAPPDATA%\DailyDesk\broker-live-session.json` | JSON |
| Knowledge library | `%USERPROFILE%\Dropbox\SuiteWorkspace\Office\Knowledge` | Mixed (md, txt, pdf, docx) |
| State root | `%USERPROFILE%\Dropbox\SuiteWorkspace\Office\State` | Mixed |
| ML artifacts | `State/ml-artifacts/` | JSON |
| Python scripts | `DailyDesk/Scripts/` | Python |
| ONNX models | `DailyDesk/Models/onnx/` | ONNX |
