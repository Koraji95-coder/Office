# How To Reply To DailyDesk Agents

This guide is for getting better results from the desks, inbox suggestions, and approval workflow in DailyDesk.

## The Core Rule

A useful reply has 4 parts:

1. `Source`
What material the agent should use.

2. `Task`
What you want it to do.

3. `Output`
What form the answer should take.

4. `Constraints`
What to ignore, how narrow to stay, and whether to ask follow-up questions.

If you leave one of those out, the agent will usually fill the gap with assumptions.

## The Best Prompt Shape

Use this pattern:

```text
Use [source].
Do [task].
Return [output].
Stay within [constraints].
```

Example:

```text
Use my imported OneNote package on power-system protection.
Create a study guide.
Return key concepts, formulas, relay types, failure modes, common mistakes, and a 10-question self-check.
Stay grounded in the notebook only. If the notebook does not contain enough information, say what is missing.
```

## What Each Desk Is Good For

### Chief of Staff

Use for:
- deciding what to do next
- routing work between study, Suite, CAD, and business
- turning too many ideas into one practical plan

Best kinds of prompts:
- `Give me the highest-leverage next move for today based on my current queue and training pressure.`
- `Turn these 3 priorities into a morning plan, study block, and repo block.`
- `Tell me what to ignore today and why.`

### Engineering Desk

This is the best desk for EE Mentor work.

Use for:
- explanations
- study guides
- quizzes
- oral defenses
- notebook-grounded learning

Best kinds of prompts:
- `Use my imported OneNote package to explain transformer differential protection like a tutor.`
- `Quiz me one question at a time from my imported notebook. Wait for my answer before continuing.`
- `Turn my notebook into a study guide with sections, formulas, and common mistakes.`
- `Create a 20-question practice test from my notebook and include answer explanations at the end.`

### Suite Context

Use for:
- quiet Suite awareness
- workflow interpretation
- read-only repo or product context
- workflow-pattern research tied to Suite

Best kinds of prompts:
- `Use Suite as background context only. Compare approval-routing patterns that fit our workflow.`
- `Explain the tradeoffs of issue-set approval flow without proposing code changes yet.`

### Business Ops

Use for:
- market research
- competitor review
- offer framing
- narrowing broad research into a decision

Best kinds of prompts:
- `Research drafting workflow approval tools and return only features relevant to electrical production control.`
- `Compare these 3 competitors by approval routing, audit trail, revision control, and pricing risk.`

### ML Engineer

Use for:
- understanding the ML analytics and learning pipeline status
- diagnosing weak topic clusters or learning plateaus
- checking document embedding and knowledge indexing health
- interpreting forecast anomalies or readiness scores

Best kinds of prompts:
- `What is the current ML pipeline status and when did it last run?`
- `Show me the current weak topics and what the forecast says about my learning plateau.`
- `What is the embedding coverage for my imported knowledge documents?`
- `Explain what the ML analytics say about my progress and what to do next.`

## Understanding Agent Response Sections

Each desk structures its answers using named sections. Knowing what each section means helps you evaluate the response and decide what to do next.

### Chief of Staff

| Section | What It Contains |
|---------|-----------------|
| `NEXT MOVE` | The single highest-leverage action for right now |
| `WHY` | The reasoning that makes this the right move |
| `HANDOFF` | Where the work should go next (study, Suite, CAD, or business) |

### Engineering Desk

| Section | What It Contains |
|---------|-----------------|
| `ANSWER` | The direct technical answer or explanation |
| `CHECKS` | Key verification points, failure modes, or safety considerations |
| `CAD OR SUITE LINK` | How this concept connects to CAD workflow or Suite context |

### Suite Context

| Section | What It Contains |
|---------|-----------------|
| `CONTEXT` | Current Suite state, hot areas, or relevant workflow background |
| `TRUST` | Suite availability and runtime trust status |
| `WHY IT MATTERS` | Why this context affects operator decisions right now |

