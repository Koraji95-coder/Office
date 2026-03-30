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
- Tool detail is a second depth for launch readiness and graduation path

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
