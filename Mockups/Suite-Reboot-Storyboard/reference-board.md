# Suite Reboot Reference Board

## Daily Desk

Official references

- Raycast Notes - https://manual.raycast.com/notes
- Raycast Snippets - https://manual.raycast.com/snippets
- Obsidian Backlinks - https://help.obsidian.md/plugins/backlinks
- Linear Team pages and Projects - https://linear.app/docs/default-views and https://linear.app/docs/projects

What to borrow

- Command-first entry instead of dashboard-first entry
- Lightweight note capture and reusable snippet patterns
- Linked context instead of longer assistant history
- Triage-oriented workflow structure with one queue for what needs attention

What to reject

- Separate windows for every assistant or every function
- White controls, white popups, or default platform chrome
- Card mosaics as the default visual language
- More top-level tabs every time a new workflow appears

Navigation model

- One main family entry called Daily Desk
- Two view depths in the storyboard: hero and active session
- A shared command palette for route jumping and quick actions
- Approval work represented as an inbox glance in the hero and a deeper queue in the session story

Layout hierarchy

- Command strip first
- Today stack second
- Training, research, repo coach, and inbox as major sections
- Right inspector for rules and rationale, not extra work

Status and loading behavior

- One small activity dock for live jobs
- Saved-state language for training history
- No startup chatter or "syncing" copy in the first viewport

Density and tone

- Dense enough to operate
- Calm enough to teach
- Warm-metal accents only where action matters

### Session view components

The Active Session view (daily-desk:session) exposes two focused panels that drive the scoring and prioritization loop: Rubric and Review queue.

### Rubric

The Rubric panel renders the defense scoring breakdown for the current session topic. It is a rows-style panel occupying a span-4 column alongside the Review queue and Career proof panels.

Purpose: Score a typed oral defense answer across multiple dimensions so the operator knows exactly where thinking is weak before the next study move.

Scoring dimensions:
- Correctness — Whether the core answer is technically accurate
- Tradeoffs — Whether the answer compares options, constraints, or failure modes
- Validation — Whether the answer describes how the claim would be tested or verified

Each dimension carries a score out of 5 and a short annotation (e.g. "strong fundamentals", "needs comparison language", "more test thinking needed"). The annotation is the actionable signal, not the number alone.

Scoring logic:
- Scores are generated locally after each defense run
- Weak scores (2 or below) flag the dimension as a recurrence candidate
- The dimension annotations feed directly into the Review queue prioritization
- A complete session requires all three dimensions to be evaluated and saved to training history

What to show:
- All three dimensions with score and annotation in every session
- Weak scores in a visually distinct state so they stand out on re-read
- A clear path from rubric annotation to the next review item

What to reject:
- Hiding rubric output behind a toggle or secondary state
- Aggregating all dimensions into a single composite score that loses specificity
- Showing rubric results without a visible connection to the Review queue

### Review queue

The Review queue panel shows the next recommended review items for the session. It is a list-style panel occupying a span-4 column in the Active Session view.

Purpose: Surface the most important follow-up actions after practice and defense scoring so the operator's next study move is always explicit rather than implied.

Prioritization logic:
- Items are generated from weak-topic recurrence — topics that score low in defense or appear repeatedly across practice attempts
- Rubric dimension annotations (especially Tradeoffs and Validation) are the primary signals that promote a topic into the queue
- The queue is ordered by urgency: same-session follow-ups appear first, then deferred items from prior sessions
- Promoting a reflection note into a study note is always surfaced as a low-cost queue item after a completed session

Queue item format:
- Each item is an actionable instruction, not a passive label (e.g. "Revisit relay timing after lunch", not "Relay timing")
- Items reference the topic, the expected action, and optionally a time anchor or context tie-in

What to show:
- The queue immediately after a defense is scored, never hidden
- At least one item that connects a rubric annotation to a concrete follow-up
- A "promote to study note" item whenever a reflection is saved

What to reject:
- A queue that grows unbounded — cap visible items at five and rotate stale entries out
- Generic items with no tie to the session topic or rubric output
- Splitting the queue across the session timeline and a separate review surface

### How Rubric and Review queue work together

The two panels form a closed feedback loop inside the Active Session view:

1. Practice scores weaknesses
2. Rubric scores the defense answer across Correctness, Tradeoffs, and Validation
3. Dimension annotations from the Rubric populate the Review queue with specific next-run items
4. The Review queue drives the next session plan (topic selection, priority order, time anchors)
5. Completed sessions write the loop outcome to training history so future sessions inherit the recurrence signal

Neither panel should appear in isolation. If the Rubric is present, the Review queue must be adjacent. If the Review queue is populated, at least one item must trace back to a Rubric annotation or a saved reflection.

## Runtime Control

Official references

- Docker Desktop overview - https://docs.docker.com/desktop/use-desktop/
- Docker Desktop troubleshoot and diagnose - https://docs.docker.com/desktop/troubleshoot-and-support/troubleshoot/
- Grafana Application Observability inventory - https://grafana.com/docs/grafana-cloud/monitor-applications/application-observability/manual/inventory/
- Grafana Application Observability service map - https://grafana.com/docs/grafana-cloud/monitor-applications/application-observability/manual/map/

What to borrow

- Centralized runtime dashboard for local resources and actions
- Quick find and direct actions in the header
- Integrated support and diagnostics affordances close to runtime state
- Inventory and map drilldown patterns that move from service list to service relationships cleanly

