# Graduation Path Guide

> **Scope:** Developer Portal — Tool Detail view  
> **Status:** Future-state storyboard documentation. Applies to all developer-portal tools that carry a graduation path metric.

---

## Overview

The **Graduation path** panel on the Tool Detail view defines the three mandatory steps a future product tool must complete before it is exposed to customers. Productization is staged, not implied — no tool leaves the developer workshop without passing all three steps.

The panel appears at `developer-portal:tool-detail` and is always shown when a tool carries a release state of "Developer beta" or earlier. It sits alongside the Launch readiness rows, Future product fit, Proof inputs, and Route hygiene panels.

---

## The Three Graduation Steps

### 1. Tighten customer-safe copy

**What it means**

Every visible string on the tool's route — labels, descriptions, status badges, action buttons, and companion notes — must be reviewed and rewritten to meet customer-safe language standards before the tool leaves the workshop.

**Customer-safe copy rules**

| Rule | Developer workshop language | Customer-safe equivalent |
|------|-----------------------------|--------------------------|
| No lab terminology | "Developer beta", "experimental", "lab-only" | Remove or replace with product-stage language, or hide the string entirely |
| No internal architecture exposure | "Backend service ready", "gateway bridge healthy" | Translate to operator outcome: "Drawing export is available", "File transfer is ready" |
| No build-pipeline jargon | "Doctor report healthy", "version check required" | Suppress or replace with "Ready" / "Needs setup" |
| Workflow-first framing | "Launch tool", "Open runtime requirements" | "Start AutoDraft Studio", "View setup checklist" |
| Calm and delivery-oriented tone | Diagnostic or operational density | Premium, spare language that focuses on deliverables and readiness |

**How to apply**

1. Open the tool's route in the Developer Portal.
2. Walk every visible text node: kicker, title, stage badge, summary, metrics, panel eyebrows, panel bodies, action labels, inspector text, and dock items.
3. For each string, ask: "Would a customer reading this understand it without knowing the internal system?" If no, rewrite or hide.
4. Use the Customer App surface interpretation as the reference tone: customer calm, premium, delivery-oriented.
5. Mark the copy review complete in the tool's readiness checklist before moving to step 2.

**What to reject**

- Suppressing strings at render time without rewriting the underlying copy — hiding is not the same as fixing
- Replacing developer labels with marketing copy — the goal is workflow clarity, not promotion
- Leaving status badges that expose system internals (e.g. "Gateway: version mismatch") visible to customers

---

### 2. Prove workflow value with operators

**What it means**

Before productization, at least one operator must use the tool in a real project context and confirm that it delivers measurable workflow value. Usage notes from that session become the primary evidence input.

**Evidence requirements**

- At least one operator usage session recorded in Proof inputs
- Session notes describe a specific workflow task (not a feature demo)
- Notes confirm the tool reduces a step, shortens a cycle, or eliminates a manual hand-off
- Supportability check: the tool can be explained to a customer without a developer present

**What to reject**

- Internal team demos as substitutes for operator usage evidence
- Generic positive feedback without a workflow task reference
- Usage evidence from a developer acting as a proxy operator

---

### 3. Remove lab-only controls from the route

**What it means**

Developer-only interface elements — diagnostic panels, version-check readouts, raw log viewers, internal state toggles — must be removed from the route or gated behind a developer-only flag before the route becomes customer-visible.

**Controls that must be removed or gated**

- Raw runtime status rows (Frontend route, Backend service, Gateway bridge) that expose infrastructure language
- Any inspector section that surfaces internal architecture or build state
- Dock items that reference developer workflow steps (e.g. "Graduation: Queued")
- Command palette actions that trigger developer operations (restart, doctor, rebuild)

**Route hygiene check**

The cleaned route must pass the following three-point check before the tool is considered graduation-ready:

1. **One launch CTA** — a single primary action that starts the tool's workflow
2. **One readiness summary** — a single status indicator that tells the operator whether the tool is ready to use
3. **One graduation story** — a clear path from current state to full product availability, expressed in customer language

---

## Graduation Path in the Storyboard

The graduation path panel in the Tool Detail view renders the three steps above as a key-list. The ordering is fixed and intentional:

1. Copy must be safe before operators see it
2. Operator evidence must exist before the tool ships
3. Lab controls must be removed before the route goes live

The panel's subtitle is "Productization is staged, not implied." This phrase is the canonical reference for graduation intent. Any feature documentation, release note, or handoff brief that touches a future product tool should restate this principle.

---

## Relationship to Other Tool Detail Panels

| Panel | Graduation dependency |
|-------|-----------------------|
| **Launch readiness** | Runtime rows use developer language — these must be translated or suppressed in step 1 |
| **Future product fit** | Monetization signals become visible only after step 2 provides workflow evidence |
| **Proof inputs** | Operator usage notes, supportability checks, and customer-safe review state — all three must be present before step 3 is complete |
| **Route hygiene** | One launch CTA, one readiness summary, one graduation story — the final check for step 3 |

---

## Integration with Reference Board

The Developer Portal section of `reference-board.md` states that the tool detail page is responsible for "launch readiness and graduation path." The copy guidelines in this document define what that means in practice. When authoring any future tool-detail copy, treat this guide as the canonical standard.

See also `reference-board.md` → Developer Portal → Navigation model for the route structure context.
