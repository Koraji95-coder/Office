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

The third item in the Architecture and code group is **Refactor pressure notes**. These are short, developer-authored notes that flag areas of the codebase that have accumulated technical debt or have known extension points. Refactor pressure notes are not automated — they are written by developers during code review or after a session in the repo coach. Notes link to a source file or orchestrator method and include a description of the pressure, the suggested next action (refactor direction), and the prerequisite for acting.

### Navigation

1. Open **Developer Portal** from Runtime Control or the Suite sidebar.
2. Locate the **Architecture and code** group (bottom-center of the launcher grid).
3. Click **Refactor pressure notes** to open the tool-detail page.
4. The tool-detail page lists all active notes grouped by priority (High, Medium, Low) with links to the canonical source document.
5. Use the priority filter to focus on High pressure items first.

### Canonical source

The authoritative record of all refactor pressure notes is `Docs/REFACTOR-PRESSURE.md`. Every note in the portal view mirrors an entry in that document. To add, update, or resolve a note, edit `REFACTOR-PRESSURE.md` directly. The portal tool-detail page is a read-oriented view of the same content and does not auto-generate from source code.

### Note format

Each pressure note is a numbered entry under one of the priority sections and contains the following required sections:

| Section | Contents |
|---------|----------|
| **File** or **Files** | Repo-relative path(s) to the affected source file(s) |
| **Phase introduced** | The phase or PR that first created the pressure |
| **What it does now** | A factual description of the current behavior |
| **Why it is under pressure** | Bullet-point list of specific reasons the area creates friction or risk |
| **Refactor direction** | The recommended approach for resolving the pressure — the suggested next action |
| **Prerequisite** | Work that must be complete before the refactor can proceed safely |

### Priority tiers

Notes are grouped into three priority tiers based on their impact on development velocity:

| Tier | Meaning |
|------|---------|
| **High** | Actively slows new feature development or increases regression risk. Address before the next major feature phase. |
| **Medium** | Adds maintenance overhead but does not block current development. Schedule for the next cleanup sprint. |
| **Low** | Well-understood technical debt with a clear resolution path. Track but defer until the trigger condition is met. |

### How to add a note

1. Open `Docs/REFACTOR-PRESSURE.md`.
2. Choose the correct priority section (High, Medium, or Low) based on the tier definitions above.
3. Add a new numbered entry using the next available sequential number.
4. Fill in all required sections: **File/Files**, **Phase introduced**, **What it does now**, **Why it is under pressure**, **Refactor direction**, and **Prerequisite**.
5. Commit the change with a message such as `docs: add refactor pressure note for <area>`.

### How to resolve a note

1. Complete the work described in the entry's **Refactor direction** section.
2. In `Docs/REFACTOR-PRESSURE.md`, remove the entry from the active section.
3. Add a row to the **Resolved Pressure** archive table at the bottom of the document, recording the area, the phase or PR it was resolved in, and a one-line summary of the resolution.
4. Renumber remaining active entries to maintain sequential order.
5. If the resolution corresponds to a new phase, mark that phase complete in `PHASES-ROADMAP.md`.
