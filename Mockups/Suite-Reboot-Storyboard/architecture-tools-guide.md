# Architecture Tools Guide

> **Scope:** Developer Portal — Architecture and code group  
> **Status:** Future-state storyboard tools. These surfaces are accessible from the Developer Portal but live on their own focused tool pages.

---

## Overview

The Developer Portal's **Architecture and code** group exposes two visual tools — **Architecture Map** and **Architecture Graph** — alongside **Refactor pressure notes**. These tools are intended for developers who need to understand, navigate, or extend the Office and Suite codebase structure without leaving the portal workflow.

The portal provides a launcher tile for each tool. The tools themselves open as focused tool-detail pages that stay separate from the portal overview.

---

## Architecture Map

### Purpose

The Architecture Map is a spatial, layer-oriented view of the Office system. It shows the major service layers, their deployment boundaries, and how they relate at a high level. The map is read-oriented — it answers "where does this service live and what layer owns it?" without requiring the viewer to read source code.

### What it displays

| Layer | Contents |
|-------|----------|
| WPF application | `DailyDesk` — the operator desktop client |
| Broker service | `DailyDesk.Broker` — ASP.NET Core localhost API on port 57420 |
| Core library | `DailyDesk.Core` — shared models and services compiled into both broker and test targets |
| State storage | LiteDB (`office.db`) and Dropbox sync paths for knowledge, training, and session state |
| External subsystems | Ollama (local LLM), Qdrant (vector store), Python subprocesses (ML pipeline) |
| Suite integration | Artifact export path (`State/ml-artifacts/`) consumed by Suite's deterministic workflows |

### Navigation

1. Open **Developer Portal** from Runtime Control or the Suite sidebar.
2. Locate the **Architecture and code** group (bottom-center of the launcher grid).
3. Click **Architecture Map** to open the tool-detail page.
4. The tool-detail page shows the full layer diagram with service names, connection types, and trust-state indicators.
5. Use the inspector panel on the right to see design rationale and extension notes for any selected layer.

### How to extend

- Adding a new service layer: add an entry to the map's data layer that includes the service name, owning project, and connection type (HTTP, IPC, subprocess, file).
- Adding a new external dependency: record it in the External subsystems row, noting whether it is optional (fallback exists) or required.
- The map does not auto-generate from source. It is a maintained design artifact. Update it when a new project is added to the solution or when a major service boundary changes.

---

## Architecture Graph

### Purpose

The Architecture Graph is a directed-relationship view of the codebase. Where the map shows layers, the graph shows dependencies — which service calls which, which model is owned by which store, and how data flows from an operator action through the broker to a background job and back. The graph answers "how does this component connect to the rest of the system?"

### What it displays

| Node type | Examples |
|-----------|---------|
| Services | `OllamaService`, `KnowledgeImportService`, `VectorStoreService`, `OfficeJobWorker` |
| Stores | `TrainingStore`, `OperatorMemoryStore`, `OfficeJobStore`, `KnowledgeIndexStore` |
| Orchestrators | `OfficeBrokerOrchestrator` |
| Agents | `ChiefOfStaffAgent`, `EngineeringDeskAgent`, `SuiteContextAgent`, `GrowthOpsAgent`, `MLEngineerAgent` |
| External calls | Ollama `/api/chat`, Qdrant gRPC, Python subprocess, web research HTTP |
| Broker endpoints | `/api/chat/*`, `/api/ml/*`, `/api/jobs/*`, `/api/knowledge/*` |

### Navigation

1. Open **Developer Portal** and click **Architecture Graph** in the Architecture and code group.
2. The graph opens in its own tool-detail page with a filterable node list on the left and the graph canvas on the right.
3. Click any node to highlight its direct dependencies (outbound edges) and dependents (inbound edges).
4. Use the **filter by type** control to isolate services, stores, agents, or external calls.
5. Use the inspector to see the source file, public interface, and design notes for the selected node.

### How to extend

- New service or store: add a node entry with the class name, project path, and its direct dependencies. Include whether it has a fallback path.
- New broker endpoint: add an edge from the endpoint node to the orchestrator method it calls.
- The graph is a maintained design artifact that mirrors `DailyDesk.Core` and `DailyDesk.Broker` structure. Regeneration from source code can be scripted, but the canonical version in the portal is the reviewed, annotated one.

---

## Relationship between the two tools

| Question | Tool to use |
|----------|-------------|
| What layer does a service live in? | Architecture Map |
| Which services does a given class call? | Architecture Graph |
| Is an external dependency required or optional? | Architecture Map |
| What is the data flow for a specific feature? | Architecture Graph |
| Where does Suite integration fit in the system? | Architecture Map |
| How many hops exist between a broker endpoint and the LLM? | Architecture Graph |

Both tools live in the Developer Portal's Architecture and code group. Neither is intended to expose customer-visible content or appear in customer-facing routes.

---

## Refactor pressure notes

The third item in the Architecture and code group is **Refactor pressure notes**. These are short, developer-authored notes that flag areas of the codebase that have accumulated technical debt or have known extension points. Refactor pressure notes are not automated — they are written by developers during code review or after a session in the repo coach. Notes link to a source file or orchestrator method and include a one-line description of the pressure and a suggested next action.