### Business Ops

| Section | What It Contains |
|---------|-----------------|
| `MOVE` | The specific internal operating move or offer-shaping step |
| `WHY IT WINS` | The reason this move produces real value without hype |
| `WHAT TO PROVE` | The measurable proof point or next validation step |

### ML Engineer

| Section | What It Contains |
|---------|-----------------|
| `ML STATUS` | Current pipeline state, last run timestamp, and component health |
| `INSIGHTS` | Key findings from analytics, forecast, or embedding results |
| `RECOMMENDATIONS` | Specific actions to improve pipeline health or learning outcomes |
| `SUITE INTEGRATION` | How ML results connect to Suite workflow or production context |

## Know The Difference Between Desk Chat And Inbox Work

### Desk Chat

Desk chat is where you ask directly for an answer, explanation, study aid, or research result.

Use it when you want:
- an explanation now
- a study guide now
- a quiz now
- a research memo now

### Inbox

Inbox is where agents put follow-through suggestions.

Use it when the app proposes:
- a research follow-up
- a business next move
- a Suite-adjacent workflow investigation
- a repo or implementation proposal

Inbox is not the same as desk chat. Inbox items are proposed moves, not finished work.

## What Approval Buttons Actually Mean

This is the part that matters most.

### `Approve only`

This records your decision.

It does **not** start research or execution.

The item moves to `Approved next`.

Use this when:
- you agree with the direction
- you want to keep it
- you are not ready to start it yet

### `Approve & queue`

This records your decision and stages the item for follow-through.

Use this when:
- you want it kept in the active work lane
- you do not need it to run immediately

### `Approve & run`

This records your decision and starts the research follow-up immediately.

Use this when:
- you want the result now
- the item is clearly scoped
- you already know what output you want

### `Queue`

This stages an already approved or self-serve item.

### `Run now`

This starts the selected item immediately.

If the item is a research follow-up, this is what actually makes the app do the work.

## Workflow Templates

Workflow templates are named sequences of background jobs that run in order. Use them when you need the pipeline, knowledge, or analytics updated as a unit.

### Built-In Templates

#### Daily Run

Runs the full daily pipeline: ML Pipeline, Export Suite Artifacts, and Index Knowledge Documents.

Use when:
- you are starting your work session and want everything current
- you want all ML and knowledge state refreshed in one step

Steps: ML Pipeline → Export Suite Artifacts → Index Knowledge Documents

#### Exam Prep

Focused workflow for study preparation: runs learning analytics, generates document embeddings, and re-indexes knowledge documents.

Use when:
- you are preparing for a practice test or oral defense
- you want weak topics, knowledge coverage, and embeddings all current before a study session

Steps: Run Learning Analytics → Generate Document Embeddings → Index Knowledge Documents

#### Knowledge Refresh

Re-indexes all knowledge documents and updates embeddings. Aborts if any step fails.

Use when:
- you have imported new documents and want them indexed immediately
- knowledge search results are stale or missing recent imports

Steps: Index Knowledge Documents → Refresh Document Embeddings

### Running A Template

Templates can be run directly from the workflow panel or via the Approve & run path in the inbox.
If you want results immediately, use `Run now` after selecting the template.
If you want it staged for later, use `Queue`.

## How To Write Useful Approval Reasons

A good approval reason should answer 3 things:

1. Why this matters now
2. What the return should focus on
3. What to ignore

Weak:

```text
Yes
```

Better:

```text
Useful because I need approval-routing patterns for electrical drafting.
Focus on revision control, signoff states, audit trail, and package handoff.
Ignore CRM, billing, and generic PM features.
```

Best:

```text
Approve and run.
I need this to evaluate whether Suite should support drawing review routing and issue-set approvals.
Return a short fit-gap summary, top 5 features, missing proof, and one recommendation.
Ignore generic project-management features.
```

## Best Reply Patterns For Study And Learning

