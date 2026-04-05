# Daily Office

Separate repo for the `Office` desktop app (`DailyDesk`) and its local knowledge/mockup assets.

## Layout

- `DailyDesk/`: WPF application source
- `DailyDesk.Broker/`: ASP.NET Core web service broker (localhost:57420)
- `DailyDesk.Core/`: Shared business logic & models library
- `DailyDesk.Core.Tests/`: Unit tests (xUnit)
- `Knowledge/`: repo-owned knowledge and seed content
- `Mockups/`: UI mockups and experiments

## Agent Desks

Office includes five Ollama-powered agent routes, each with its own personality and focus:

| Route | Title | Purpose |
|-------|-------|---------|
| `chief` | Chief of Staff | Routes the day across Suite, engineering, CAD, and growth |
| `engineering` | Engineering Desk | EE coaching, CAD workflow judgment, practice tests, oral defense |
| `suite` | Suite Context | Read-only awareness of Suite repo, trust, and runtime signals |
| `business` | Growth Ops | Monetization discipline, offers, career proof |
| `ml` | ML Engineer | Machine learning insights, forecasts, and Suite-ready artifacts |

## ML Pipeline

Office includes a local machine learning pipeline that analyzes training data and produces actionable insights. The pipeline runs Python scripts as subprocesses and falls back to heuristic analysis when ML libraries are not installed.

### ML Engines

| Engine | Library | Purpose |
|--------|---------|---------|
| Learning Analytics | Scikit-learn | Topic clustering, readiness prediction, adaptive study scheduling, operator pattern classification |
| Document Embeddings | PyTorch | Semantic embeddings for knowledge library, document similarity, relevance-ranked search |
| Progress Forecast | TensorFlow | Time-series accuracy forecasting, plateau detection, anomaly alerts, mastery estimation |

### Suite Integration Artifacts

The ML pipeline produces versioned artifacts that Suite can consume through its deterministic workflows:

- **operator-readiness**: Skill readiness signals for project task assignment
- **knowledge-index**: Semantic document index for Suite's standards checker
- **study-schedule**: Adaptive study plan for Suite's project timeline
- **watchdog-baseline**: Anomaly detection baselines for Suite's watchdog telemetry

Artifacts are exported to `State/ml-artifacts/` and follow Suite's review-first design philosophy.

### ML Setup (Optional)

The ML pipeline works without any Python ML libraries installed (uses heuristic fallbacks). For full ML capability:

```powershell
pip install scikit-learn torch tensorflow
```

Enable the pipeline in `dailydesk.settings.json`:

```json
{
  "enableMLPipeline": true
}
```

### ML Broker Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/ml/analytics` | Run Scikit-learn learning analytics |
| POST | `/api/ml/forecast` | Run TensorFlow progress forecast |
| POST | `/api/ml/embeddings` | Run PyTorch document embeddings (optional query) |
| POST | `/api/ml/pipeline` | Run full ML pipeline (all three engines + artifact export) |
| POST | `/api/ml/export-artifacts` | Export Suite integration artifacts |
| POST | `/api/ml/index-knowledge` | Index knowledge documents into vector store (async, `?sync=true` for blocking) |
| GET | `/api/knowledge/index-status` | Get knowledge index status (indexed vs. total) |

## Qdrant Setup (Phase 5 — Semantic Search)

Semantic search requires a local Qdrant vector database. Run Qdrant as a Docker container:

```bash
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 \
  -v qdrant_storage:/qdrant/storage \
  qdrant/qdrant
```

Qdrant is **optional** — all semantic search features fall back gracefully to keyword search when Qdrant is unavailable.

## Local Roots

Recommended workstation path:

```text
C:\Users\<you>\Documents\GitHub\Office
```

`Suite Runtime Control` resolves Office from workstation-local config first, then `C:\Users\<you>\Documents\GitHub\Office`. Office live knowledge/state now belong under Dropbox:

- `%USERPROFILE%\Dropbox\SuiteWorkspace\Office\Knowledge`
- `%USERPROFILE%\Dropbox\SuiteWorkspace\Office\State`

## GitHub Remote Setup

After you create the GitHub repo, wire this local repo to it:

```powershell
git remote add origin https://github.com/Koraji95-coder/Office.git
git push -u origin main
```

## Other Workstation Setup

On the other PC, clone this repo directly into the standard path:

```powershell
git clone https://github.com/Koraji95-coder/Office.git C:\Users\<you>\Documents\GitHub\Office
```

Then clone `Suite` into `C:\Users\<you>\Documents\GitHub\Suite` and run Suite's workstation bootstrap from the `Suite` repo. If both repos are already in their standard roots, Suite does not need a `-DailyRepoUrl` argument.

## Build

```powershell
cd DailyDesk
dotnet build
```

## Run

```powershell
cd DailyDesk
dotnet run
```

## Test

```powershell
dotnet test DailyDesk.Core.Tests
```

## Relationship To Suite

- `Suite` stays in its own repo.
- `Daily Office` stays in this repo.
- `Suite Runtime Control` lives in `Suite` and launches the built Office executable from the workstation-local path.
- Office's ML pipeline produces artifacts that Suite can consume through its deterministic, review-first workflows.
- Suite does **not** host an agent product surface. Office owns local chat, orchestration, and operator-assistant work.
