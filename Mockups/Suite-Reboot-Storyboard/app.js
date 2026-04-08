(function () {
    const fixtures = window.storyboardFixtures;
    if ("scrollRestoration" in window.history) {
        window.history.scrollRestoration = "manual";
    }
    const byId = (id) => document.getElementById(id);
    const els = {
        familyNav: byId("family-nav"),
        routeKicker: byId("route-kicker"),
        routeTitle: byId("route-title"),
        routeStage: byId("route-stage"),
        routeSummary: byId("route-summary"),
        viewSwitcher: byId("view-switcher"),
        stage: byId("stage"),
        inspector: byId("inspector"),
        dock: byId("activity-dock"),
        commandOverlay: byId("command-overlay"),
        commandInput: byId("command-input"),
        commandResults: byId("command-results"),
        commandToggle: byId("command-toggle"),
        commandClose: byId("command-close"),
        commandBackdrop: byId("command-backdrop"),
        inspectorToggle: byId("inspector-toggle")
    };

    const state = {
        familyId: "daily-desk",
        viewId: "hero",
        commandQuery: ""
    };

    const routes = {};

    routes["daily-desk:hero"] = screen({
        kicker: "Command-first operator",
        title: "Daily Desk",
        stage: "High-fidelity hero",
        summary: "One local-first operator surface for study, research, repo coaching, and approval work.",
        metrics: [
            metric("Chief pass", "08:30", "ready for today"),
            metric("Practice queue", "3 due", "grounding, relay logic, fault review"),
            metric("Research watch", "2 live", "NFPA sweep and automation market scan"),
            metric("Approval inbox", "4 items", "repo proposals and learning routes")
        ],
        lead: panel("Command strip", "What should today advance in your career engine?", "The desk routes one study move, one Suite move, and one proof-of-progress move.", ["Run morning pass", "Resume training", "Open approval inbox"]),
        companion: note("Reference move", "Borrow speed from launchers, context from notes, and discipline from triage.", "Use one command surface, saved context, and a small explicit queue."),
        panels: [
            rows("span-8", "Today stack", "One stack, three outcomes", "Route today through study, repo progress, and career proof.", [
                row("Study move", "Retest transient grounding", "oral defense due after practice"),
                row("Suite move", "Map unified doctor touchpoints", "read-only analysis first"),
                row("Career proof", "Capture one architecture note", "feeds portfolio language")
            ]),
            list("span-4", "Inbox glance", "Approvals stay small and explicit", "Pending work stays visible without taking over the session.", [
                "Approve repo coach recommendation",
                "Defer market scan note until Friday",
                "Review one saved research brief"
            ]),
            rows("span-4", "Training", "Scored learning loop", "Practice, defense, and reflection stay in one session.", [
                row("Current topic", "Relay coordination", "weak after last defense"),
                row("Next run", "12 min practice + oral drill", "uses saved notes and history")
            ]),
            list("span-4", "Research", "Use live research when uncertainty appears", "Research should answer decisions, not curiosity.", [
                "NFPA 70 grounding changes worth tracking",
                "Drawing QA workflow references",
                "Suite-adjacent monetization patterns"
            ]),
            rows("span-4", "Repo coach", "Suite stays read-only", "Repo pressure becomes next work, learning payoff, and product impact.", [
                row("Hot area", "Unified doctor contract", "high leverage across app, runtime, and scripts"),
                row("Learning payoff", "Typed status architecture", "strong career narrative")
            ])
        ],
        inspector: inspect("Borrow rules", "Daily Desk should feel like an operator console, not a chat wall.", {
            Keep: ["Global command-first entry", "Linked memory and saved context", "One explicit approval queue"],
            Reject: ["Card mosaics as the main layout", "White controls or default chrome", "Ever-growing top-level tabs"],
            Tone: ["Dense enough to operate", "Calm enough to teach", "Warm-metal accents where action matters"]
        }),
        dock: [
            dock("Chief brief", "Ready", "Pinned after morning pass"),
            dock("Research job", "Running", "NFPA 70 article 250 sweep"),
            dock("Training session", "Queued", "Relay coordination retest"),
            dock("Repo coach", "Blocked", "Waits on approval inbox")
        ]
    });

    routes["daily-desk:session"] = screen({
        kicker: "Guided training and research",
        title: "Active Session",
        stage: "Structural detail",
        summary: "A single loop for practice, oral defense, reflection, and next-action routing.",
        metrics: [
            metric("Focus topic", "Transient grounding", "chosen from weak topics + repo pressure"),
            metric("Practice save", "Complete", "7 of 9 correct"),
            metric("Defense save", "Complete", "clarity improved, tradeoffs weak"),
            metric("History file", "Updated", "training-history.json 09:14")
        ],
        lead: panel("Guided flow", "One session, one topic, one saved outcome", "The session view keeps focus on a single topic until practice, defense, and reflection are all saved.", ["Score next answer", "Save reflection", "Queue review follow-up"]),
        companion: note("Why it matters", "Training history must be visible, not implied.", "Show what was saved, where it was written, and how the next route changed."),
        panels: [
            list("span-7", "Session timeline", "Plan to complete", "The stage rail turns saved progress into a visible loop.", [
                "1. Plan - topic chosen from history, repo needs, and notes",
                "2. Practice - generated and scored locally",
                "3. Defense - typed answer and rubric score",
                "4. Reflection - short memory note saved",
                "5. Complete - next review and why it changed"
            ]),
            rows("span-5", "Saved context", "Current memory packet", "Saved notes, latest mistakes, and the active repo tie-in stay visible.", [
                row("Local notes", "Ground-grid checklist", "imported from knowledge folder"),
                row("Repo tie-in", "Doctor signal vocabulary", "connect EE clarity with product language")
            ]),
            rows("span-4", "Rubric", "Defense scoring breakdown", "Five dimensions, one direct next move.", [
                row("Correctness", "4 / 5", "strong fundamentals"),
                row("Tradeoffs", "2 / 5", "needs comparison language"),
                row("Validation", "3 / 5", "more test thinking needed")
            ]),
            list("span-4", "Review queue", "Next recommended review", "The next run is generated from weak-topic recurrence.", [
                "Revisit relay timing after lunch",
                "Link grounding explanation to field validation",
                "Promote one reflection into a study note"
            ]),
            list("span-4", "Career proof", "Convert learning into proof", "Every completed session should become evidence of clearer engineering thinking.", [
                "Write a short architecture note",
                "Capture one interview-quality explanation",
                "Tag one Suite-adjacent concept for packaging"
            ])
        ],
        inspector: inspect("Session rules", "The loop is complete only when history is saved and the next route is updated.", {
            Visible: ["Show practice state", "Show defense state", "Show reflection state", "Show last history write"],
            Reject: ["Do not split practice and defense into disconnected tabs", "Do not hide training history in passive summaries"]
        }),
        dock: [
            dock("Practice score", "Saved", "Retest moved grounding score upward"),
            dock("Defense score", "Saved", "Clarity improved"),
            dock("Reflection", "Pending", "Need one short note before complete"),
            dock("Next review", "Prepared", "Relay timing in 3 hours")
        ]
    });

    routes["runtime-control:hero"] = screen({
        kicker: "Primary developer door",
        title: "Runtime Control",
        stage: "High-fidelity hero",
        summary: "Compact workstation operations surface with one runtime truth, one doctor summary, and fast support actions.",
        metrics: [
            metric("Runtime state", "Ready", "frontend, backend, broker, watchdog"),
            metric("Doctor issues", "2 actionable", "gateway endpoint and stale collector"),
            metric("Support bundle", "Prepared", "last export 08:52"),
            metric("Developer launchers", "7 linked", "portal, docs, graph, labs")
        ],
        lead: panel("Operational center", "Workstation truth should be immediate, compact, and trustworthy.", "Runtime Control owns local reality: what is up, what is degraded, what needs support action, and what tool should open next.", ["Run doctor", "Open logs bundle", "Launch Developer Portal"]),
        companion: note("Reference move", "Borrow centralized control from desktop runtime tools and observability drilldown.", "Favor a small number of operational bands and one trust vocabulary over decorative dashboards."),
        panels: [
            rows("span-8", "Unified doctor", "One report, four subsystems, one trust vocabulary", "The report should power Runtime Control, Suite, and scripts without parallel status universes.", [
                row("Frontend", "Ready", "shell reachable and current"),
                row("Backend", "Needs attention", "health route reachable, one endpoint mismatch"),
                row("Gateway", "Unavailable", "last heartbeat stale"),
                row("Watchdog", "Background", "collectors active, no noisy first-view alerts")
            ]),
            list("span-4", "Support actions", "Support should be one move away", "Diagnostics, restart actions, and bundle export stay next to runtime truth.", [
                "Export support bundle",
                "Copy doctor snapshot",
                "Restart local broker",
                "Open terminal in runtime root"
            ]),
            list("span-4", "Developer tools", "Launch the next workshop route", "Runtime launches dev routes without duplicating them.", [
                "Developer Portal",
                "Architecture graph",
                "Command Center",
                "Developer docs"
            ]),
            rows("span-4", "Watchdog", "Quiet until action matters", "Background checks stay subdued until they become persistent and actionable.", [
                row("Collector drift", "None", "no repeated stale heartbeats"),
                row("Plugin health", "1 warning", "AutoCAD bridge version skew")
            ]),
            list("span-4", "Recent events", "Activity without noise", "A short rail replaces verbose startup chatter.", [
                "09:03 doctor run completed",
                "09:04 backend endpoint mismatch detected",
                "09:06 support bundle prepared"
            ])
        ],
        inspector: inspect("Runtime rules", "Compact, trustworthy, and supportable.", {
            Keep: ["Central runtime dashboard", "Quick find and direct actions", "Integrated logs and terminal access", "Service inventory and map style drilldown"],
            Reject: ["Multiple health vocabularies", "First-view startup noise", "Developer portal content duplicated here"]
        }),
        dock: [
            dock("Doctor", "Ready", "Unified report available"),
            dock("Support bundle", "Prepared", "Exported with latest logs"),
            dock("Gateway", "Risk", "Heartbeat stale"),
            dock("Developer launcher", "Ready", "Portal available")
        ]
    });

    routes["runtime-control:diagnostics"] = screen({
        kicker: "Support and drilldown",
        title: "Diagnostics Detail",
        stage: "Structural detail",
        summary: "Right-depth support view for logs, restarts, doctor evidence, and bundle capture.",
        metrics: [
            metric("Logs", "4 streams", "frontend, backend, broker, watchdog"),
            metric("Bundle size", "18.4 MB", "ready for export"),
            metric("Restart actions", "3 available", "scoped and explicit"),
            metric("Doctor evidence", "Attached", "endpoint mismatch and collector drift")
        ],
        lead: panel("Support detail", "Diagnostics should deepen context without changing the truth model.", "This state keeps the runtime summary stable while exposing the evidence and actions a developer needs when something is wrong.", ["Export bundle", "Restart backend", "Open filtered logs"]),
        companion: note("Drilldown pattern", "Keep the surface investigative, not theatrical.", "Logs, evidence, and restarts should appear as compact work surfaces with strong defaults."),
        panels: [
            list("span-7", "Log streams", "Focus on the streams that explain the current issue", "Use filtered highlights, not a wall of undifferentiated text.", [
                "backend: health route returned stale gateway metadata",
                "gateway: no heartbeat for 94 seconds",
                "watchdog: collector recovered after retry"
            ]),
            rows("span-5", "Bundle contents", "Support export should be predictable", "Bundle content must be legible before export.", [
                row("Doctor snapshot", "Included", "latest manual report"),
                row("Runtime logs", "Included", "last 30 minutes"),
                row("Config masks", "Included", "secrets hidden")
            ]),
            list("span-4", "Restart safety", "Scoped actions only", "Restart actions stay precise and reversible where possible.", ["Restart backend only", "Restart broker only", "Re-run doctor after restart"]),
            list("span-4", "Evidence chain", "Every warning needs evidence", "Warnings carry timestamps, source, and the next recommendation.", ["Gateway heartbeat age", "Backend endpoint mismatch details", "Collector version skew note"]),
            list("span-4", "Support note", "Hand-off ready", "The detail view should be easy to hand to another engineer.", ["Problem summary", "What changed recently", "What has already been tried"])
        ],
        inspector: inspect("Support rules", "Diagnostics should help resolve issues, not add a new workflow.", {
            Visible: ["Current issue summary", "Evidence and timestamps", "Safe actions", "Bundle export path"]
        }),
        dock: [
            dock("Logs filter", "Applied", "Showing gateway and backend only"),
            dock("Bundle", "Ready", "Ready to export"),
            dock("Restart", "Available", "Backend only"),
            dock("Doctor", "Pinned", "Evidence attached to bundle")
        ]
    });

    routes["developer-portal:hero"] = screen({
        kicker: "Web-side workshop home",
        title: "Developer Portal",
        stage: "High-fidelity hero",
        summary: "Grouped launcher and overview for developer-only routes, staged tools, and docs.",
        metrics: [
            metric("Publishing", "2 active", "changelog and draft notes"),
            metric("Automation lab", "5 staged", "future product surfaces"),
            metric("Architecture", "2 live", "map and graph"),
            metric("Docs and labs", "3 live", "agent lab and dev docs")
        ],
        lead: panel("Workshop launcher", "Developer routes need calm grouping, not a tools junk drawer.", "This surface groups launchers by job and release state, then defers dense diagnostics back to Runtime Control and Command Center.", ["Open Runtime Control", "Launch Architecture Map", "Review future products"]),
        companion: note("Reference move", "Use one collapsible navigation system and consistent tool grouping across scopes.", "The portal should feel quick to scan, easy to launch from, and light on decorative panels."),
        panels: [
            list("span-6", "Publishing and evidence", "Release notes, work proof, and published artifacts", "Keep proof-of-work separate from customer surfaces.", ["Changelog - developer beta", "Delivery evidence ledger", "Draft note review queue"]),
            list("span-6", "Automation lab", "Future product tools stay under workshop control", "The customer app should not carry these until they are individually ready.", ["AutoDraft Studio", "AutoWire", "Ground Grid Generation", "ETAP DXF Cleanup"]),
            rows("span-4", "Agent lab", "Experimental agents stay dev-only", "Quarantine experimental orchestration here.", [
                row("Agents route", "Developer beta", "hidden from customer framing"),
                row("Pairing state", "Experimental", "not part of released product story")
            ]),
            list("span-4", "Architecture and code", "Maps, graphs, and refactor surfaces", "The portal gives quick access, but tool pages stay focused.", ["Architecture Map", "Architecture Graph", "Refactor pressure notes"]),
            list("span-4", "Developer docs", "Workshop notes and runbooks", "Docs are grouped like tools because they support the same routes.", ["Developer docs", "Whiteboard", "Runbook snapshots"])
        ],
        inspector: inspect("Portal rules", "Launch and summarize, do not become a diagnostic duplicate.", {
            Keep: ["Grouped launcher by job", "Release state and future-product callouts", "Runtime requirements as support metadata"],
            Reject: ["Flat tool-card grid as the whole page", "Customer-visible lab language", "Command Center density on the front page"]
        }),
        dock: [
            dock("Developer route", "Ready", "Portal is web-side workshop home"),
            dock("Automation lab", "Active", "Five future products staged"),
            dock("Agent lab", "Queued", "Developer-only"),
            dock("Runtime Control", "Primary", "First door for operations")
        ]
    });

    routes["developer-portal:tool-detail"] = screen({
        kicker: "Tool readiness",
        title: "Tool Detail",
        stage: "Structural detail",
        summary: "Single-tool detail for launch readiness, release state, runtime needs, and graduation path.",
        metrics: [
            metric("Selected tool", "AutoDraft Studio", "future product"),
            metric("Release state", "Developer beta", "not customer-safe yet"),
            metric("Runtime needs", "Frontend, backend, gateway", "launch-ready requirements"),
            metric("Graduation path", "3 steps", "proof before productization")
        ],
        lead: panel("Single-tool readiness", "A tool detail page should answer launch, maturity, and graduation in one scan.", "This is where a developer decides whether to launch, keep iterating, or hold a tool behind the workshop wall.", ["Launch tool", "Open runtime requirements", "Review graduation criteria"]),
        companion: note("Why this exists", "Future product tools need staging, not accidental product exposure.", "Turn release state, runtime dependencies, and customer impact into a clear readiness call."),
        panels: [
            rows("span-7", "Launch readiness", "What the developer needs right now", "The page combines runtime dependencies, current blockers, and quick launch actions.", [
                row("Frontend route", "Available", "launch from portal"),
                row("Backend service", "Ready", "doctor report healthy"),
                row("Gateway bridge", "Needs attention", "version check required")
            ]),
            list("span-5", "Graduation path", "How this becomes customer-safe later", "Productization is staged, not implied.", ["Tighten customer-safe copy", "Prove workflow value with operators", "Remove lab-only controls from the route"]),
            list("span-4", "Future product fit", "Monetization signal", "The detail page can quietly show future packaging potential.", ["High operator leverage", "Strong automation story", "Needs workflow evidence"]),
            list("span-4", "Proof inputs", "What evidence still matters", "Evidence closes the loop between lab and future product.", ["Operator usage notes", "Supportability checks", "Customer-safe review state"]),
            list("span-4", "Route hygiene", "Keep the tool page focused", "The detail view should not become a second portal.", ["One launch CTA", "One readiness summary", "One graduation story"])
        ],
        inspector: inspect("Readiness rules", "Future product tools need explicit staging.", {
            Show: ["Current release state", "Runtime requirements", "Blockers", "Graduation path"]
        }),
        dock: [
            dock("Selected tool", "Pinned", "AutoDraft Studio"),
            dock("Readiness", "Attention", "Gateway check pending"),
            dock("Graduation", "Queued", "Customer-safe later"),
            dock("Portal", "Stable", "Launcher remains calm")
        ]
    });

    routes["customer-app:hero"] = screen({
        kicker: "Customer-safe mission board",
        title: "Customer App",
        stage: "High-fidelity hero",
        summary: "A calmer operations board for drawing production control, deadlines, review pressure, and issue flow.",
        metrics: [
            metric("Project readiness", "7 of 9", "two projects need setup attention"),
            metric("Review pressure", "3 active", "two due today, one overdue"),
            metric("Transmittal queue", "5 items", "two ready to package"),
            metric("Watchdog", "Background", "quiet until action matters")
        ],
        lead: panel("Mission board", "Tell one story: drawing production control.", "The customer dashboard should orient work around readiness, review pressure, issue sets, transmittals, and deadlines instead of architecture or agent internals.", ["Open projects", "Review issues", "Prepare transmittal"]),
        companion: note("Reference move", "Use project and issue structure, but strip out developer density.", "The customer surface stays premium and calm while keeping project timelines and due signals legible."),
        panels: [
            rows("span-8", "Project readiness", "Projects should read like deliverable state, not system state", "Readiness is the main customer lens.", [
                row("North Substation", "Ready for review", "issue set closed, transmittal pending"),
                row("Relay Retrofit", "Needs setup attention", "reference package incomplete"),
                row("Ground Grid Update", "In active review", "two comments still open")
            ]),
            list("span-4", "Review pressure", "Reviews need a calm but visible pressure lane", "Review work is urgent but should not explode the whole dashboard.", ["2 due today", "1 overdue package", "4 waiting on markups"]),
            rows("span-4", "Issue sets", "Group issues by work impact", "Issue grouping should support action, not diagnostic curiosity.", [
                row("Coordination", "4", "relay and grounding notes"),
                row("Drafting QA", "3", "layer and annotation corrections")
            ]),
            list("span-4", "Transmittal queue", "Packaging should feel explicit and finite", "The queue is small, legible, and next-action friendly.", ["North Substation IFC package", "Relay Retrofit review package", "Civil coordination resend"]),
            rows("span-4", "Deadlines", "Due dates belong on the board", "Deadlines should sequence work without becoming an alert wall.", [
                row("Today", "2 due", "one review, one transmittal"),
                row("Next 7 days", "4 due", "sorted by nearest delivery")
            ])
        ],
        inspector: inspect("Customer rules", "Premium, calm, and product-safe.", {
            Keep: ["Project, issue, and deadline structure", "Mission-board framing", "Minimal but useful trust states"],
            Reject: ["Architecture pressure", "Agent memory surfaces", "Repo hotspot content", "Diagnostics in the customer viewport"]
        }),
        dock: [
            dock("Dashboard", "Ready", "Mission board live"),
            dock("Reviews", "Attention", "Two due today"),
            dock("Transmittals", "Queued", "Five items"),
            dock("Watchdog", "Background", "No noisy banners")
        ]
    });

    routes["customer-app:project-detail"] = screen({
        kicker: "Customer-safe depth",
        title: "Project Detail",
        stage: "Structural detail",
        summary: "A project and knowledge detail state that stays customer-safe and workflow-first.",
        metrics: [
            metric("Selected project", "North Substation", "IFC delivery lane"),
            metric("Readiness", "Review ready", "one transmittal gate left"),
            metric("Open issues", "6", "grouped by coordination and drafting QA"),
            metric("Reference library", "12 docs", "controlled and customer-safe")
        ],
        lead: panel("Project depth", "Project detail should deepen delivery work without exposing developer internals.", "This state shows milestones, issue lanes, and reference content with the same calm tone as the mission board.", ["Open issue lane", "Review reference docs", "Prepare package"]),
        companion: note("Why this matters", "Depth belongs to project work, not dev telemetry.", "Every section must help plan, review, or deliver."),
        panels: [
            list("span-7", "Milestones", "Delivery sequence", "Milestones, dependencies, and review gates stay project-facing.", ["Reference package locked", "Review markup pass in progress", "IFC package queued for release"]),
            list("span-5", "Reference library", "Customer-safe knowledge only", "Reference content stays useful without turning into developer docs.", ["Project scope brief", "Drawing standards packet", "Submittal checklist"]),
            rows("span-4", "Issue lane", "Coordination issues", "Issues stay grouped by resolution path.", [
                row("Electrical coordination", "3 open", "one due today"),
                row("Drafting QA", "2 open", "both assigned")
            ]),
            list("span-4", "Package prep", "Transmittal readiness", "The path from review-ready to package-ready is explicit.", ["Check issue closure", "Confirm reference set", "Prepare release notes"]),
            list("span-4", "Customer trust", "Keep confidence calm", "Trust states should sound stable and operational.", ["Ready", "Background", "Needs attention", "Unavailable"])
        ],
        inspector: inspect("Project detail rules", "Only delivery-facing depth belongs here.", {
            Visible: ["Milestones", "Issue groups", "Reference docs", "Package prep"]
        }),
        dock: [
            dock("Project", "Pinned", "North Substation"),
            dock("Issue lane", "Active", "Coordination focus"),
            dock("Reference set", "Ready", "Customer-safe docs only"),
            dock("Package prep", "Queued", "Review closure first")
        ]
    });

    /**
     * Build a metric card model.
     * Each route exposes four metric cards in a strip above the board grid.
     * The card surfaces an operational snapshot: a short label, a prominent
     * value, and a single-line meta note that adds context without explanation.
     * @param {string} label - Short noun phrase (e.g. "Runtime state", "Review pressure")
     * @param {string} value - Prominent status or count (e.g. "Ready", "3 active")
     * @param {string} meta  - One-line qualifier (e.g. "two due today, one overdue")
     */
    function metric(label, value, meta) {
        return { label, value, meta };
    }

    /**
     * Build a data row model for use inside a rows() panel.
     * Rows panels use a row-list layout: each row has a label, a value, and an
     * annotation note. Use rows() when the relationship between label and value
     * needs an explanatory suffix (e.g. scoring dimensions, subsystem states).
     * @param {string} label - Left-aligned identifier (e.g. "Correctness", "Frontend")
     * @param {string} value - Right-aligned status or score (e.g. "4 / 5", "Ready")
     * @param {string} note  - Short annotation below the row (e.g. "strong fundamentals")
     */
    function row(label, value, note) {
        return { label, value, note };
    }

    /**
     * Build the lead panel model (hero panel, span-7).
     * Each route has one lead panel that states the surface purpose, adds a
     * single explanatory body sentence, and exposes up to three action pills.
     * @param {string}   eyebrow - Eyebrow / kicker label
     * @param {string}   title   - Panel heading
     * @param {string}   body    - One-sentence intent description
     * @param {string[]} actions - Array of action pill labels (max 3)
     */
    function panel(eyebrow, title, body, actions) {
        return { eyebrow, title, body, actions };
    }

    /**
     * Build the companion note model (span-5, sits beside the lead panel).
     * The companion note provides a reference rationale — what to borrow and why —
     * without adding actions or extra work to the view.
     * @param {string} eyebrow - Eyebrow / kicker label
     * @param {string} title   - Note heading
     * @param {string} body    - Rationale sentence
     */
    function note(eyebrow, title, body) {
        return { eyebrow, title, body };
    }

    /**
     * Build a rows-layout panel model.
     * Rows panels render a .row-list of .data-row elements, each with a label,
     * value, and annotation note. Use for structured comparisons, scoring
     * dimensions, subsystem states, and multi-field readiness tables.
     * @param {string}   span   - CSS grid span class (e.g. "span-4", "span-7")
     * @param {string}   eyebrow - Panel eyebrow / kicker
     * @param {string}   title   - Panel heading
     * @param {string}   body    - Panel copy / intent sentence
     * @param {object[]} items   - Array of row() objects
     */
    function rows(span, eyebrow, title, body, items) {
        return { span, eyebrow, title, body, rows: items };
    }

    /**
     * Build a list-layout panel model.
     * List panels render a .key-list of .key-row elements, each containing a
     * single <strong> label. Use for action queues, launcher lists, evidence
     * chains, and any panel where items stand alone without a paired value.
     * @param {string}   span   - CSS grid span class (e.g. "span-4", "span-5")
     * @param {string}   eyebrow - Panel eyebrow / kicker
     * @param {string}   title   - Panel heading
     * @param {string}   body    - Panel copy / intent sentence
     * @param {string[]} items   - Array of item label strings
     */
    function list(span, eyebrow, title, body, items) {
        return { span, eyebrow, title, body, list: items };
    }

    /**
     * Build an inspector model.
     * Each route has one inspector card that surfaces design rules and rationale.
     * Sections are named keep/reject/visible/show depending on the rule type.
     * @param {string} title    - Inspector card heading (e.g. "Runtime rules")
     * @param {string} subtitle - One-sentence design intent
     * @param {Object.<string, string[]>} sections - Named rule buckets, each an array of strings
     */
    function inspect(title, subtitle, sections) {
        return { title, subtitle, sections };
    }

    /**
     * Build a dock item model for the activity dock.
     * The dock shows up to four live-job or session-state items.
     * Each item carries a label, a trust-state label, and a short note.
     * Valid trust-state labels and their CSS mappings:
     *   - "Ready" / "Saved" / "Stable"  → status-pill-ready  (green)
     *   - "Running" / "Prepared" / "Primary" / "Pinned" / "Active" / "Queued"
     *                                    → status-pill-info   (blue)
     *   - "Risk" / "Blocked"            → status-pill-risk   (red)
     *   - all others (e.g. "Pending", "Attention", "Background")
     *                                    → status-pill-attention (amber)
     * @param {string} label      - Item label (e.g. "Doctor", "Reflection")
     * @param {string} stateLabel - Trust-state word (see valid labels above)
     * @param {string} noteText   - Short context note
     */
    function dock(label, stateLabel, noteText) {
        return { label, stateLabel, note: noteText };
    }

    /**
     * Wrap a view model object — identity function used to signal intent.
     * All route data is passed through screen() so that the shape is explicit
     * and consistent across routes.
     * @param {object} model - Route model object (metrics, lead, companion, panels, inspector, dock)
     */
    function screen(model) {
        return model;
    }

    function parseHash() {
        const cleaned = window.location.hash.replace(/^#/, "");
        if (!cleaned) {
            return;
        }
        const [familyId, viewId] = cleaned.split("/");
        if (routes[`${familyId}:${viewId}`]) {
            state.familyId = familyId;
            state.viewId = viewId;
        }
    }

    function render() {
        const family = fixtures.families.find((item) => item.id === state.familyId) || fixtures.families[0];
        const view = routes[`${state.familyId}:${state.viewId}`];
        els.routeKicker.textContent = `${family.label} / ${state.viewId.replace("-", " ")}`;
        els.routeTitle.textContent = view.title;
        els.routeStage.textContent = view.stage;
        els.routeSummary.textContent = view.summary;
        renderFamilyNav();
        renderViewSwitcher(family);
        renderStage(view);
        renderInspector(view.inspector);
        renderDock(view.dock);
        renderCommandResults();
        requestAnimationFrame(() => {
            const scroller = els.stage.closest(".stage-wrap");
            if (scroller) {
                scroller.scrollTop = 0;
            }
            document.documentElement.scrollTop = 0;
            document.body.scrollTop = 0;
        });
    }

    function renderFamilyNav() {
        els.familyNav.innerHTML = fixtures.families.map((family) => `
            <a class="family-link ${family.id === state.familyId ? "is-active" : ""}" href="#${family.id}/${family.views[0].id}">
                <strong>${family.label}</strong>
                <span>${family.summary}</span>
            </a>
        `).join("");
    }

    function renderViewSwitcher(family) {
        els.viewSwitcher.innerHTML = family.views.map((view) => `
            <a class="view-chip ${view.id === state.viewId ? "is-active" : ""}" href="#${family.id}/${view.id}">${view.label}</a>
        `).join("");
    }

    function renderStage(view) {
        els.stage.innerHTML = `
            <section class="metrics-strip">${view.metrics.map(renderMetric).join("")}</section>
            <section class="board-grid">
                <article class="panel panel-hero span-7">
                    <div class="panel-header"><div><p class="panel-kicker">${view.lead.eyebrow}</p><h3 class="panel-title">${view.lead.title}</h3></div></div>
                    <p class="panel-copy">${view.lead.body}</p>
                    <div class="stacked-actions">${view.lead.actions.map((action) => `<span class="action-pill">${action}</span>`).join("")}</div>
                </article>
                <article class="panel span-5">
                    <div class="panel-header"><div><p class="panel-kicker">${view.companion.eyebrow}</p><h3 class="panel-title">${view.companion.title}</h3></div></div>
                    <p class="companion-copy">${view.companion.body}</p>
                </article>
                ${view.panels.map(renderPanel).join("")}
            </section>
        `;
    }

    /**
     * Render one metric card.
     * Produces an <article class="metric-card"> with a label, prominent value,
     * and a meta note. Four metric cards appear in the .metrics-strip at the
     * top of every route stage, giving an instant operational snapshot.
     */
    function renderMetric(item) {
        return `<article class="metric-card"><span class="metric-label">${item.label}</span><div class="metric-row"><strong class="metric-value">${item.value}</strong><span class="metric-meta">${item.meta}</span></div></article>`;
    }

    /**
     * Render one secondary panel in the .board-grid.
     *
     * Two layout modes are supported, determined by which property the model carries:
     *
     * rows layout  (.row-list)
     *   Used when each item has a label, a value, and a note (structured tables,
     *   scoring dimensions, subsystem states, project readiness rows).
     *   Each row renders as .data-row > .data-row-main > .row-label + .row-value
     *   followed by a .row-note annotation.
     *
     * list layout  (.key-list)
     *   Used when items stand alone as actionable instructions or launcher links
     *   (review queues, support actions, evidence chains, milestone lists).
     *   Each item renders as .key-row > <strong>.
     *
     * The panel span class (e.g. span-4, span-7) is set by the data model and
     * controls how many grid columns the panel occupies.
     */
    function renderPanel(item) {
        const body = item.rows
            ? `<div class="row-list">${item.rows.map((line) => `<div class="data-row"><div class="data-row-main"><span class="row-label">${line.label}</span><span class="row-value">${line.value}</span></div><span class="row-note">${line.note}</span></div>`).join("")}</div>`
            : `<div class="key-list">${item.list.map((line) => `<div class="key-row"><strong>${line}</strong></div>`).join("")}</div>`;
        return `<article class="panel ${item.span}"><div class="panel-header"><div><p class="panel-kicker">${item.eyebrow}</p><h3 class="panel-title">${item.title}</h3></div></div><p class="panel-copy">${item.body}</p>${body}</article>`;
    }

    function renderInspector(model) {
        els.inspector.innerHTML = `
            <div class="inspector-card">
                <p class="kicker">Inspector</p>
                <h3>${model.title}</h3>
                <p class="route-summary">${model.subtitle}</p>
                ${Object.entries(model.sections).map(([label, items]) => `
                    <section class="inspector-section">
                        <h4>${label}</h4>
                        <ul>${items.map((item) => `<li>${item}</li>`).join("")}</ul>
                    </section>
                `).join("")}
            </div>
        `;
    }

    /**
     * Render the activity dock.
     * The dock shows up to four live-job or session-state items as .dock-item
     * elements. Each item carries a .status-pill whose CSS class is set by
     * statusClass(), a label, and a short context note.
     */
    function renderDock(items) {
        els.dock.innerHTML = items.map((item) => `
            <div class="dock-item">
                <span class="status-pill ${statusClass(item.stateLabel)}">${item.stateLabel}</span>
                <strong>${item.label}</strong>
                <span class="activity-note">${item.note}</span>
            </div>
        `).join("");
    }

    /**
     * Map a trust-state label to a status-pill CSS modifier class.
     *
     * The shared trust vocabulary is intentionally limited so that every
     * surface (Daily Desk, Runtime Control, Developer Portal, Customer App)
     * uses the same visual language without inventing new states.
     *
     * Mapping:
     *   ready   (green)  — "Ready", "Saved", "Stable"
     *   info    (blue)   — "Running", "Prepared", "Primary", "Pinned", "Active", "Queued"
     *   risk    (red)    — "Risk", "Blocked"
     *   attention (amber) — everything else (e.g. "Pending", "Attention",
     *                       "Background", "Unavailable", "Needs attention")
     *
     * Customer App surfaces only use Ready, Background, Needs attention, and
     * Unavailable — keeping the vocabulary product-safe and calm.
     *
     * @param {string} label - Trust-state label from a dock item or status badge
     * @returns {string} CSS class name
     */
    function statusClass(label) {
        const value = label.toLowerCase();
        if (["ready", "saved", "stable"].includes(value)) return "status-pill-ready";
        if (["running", "prepared", "primary", "pinned", "active", "queued"].includes(value)) return "status-pill-info";
        if (["risk", "blocked"].includes(value)) return "status-pill-risk";
        return "status-pill-attention";
    }

    function renderCommandResults() {
        const query = state.commandQuery.trim().toLowerCase();
        const items = Object.entries(routes).map(([key, route]) => {
            const [familyId, viewId] = key.split(":");
            const family = fixtures.families.find((item) => item.id === familyId);
            return {
                hash: `#${familyId}/${viewId}`,
                family: family.label,
                label: viewId.replace("-", " "),
                title: route.title,
                summary: route.summary
            };
        }).filter((item) => !query || `${item.family} ${item.label} ${item.title} ${item.summary}`.toLowerCase().includes(query));

        els.commandResults.innerHTML = items.length
            ? items.map((item) => `<button class="command-result" type="button" data-hash="${item.hash}"><span class="command-result-meta">${item.family} / ${item.label}</span><strong>${item.title}</strong><p>${item.summary}</p></button>`).join("")
            : `<div class="command-empty"><strong>No matching surface</strong><p>Try diagnostics, projects, training, or developer portal.</p></div>`;
    }

    function setCommandOpen(open) {
        els.commandOverlay.classList.toggle("is-hidden", !open);
        els.commandOverlay.setAttribute("aria-hidden", open ? "false" : "true");
        if (open) {
            els.commandInput.focus();
            els.commandInput.select();
        } else {
            state.commandQuery = "";
            els.commandInput.value = "";
            renderCommandResults();
        }
    }

    function bind() {
        window.addEventListener("hashchange", () => {
            parseHash();
            render();
        });

        window.addEventListener("keydown", (event) => {
            if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
                event.preventDefault();
                setCommandOpen(true);
            }
            if (event.key === "Escape") {
                setCommandOpen(false);
            }
        });

        els.commandToggle.addEventListener("click", () => setCommandOpen(true));
        els.commandClose.addEventListener("click", () => setCommandOpen(false));
        els.commandBackdrop.addEventListener("click", () => setCommandOpen(false));
        els.inspectorToggle.addEventListener("click", () => document.body.classList.toggle("inspector-open"));
        els.commandInput.addEventListener("input", () => {
            state.commandQuery = els.commandInput.value;
            renderCommandResults();
        });

        document.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }
            const jump = target.closest("[data-hash]");
            if (jump instanceof HTMLElement && jump.dataset.hash) {
                setCommandOpen(false);
                window.location.hash = jump.dataset.hash;
            }
        });
    }

    parseHash();
    bind();
    render();
})();