If you want EE Mentor to help you learn, reply in one of these patterns.

### Study Guide

```text
Use my imported OneNote package on [topic].
Create a study guide.
Return concepts, formulas, terminology, failure modes, common mistakes, and a short self-check.
Stay grounded in the notebook only.
```

### Tutor Mode

```text
Use my imported OneNote package on [topic].
Teach me this step by step like a tutor.
Return one concept at a time with a short example.
Stop after each concept and ask if I want the next one.
```

### Quiz Mode

```text
Use my imported OneNote package on [topic].
Quiz me one question at a time.
Do not reveal the answer until I respond.
After each answer, explain why I was right or wrong.
```

### Oral Defense Mode

```text
Use my imported OneNote package on [topic].
Run an oral defense.
Ask one technical question at a time, wait for my answer, then grade it for correctness, completeness, and field judgment.
```

### Practice Test

```text
Use my imported OneNote package on [topic].
Create a practice test.
Return 15 questions with mixed difficulty and an answer key with explanations at the end.
Stay within the notebook content.
```

## Best Reply Patterns For ML Engineer

Use ML Engineer when you want to understand or improve the learning pipeline.

### Check Pipeline Health

```text
Show me the current ML pipeline status.
Return when it last ran, what components are active, and any anomalies.
```

### Diagnose Weak Topics

```text
Use my current ML analytics.
List my weak topic clusters and what the forecast says about my plateau risk.
Return one recommended study focus.
```

### Check Knowledge Coverage

```text
What is the current embedding and indexing coverage for my imported knowledge documents?
Return document count, embedding dimension, and any gaps.
```

### Interpret Learning Forecast

```text
Use my current ML forecast.
Explain the progress trend and flag any plateaus or anomalies.
Return one concrete next step.
```

## Best Reply Patterns For Electrical Drafting Workflows

Use these patterns when working on drawing review, revision control, issue sets, or approval routing for electrical production work.

### Drawing Review Routing

```text
Use Suite as background context only.
Explain the approval-routing states that matter for electrical drawing review.
Return: review states, transition triggers, signoff requirements, and operator risks.
Do not suggest code changes yet.
```

### Revision Control and Audit Trail

```text
Research revision-control patterns for electrical drafting production workflows.
Return: revision tracking, signoff states, audit trail requirements, and package handoff steps.
Ignore CRM, billing, and generic PM features.
Focus only on what affects drawing approval and transmittal.
```

### Issue Set Approval

```text
Use Suite as background context only.
Compare issue-set approval flow patterns for electrical drafting teams.
Return: issue states, approval gates, rejection paths, and resubmission rules.
Keep it tied to review-first production control.
```

### Drawing QA Checklist

```text
Use my imported knowledge on electrical drawing QA.
Create a review checklist for [drawing type].
Return: standard checks, failure modes, code references, and sign-off criteria.
Stay grounded in the imported knowledge only.
```

### Production Transmittal Workflow

```text
Use Suite as background context only.
Explain the transmittal and package handoff workflow for electrical drawing sets.
Return: package states, required approvals, delivery triggers, and what can fail silently.
Do not propose implementation yet.
```

### Competitor Fit-Gap For Drafting Control

```text
Research [tool name] for electrical drafting production control.
Return: approval routing, revision tracking, issue-set handling, audit trail, and AutoCAD workflow fit.
Return a short fit-gap summary and one recommendation.
Ignore CRM, invoicing, and general PM features.
```

## Best Reply Patterns For Approval Routing and Workflow Fit

Use these patterns when validating approval routing, issue-set handling, audit trail compliance,
and overall workflow fit for electrical drafting production control.

### Revision Tracking Alignment

```text
Use Suite as background context only.
Verify that the revision tracking workflow aligns with the approval routing requirements.
Return: revision states, transition rules, approval gates, and compliance gaps.
Focus only on revision-to-approval alignment.
```

### Issue-Set Handling