What to reject

- Parallel status universes between runtime, app, scripts, and support tools
- Decorative dashboard clutter in an operational control surface
- Loud background warnings that are not actionable
- Restart actions scattered across multiple surfaces

Navigation model

- Runtime Control as the primary developer front door
- Hero state for runtime truth and support actions
- Diagnostics state for logs, evidence, support bundle, and restart actions
- Launches Developer Portal instead of reproducing it

Layout hierarchy

- Runtime summary and doctor state first
- Support actions second
- Tool launchers third
- Event rail last

Status and loading behavior

- Trust vocabulary stays limited to Ready, Background, Needs attention, and Unavailable
- Doctor summary is prominent and stable
- Logs and evidence appear in a deeper state, not the main hero

Density and tone

- Utilitarian and compact
- Clear, not decorative
- Operational confidence over mood

## Developer Portal

Official references

- Vercel dashboard navigation redesign - https://vercel.com/changelog/dashboard-navigation-redesign-rollout
- Suite developer tool manifest - C:/Users/koraj/OneDrive/Documents/GitHub/Suite/src/routes/developerToolsManifest.ts
- Suite Operations route - C:/Users/koraj/OneDrive/Documents/GitHub/Suite/src/routes/OperationsRoutePage.tsx

What to borrow

- Resizable or collapsible navigation pattern
- Consistent scope model across route groups
- Group tools by job instead of showing a flat launcher wall
- Use release state and runtime requirements as quiet support data

What to reject

- Flat tool-card grid as the whole page
- Customer-visible lab language
- Diagnostic density on the workshop homepage
- Duplicating Runtime Control inside the portal

Navigation model

- Developer Portal is the web-side workshop homepage
- Groups stay fixed: Publishing and Evidence, Automation Lab, Agent Lab, Architecture and Code, Developer Docs
- Tool detail is a second depth for launch readiness and graduation path; see graduation-path-guide.md for the three-step graduation workflow and customer-safe copy standards

Architecture and Code tools

- Architecture Map — spatial layer view of all Office services, their deployment boundaries, and Suite integration paths; see architecture-tools-guide.md
- Architecture Graph — directed dependency graph showing how services, stores, agents, and broker endpoints connect; see architecture-tools-guide.md
- Refactor pressure notes — developer-authored notes flagging technical debt and extension points; linked to source files via the repo coach

Layout hierarchy

- Workshop purpose first
- Grouped launcher second
- Selected-tool or future-product detail third

Status and loading behavior

- Release state labels stay visible but quiet
- Runtime requirements show readiness without overwhelming the route
- Command Center remains a deeper toolshed, not the portal homepage

Density and tone

- Dense but orderly
- Calm overview, not a junk drawer
- Strong grouping and clear route purpose

## Customer App

Official references

- Linear Projects - https://linear.app/docs/projects
- Linear Team pages - https://linear.app/docs/default-views
- Linear Due dates - https://linear.app/docs/due-dates

What to borrow

- Project-first planning
- Clean separation between projects, issues, and timing pressure
- Due-date visibility without alarm fatigue
- Sidebar and detail rhythm that favors operation over decoration

What to reject

- Architecture pressure and developer diagnostics in the dashboard
- Agent or repo framing in customer views
- Too many dashboard cards competing in the first viewport
- Route-local title duplication

Navigation model

- Customer routes stay focused on Dashboard, Watchdog, Projects, Calendar, Apps, Knowledge, and Settings
- Dashboard acts as a mission board, not a diagnostics page
- Project detail and reference-library detail are the main deeper states

Layout hierarchy

- Mission board summary first
- Project readiness and review pressure second
- Issue sets, transmittal queue, and deadlines after that

Status and loading behavior

- Trust states are stable and product-safe
- Review pressure is visible but not loud
- Watchdog remains supportive, not dominant

Density and tone

- Customer calm
- Premium and legible
- Delivery-oriented, not developer-oriented

## Shared Family Rules

Official references

- Suite tokens - C:/Users/koraj/OneDrive/Documents/GitHub/Suite/src/styles/tokens.css
- Suite theme - C:/Users/koraj/OneDrive/Documents/GitHub/Suite/src/theme.css
- Suite PageFrame - C:/Users/koraj/OneDrive/Documents/GitHub/Suite/src/components/apps/ui/PageFrame.tsx
- Suite PageContextBand - C:/Users/koraj/OneDrive/Documents/GitHub/Suite/src/components/apps/ui/PageContextBand.tsx

Family rules

- Use one dark refined palette family across all surfaces
- Typography family stays Plus Jakarta Sans plus IBM Plex Mono
- Use warm-metal accents and restrained blue only where state needs it
- Favor layout hierarchy over piles of bordered cards
- Use one shared trust vocabulary across runtime and app surfaces

Behavior rules

- No white controls, dropdowns, or scrollbars
- No clipped top-level tabs or nav labels
- No route-local title duplication
- No dashboard-card mosaics by default
- No noisy "checking", "syncing", or "standby" copy in the first viewport

Surface interpretation rules

- Customer calm: premium, spare, delivery-oriented
- Dev dense: grouped, scannable, operationally rich
- Runtime utilitarian: compact, direct, support-ready

Prototype note

- The linked storyboard is a future-state artifact that proves structure and visual family without mutating Suite during the current refactor.
