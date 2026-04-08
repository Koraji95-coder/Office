# Copilot Instructions

## Repo Purpose

This repo is an **ML-powered PR scoring and training pipeline**. Every file either directly supports the ML scoring pipeline or documents how it works.

## What This Repo Is

- `scripts/auto-pr-review.ps1` — live PR scoring engine using Ollama
- `scripts/scoring/` — historical replay, training data pull, schema validation, and model retraining
- `scripts/rag/` — RAG indexer and query system for context retrieval
- `schemas/feature-v1.json` — training feature schema for the scoring model
- `DailyDesk/` — WPF operator UI with five Ollama-powered agent desks
- `DailyDesk.Broker/` — ASP.NET Core broker (localhost:57420) for async ML job dispatch
- `DailyDesk.Core/` — shared ML pipeline services and models
- `DailyDesk.Core.Tests/` — xUnit tests for the ML pipeline

## What To Work On

Only open PRs and make changes that directly support the ML scoring pipeline or the operator infrastructure. Acceptable PR topics include:

- Fixing bugs or improving reliability in `auto-pr-review.ps1`
- Improving the scoring model training pipeline (`scripts/scoring/`)
- Improving RAG index quality or query accuracy (`scripts/rag/`)
- Adding or fixing ML pipeline services in `DailyDesk.Core/`
- Adding tests for the ML pipeline, broker endpoints, or scoring logic
- Updating documentation to reflect the ML pipeline architecture
- Fixing security issues in broker endpoints
- Improving the async job model (enqueue, dequeue, retry, retention)
- Improving Ollama integration or embedding quality

## What NOT To Work On

Do **not** open PRs or make changes related to:

- graduation paths, graduation workflows, or any school/educational concepts
- Suite-Reboot-Storyboard, storyboards, or UI mockups
- customer-safe copy, customer app guidelines, or customer-facing content
- Reference Library Control or reference library management
- electrical QA/QC checklists, electrical drafting workflows, or drawing review
- oral defense scoring, practice tests, or study session logic beyond what exists
- FluentValidation tests for school or electrical endpoints
- AGENT_REPLY_GUIDE chunks about school workflows or electrical approval routing
- audit trail tests for non-ML workflows
- DailyDesk NuGet audit unrelated to the ML pipeline

## Language and Style

- C# for broker, core services, and WPF code
- Python for ML training scripts and RAG scripts
- PowerShell for pipeline automation scripts
- Follow patterns in `Docs/CONVENTIONS.md`
- All tests go in `DailyDesk.Core.Tests/` using xUnit
- Tests must pass on Linux (`dotnet test DailyDesk.Core.Tests`)
- One concern per PR — do not mix unrelated changes

## Architecture

The scoring pipeline flows:

```
auto-pr-review.ps1  →  Ollama LLM scoring  →  feature-v1.json  →  retrain.py
        ↑                                                                ↓
    RAG context (rag/)                                        scoring model artifacts
```

The broker handles async ML jobs:

```
POST /api/ml/*  →  OfficeJobStore (queued)  →  OfficeJobWorker  →  MLAnalyticsService / Python
```

See `Docs/ARCHITECTURE.md` for the full phase-by-phase implementation history.