```text
Use Suite as background context only.
Describe how issue sets are grouped, submitted, and tracked through the approval workflow.
Return: issue-set states, approval gates, rejection paths, and resubmission rules.
Exclude CRM, billing, and general PM scope.
```

### Audit Trail Compliance

```text
Research audit trail requirements for electrical drafting approval workflows.
Return: required audit fields, actor accountability, state transition records, and timestamp requirements.
Verify compliance against revision tracking and issue-set approval steps.
Ignore features unrelated to approval and signoff audit.
```

### Approval Routing Verification

```text
Use Suite as background context only.
Verify the approval routing workflow covers all required signoff states and transitions.
Return: routing rules, signoff accountabilities, escalation paths, and workflow fit gaps.
Focus on fit with electrical drafting production control requirements.
```

### Workflow Fit Assessment

```text
Research workflow fit for electrical drafting production control tools.
Return: revision tracking fit, issue-set handling fit, audit trail fit, approval routing fit, and AutoCAD integration fit.
Return a fit-gap summary per category and one overall recommendation.
Ignore CRM, invoicing, and general PM features.
```

## Electrical Drafting Production Control: Implementation Steps

This section documents how the electrical drafting production control and audit trail processes
integrate with the Suite system. It is intended for developers and stakeholders who need to
understand how these processes are implemented, not just how to prompt for them.

### DraftFlow Evaluation and Integration

DraftFlow (www.draftflow.org) is the primary external reference tool evaluated for electrical
drafting production control. It tracks sheets, revisions, QA workflows, and approvals without
relying on email or spreadsheets. When evaluating DraftFlow against Suite requirements, follow
these steps:

1. **Run the fit-gap research prompt** (see "Competitor Fit-Gap For Drafting Control" in the
   Electrical Drafting Workflows section) to obtain a structured comparison across the five
   key categories: approval routing, revision tracking, issue-set handling, audit trail, and
   AutoCAD workflow fit.
2. **Map DraftFlow features to Suite models.** Each DraftFlow capability should be evaluated
   against the corresponding Suite model:
   - Sheet/revision tracking → `DrawingRevisionRecord` (DrawingId, RevisionNumber, State, IssuedBy, PackageRef)
   - QA workflow → `DrawingSignoffState` states: Draft → InReview → Approved / Rejected → Superseded
   - Issue package grouping → `IssueSetRecord` (DrawingSetRef, RevisionIds, State, PackageRef)
   - Approval routing → `IssueSetState` states: Pending → InApproval → Approved / Rejected → Resubmitted
   - Audit trail → `AuditTrailEntry` (Actor, Action, FromState, ToState, Notes, OccurredAt)
3. **Record fit-gap findings** as a research note in `Knowledge/Research/` following the
   existing naming convention (e.g. `YYYYMMDD-draftflow-fit-gap-summary.md`).
4. **Identify implementation gaps.** Any DraftFlow capability that Suite models cannot yet
   support should be logged as an Inbox item for Chief of Staff routing.

### Audit Trail Implementation Steps

The audit trail records every state transition in the electrical drawing approval workflow.
Each entry in `AuditTrailEntry` must capture the following fields:

| Field | Required | Description |
|---|---|---|
| `Id` | Yes (auto-generated) | Unique identifier; auto-generated by the property initializer — callers do not need to assign it |
| `DrawingId` | Yes | Links the audit entry to its parent drawing |
| `RevisionId` | Yes | Links the audit entry to the specific revision or issue-set record |
| `Action` | Yes | Human-readable description of the event (e.g. "submitted for review", "approved", "rejected", "issued to package") |
| `Actor` | Yes | Name or identifier of the person who performed the action |
| `FromState` | Yes | `DrawingSignoffState` the drawing was in before the transition |
| `ToState` | Yes | `DrawingSignoffState` the drawing moved to after the transition |
| `Notes` | Optional | Reviewer comments, rejection reasons, or transmittal references |
| `OccurredAt` | Yes | UTC timestamp of the action |

**Recording steps for a revision state transition:**

1. Capture `FromState` from the current `DrawingRevisionRecord.State`.
2. Capture `Actor` from the authenticated operator or approval officer.
3. Build the `AuditTrailEntry` with `DrawingId`, `RevisionId`, `Action`, `Actor`,
   `FromState`, `ToState`, `Notes`, and `OccurredAt = DateTimeOffset.UtcNow`.
4. Update `DrawingRevisionRecord.State` to `ToState`.
5. Persist the `AuditTrailEntry` to the audit log store before any other side effects.

**Recording steps for an issue-set state transition:**

1. Capture `Actor` from the approval officer who submitted or reviewed the issue set.
2. Build the `AuditTrailEntry` using `DrawingSetRef` as `DrawingId` and the issue-set
   `Id` as `RevisionId`. Record the transition in `Action` and `Notes` fields because
   `IssueSetState` and `DrawingSignoffState` are semantically distinct enums.
3. Update `IssueSetRecord.State` to the new `IssueSetState`.
4. Persist the `AuditTrailEntry` before returning the updated record.

### Production Control Workflow Integration

The production control workflow connects drawing authoring to transmittal delivery through
four sequential gates:

```
[Draft] → submit for review → [InReview] → approve → [Approved] → issue to package → [Superseded on next rev]
                                          → reject  → [Rejected]  → rework → [Draft] (new revision)
```

For issue sets:

```
[Pending] → submit → [InApproval] → approve → [Approved] → assign PackageRef
                                  → reject  → [Rejected]  → rework → [Resubmitted] → [InApproval]
```

**Integration checklist for each gate:**

- [ ] State transition recorded in `AuditTrailEntry` before any downstream action
- [ ] `Actor` field populated from the authenticated user performing the approval
- [ ] `OccurredAt` set to `DateTimeOffset.UtcNow` at the moment of transition
- [ ] `PackageRef` assigned to `DrawingRevisionRecord` and `IssueSetRecord` only after `Approved` state
- [ ] Rejection recorded with a `RejectionReason` in `IssueSetRecord` and a `Notes` entry in `AuditTrailEntry`

### Suite Model Reference

| Model | Purpose | Key Fields |
|---|---|---|
| `DrawingRevisionRecord` | Tracks a single drawing revision through the signoff lifecycle | DrawingId, RevisionNumber, State, IssuedBy, IssuedAt, PackageRef |
| `DrawingSignoffState` | Enum: Draft, InReview, Approved, Rejected, Superseded | — |
| `IssueSetRecord` | Groups revisions into a named issue package for approval | DrawingSetRef, RevisionIds, State, IssuedBy, IssuedAt, RejectionReason, PackageRef |
| `IssueSetState` | Enum: Pending, InApproval, Approved, Rejected, Resubmitted | — |
| `AuditTrailEntry` | Immutable record of every state transition event | DrawingId, RevisionId, Action, Actor, FromState, ToState, Notes, OccurredAt |

## Best Reply Patterns For Research

If the answer needs current web facts, say so directly.

Use:

```text
/research [query]
```

Or:

```text
Use live research for this.
Compare [thing A] vs [thing B].
Return only the differences that matter for [decision].
Ignore generic marketing claims.
```

Good research prompts are narrow.

Weak:

```text
Research DraftFlow
```

Better:

```text
Research DraftFlow for electrical drafting production control.
Return approval routing, revision tracking, issue-set handling, audit trail, and AutoCAD-related workflow fit.
Ignore CRM, invoicing, and general PM features.
```

## Best Reply Patterns For Chief Of Staff

Use Chief when you are overloaded or need a decision.

Good examples:

```text
I have 90 minutes.
Use my current queue, training pressure, and open approvals.
Tell me the single highest-leverage move and the next 2 backup moves.
```

```text
I have study work, Suite work, and business research competing right now.
Route the day into morning, midday, and evening blocks.
Keep it realistic and cut anything non-essential.
```

## Best Reply Patterns For Suite Context

Use Suite Context when you want read-only product or workflow interpretation.

Good examples:

```text
Use Suite as background context only.
Explain the approval-routing patterns we should probably support.
Return a short list of states, transitions, and operator risks.
Do not suggest code changes yet.
```

```text
Compare review-first workflow patterns that fit electrical drafting.
Return only the patterns that affect drawing review, issue sets, approvals, and transmittals.
```

## Best Reply Patterns For Business Ops

Use Business Ops when you want market validation or offer framing.

Good examples:

```text
Research workflow approval tools for drafting and engineering teams.
Return only features tied to revision control, signoff, audit trail, and package delivery.
Ignore CRM, billing, and agency-focused features.
```

```text
Turn this competitor research into a decision memo.
Return ideal customer, strongest overlap with Suite, biggest gap, and one practical next move.
```

## What Usually Causes Bad Answers

- asking for too much in one turn
- not telling the agent what source to use
- not saying whether you want study help, research, or a decision
- not saying what to ignore
- approving an item and expecting that alone to run it
- using replies like `yes`, `ok`, or `look into this` with no scope

## The Fastest Way To Get Good Results

Use this checklist before you hit send:

1. Did I say what source to use?
2. Did I say what exact output I want?
3. Did I say what to ignore?
4. Do I want approval only, or do I want the work to start now?
5. If this needs current facts, did I explicitly ask for research?

## Paste-Ready Short Replies

### Start a useful approved research item

```text
Approve and run.
Focus on approval routing, audit trail, revision control, and handoff.
Return a short fit-gap summary and one recommendation.
Ignore generic PM features.
```

### Keep an item without running it

```text
Approve only.
This is relevant, but not for now.
Keep it for later comparison against Suite approval flow.
```

### Get a notebook-grounded study guide

```text
Use my imported OneNote package.
Create a study guide on [topic].
Stay grounded in the notebook only.
```

### Get quizzed properly

```text
Use my imported OneNote package on [topic].
Quiz me one question at a time and wait for my answer.
```

### Force a narrow research answer

```text
Use live research.
Return only what matters for [decision].
Ignore broad market commentary and generic feature lists.
```

## Electrical Construction QA/QC Templates

The electrical drawing QA/QC workflow references an official PDF template published by Watercare:

**QA/QC Templates for General Electrical Construction Standards**
- URL: [qa_templates_for_electrical_construction_standards.pdf](https://wslpwstoreprd.blob.core.windows.net/kentico-media-libraries-prod/watercarepublicweb/media/watercare-media-library/electrical-standards/qa_templates_for_electrical_construction_standards.pdf)
- Relevant section: **1.13 QA/QC template** — Minimum mandatory tests, including switchboards, distribution centres, and control centres.

Use this template when:
- Running an electrical drawing review against construction standards
- Verifying that a drawing package satisfies the 1.13 QA/QC minimum mandatory test requirements
- Generating a checklist prompt for the Engineering Desk grounded in official construction standards

### Prompt Pattern For 1.13 QA/QC Review

```text
Use the Watercare QA/QC Templates for General Electrical Construction Standards (section 1.13).
Source: https://wslpwstoreprd.blob.core.windows.net/kentico-media-libraries-prod/watercarepublicweb/media/watercare-media-library/electrical-standards/qa_templates_for_electrical_construction_standards.pdf
Review [drawing package or topic] against the minimum mandatory test requirements.
Return a pass/fail checklist covering switchboards, distribution centres, and control centres.
Flag any items that are missing or unclear.
```

## Current Practical Rule

If you want the agent to actually do work now, your best choices are usually:

- a direct desk prompt
- `Approve & run`
- `Run now`

If you use `Approve only`, assume the work has **not** started yet.
