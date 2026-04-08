/**
 * Integration tests for Review Queue and Rubric components.
 * Loads fixtures.js and app.js into a jsdom environment, navigates to the
 * relevant route, and asserts on the rendered DOM.
 *
 * @jest-environment jest-environment-jsdom
 */
'use strict';

const fs = require('fs');
const path = require('path');

const fixturesSource = fs.readFileSync(path.join(__dirname, 'fixtures.js'), 'utf8');
const appSource = fs.readFileSync(path.join(__dirname, 'app.js'), 'utf8');

/** Minimal DOM shell that mirrors index.html element IDs. */
const HTML_SHELL = `
  <nav id="family-nav"></nav>
  <p id="route-kicker"></p>
  <h2 id="route-title"></h2>
  <span id="route-stage"></span>
  <p id="route-summary"></p>
  <div id="view-switcher"></div>
  <main class="stage-wrap"><div id="stage"></div></main>
  <aside id="inspector"></aside>
  <footer id="activity-dock"></footer>
  <div id="command-overlay" class="is-hidden" aria-hidden="true">
    <div id="command-backdrop"></div>
    <input id="command-input" type="text" />
    <div id="command-results"></div>
  </div>
  <button id="command-toggle"></button>
  <button id="command-close"></button>
  <button id="inspector-toggle"></button>
`;

/**
 * Re-initialise the DOM, set the URL hash, then evaluate fixtures + app.
 * Each call gives the IIFE fresh element references so stale window listeners
 * from previous calls operate on detached nodes and become no-ops.
 */
function initApp(hash) {
    document.body.innerHTML = HTML_SHELL;
    delete window.storyboardFixtures;
    window.location.hash = hash || '';
    // eslint-disable-next-line no-eval
    eval(fixturesSource);
    // eslint-disable-next-line no-eval
    eval(appSource);
}

/**
 * Return the first panel whose .panel-kicker (eyebrow) text matches `label`.
 *
 * In renderPanel the second argument to rows()/list() — the eyebrow — becomes
 * the .panel-kicker element (e.g. "Review queue", "Rubric"). The third
 * argument (title) becomes .panel-title.
 */
function getPanelByEyebrow(label) {
    for (const panel of document.querySelectorAll('.panel')) {
        const kicker = panel.querySelector('.panel-kicker');
        if (kicker && kicker.textContent.trim() === label) {
            return panel;
        }
    }
    return null;
}

// ---------------------------------------------------------------------------
// Review Queue component
// ---------------------------------------------------------------------------
describe('Review Queue component (daily-desk:session)', () => {
    beforeAll(() => {
        initApp('#daily-desk/session');
    });

    it('renders a Review queue panel on the session stage', () => {
        expect(getPanelByEyebrow('Review queue')).not.toBeNull();
    });

    it('shows the panel title "Next recommended review"', () => {
        const panel = getPanelByEyebrow('Review queue');
        const title = panel.querySelector('.panel-title');
        expect(title.textContent.trim()).toBe('Next recommended review');
    });

    it('shows the copy describing how the queue is generated', () => {
        const panel = getPanelByEyebrow('Review queue');
        const copy = panel.querySelector('.panel-copy');
        expect(copy.textContent.trim()).toBe(
            'The next run is generated from weak-topic recurrence.'
        );
    });

    it('renders exactly three workflow recommendation items', () => {
        const panel = getPanelByEyebrow('Review queue');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(3);
    });

    it('includes "Revisit relay timing after lunch" as a recommendation', () => {
        const panel = getPanelByEyebrow('Review queue');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Revisit relay timing after lunch');
    });

    it('includes "Link grounding explanation to field validation" as a recommendation', () => {
        const panel = getPanelByEyebrow('Review queue');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Link grounding explanation to field validation');
    });

    it('includes "Promote one reflection into a study note" as a recommendation', () => {
        const panel = getPanelByEyebrow('Review queue');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Promote one reflection into a study note');
    });

    it('uses a key-list layout (not a row-list)', () => {
        const panel = getPanelByEyebrow('Review queue');
        expect(panel.querySelector('.key-list')).not.toBeNull();
        expect(panel.querySelector('.row-list')).toBeNull();
    });
});

// ---------------------------------------------------------------------------
// Rubric component
// ---------------------------------------------------------------------------
describe('Rubric component (daily-desk:session)', () => {
    beforeAll(() => {
        initApp('#daily-desk/session');
    });

    it('renders a Rubric panel on the session stage', () => {
        expect(getPanelByEyebrow('Rubric')).not.toBeNull();
    });

    it('shows the panel title "Defense scoring breakdown"', () => {
        const panel = getPanelByEyebrow('Rubric');
        const title = panel.querySelector('.panel-title');
        expect(title.textContent.trim()).toBe('Defense scoring breakdown');
    });

    it('shows the copy describing the rubric intent', () => {
        const panel = getPanelByEyebrow('Rubric');
        const copy = panel.querySelector('.panel-copy');
        expect(copy.textContent.trim()).toBe('Five dimensions, one direct next move.');
    });

    it('renders exactly three scoring dimensions', () => {
        const panel = getPanelByEyebrow('Rubric');
        const dataRows = panel.querySelectorAll('.data-row');
        expect(dataRows).toHaveLength(3);
    });

    it('shows Correctness scored at 4 / 5', () => {
        const panel = getPanelByEyebrow('Rubric');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Correctness');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('4 / 5');
    });

    it('shows Tradeoffs scored at 2 / 5', () => {
        const panel = getPanelByEyebrow('Rubric');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Tradeoffs');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('2 / 5');
    });

    it('shows Validation scored at 3 / 5', () => {
        const panel = getPanelByEyebrow('Rubric');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Validation');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('3 / 5');
    });

    it('attaches a note to each scoring dimension', () => {
        const panel = getPanelByEyebrow('Rubric');
        const notes = Array.from(panel.querySelectorAll('.row-note')).map(
            (n) => n.textContent.trim()
        );
        expect(notes).toContain('strong fundamentals');
        expect(notes).toContain('needs comparison language');
        expect(notes).toContain('more test thinking needed');
    });

    it('uses a row-list layout (not a key-list)', () => {
        const panel = getPanelByEyebrow('Rubric');
        expect(panel.querySelector('.row-list')).not.toBeNull();
        expect(panel.querySelector('.key-list')).toBeNull();
    });
});

// ---------------------------------------------------------------------------
// statusClass scoring logic (tested through the activity dock)
// ---------------------------------------------------------------------------
describe('statusClass scoring logic', () => {
    describe('in session view dock', () => {
        beforeAll(() => {
            initApp('#daily-desk/session');
        });

        it('applies status-pill-ready to "Saved" items', () => {
            const dock = document.getElementById('activity-dock');
            const savedPills = Array.from(dock.querySelectorAll('.status-pill')).filter(
                (p) => p.textContent.trim() === 'Saved'
            );
            expect(savedPills.length).toBeGreaterThanOrEqual(1);
            savedPills.forEach((pill) =>
                expect(pill.classList.contains('status-pill-ready')).toBe(true)
            );
        });

        it('applies status-pill-attention to "Pending" items', () => {
            const dock = document.getElementById('activity-dock');
            const pendingPills = Array.from(dock.querySelectorAll('.status-pill')).filter(
                (p) => p.textContent.trim() === 'Pending'
            );
            expect(pendingPills.length).toBeGreaterThanOrEqual(1);
            pendingPills.forEach((pill) =>
                expect(pill.classList.contains('status-pill-attention')).toBe(true)
            );
        });

        it('applies status-pill-info to "Prepared" items', () => {
            const dock = document.getElementById('activity-dock');
            const preparedPills = Array.from(dock.querySelectorAll('.status-pill')).filter(
                (p) => p.textContent.trim() === 'Prepared'
            );
            expect(preparedPills.length).toBeGreaterThanOrEqual(1);
            preparedPills.forEach((pill) =>
                expect(pill.classList.contains('status-pill-info')).toBe(true)
            );
        });
    });

    describe('in hero view dock', () => {
        beforeAll(() => {
            initApp('#daily-desk/hero');
        });

        it('applies status-pill-risk to "Blocked" items', () => {
            const dock = document.getElementById('activity-dock');
            const blockedPills = Array.from(dock.querySelectorAll('.status-pill')).filter(
                (p) => p.textContent.trim() === 'Blocked'
            );
            expect(blockedPills.length).toBeGreaterThanOrEqual(1);
            blockedPills.forEach((pill) =>
                expect(pill.classList.contains('status-pill-risk')).toBe(true)
            );
        });

        it('applies status-pill-info to "Running" items', () => {
            const dock = document.getElementById('activity-dock');
            const runningPills = Array.from(dock.querySelectorAll('.status-pill')).filter(
                (p) => p.textContent.trim() === 'Running'
            );
            expect(runningPills.length).toBeGreaterThanOrEqual(1);
            runningPills.forEach((pill) =>
                expect(pill.classList.contains('status-pill-info')).toBe(true)
            );
        });

        it('applies status-pill-ready to "Ready" items', () => {
            const dock = document.getElementById('activity-dock');
            const readyPills = Array.from(dock.querySelectorAll('.status-pill')).filter(
                (p) => p.textContent.trim() === 'Ready'
            );
            expect(readyPills.length).toBeGreaterThanOrEqual(1);
            readyPills.forEach((pill) =>
                expect(pill.classList.contains('status-pill-ready')).toBe(true)
            );
        });
    });
});

// ---------------------------------------------------------------------------
// Session view overall structure
// ---------------------------------------------------------------------------
describe('Session view overall structure', () => {
    beforeAll(() => {
        initApp('#daily-desk/session');
    });

    it('sets the route title to "Active Session"', () => {
        expect(document.getElementById('route-title').textContent.trim()).toBe('Active Session');
    });

    it('sets the route stage to "Structural detail"', () => {
        expect(document.getElementById('route-stage').textContent.trim()).toBe('Structural detail');
    });

    it('renders four metrics cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders both Rubric and Review queue on the same stage', () => {
        expect(getPanelByEyebrow('Rubric')).not.toBeNull();
        expect(getPanelByEyebrow('Review queue')).not.toBeNull();
    });

    it('renders the Session timeline panel listing the training stages', () => {
        const panel = getPanelByEyebrow('Session timeline');
        expect(panel).not.toBeNull();
        const items = panel.querySelectorAll('.key-row strong');
        expect(items.length).toBeGreaterThanOrEqual(5);
    });

    it('renders four dock items for the session', () => {
        const dockItems = document.querySelectorAll('#activity-dock .dock-item');
        expect(dockItems).toHaveLength(4);
    });
});

// ---------------------------------------------------------------------------
// Command palette interaction
// ---------------------------------------------------------------------------
describe('Command palette interaction', () => {
    beforeEach(() => {
        initApp('');
    });

    it('is hidden by default', () => {
        const overlay = document.getElementById('command-overlay');
        expect(overlay.classList.contains('is-hidden')).toBe(true);
        expect(overlay.getAttribute('aria-hidden')).toBe('true');
    });

    it('opens when the command-toggle button is clicked', () => {
        const overlay = document.getElementById('command-overlay');
        document.getElementById('command-toggle').click();
        expect(overlay.classList.contains('is-hidden')).toBe(false);
        expect(overlay.getAttribute('aria-hidden')).toBe('false');
    });

    it('closes when the command-close button is clicked', () => {
        const overlay = document.getElementById('command-overlay');
        document.getElementById('command-toggle').click();
        document.getElementById('command-close').click();
        expect(overlay.classList.contains('is-hidden')).toBe(true);
    });

    it('lists all registered routes when opened', () => {
        document.getElementById('command-toggle').click();
        const results = document.querySelectorAll('.command-result');
        // 4 families × 2 views = 8 routes minimum
        expect(results.length).toBeGreaterThanOrEqual(8);
    });

    it('filters results when the user types in the search input', () => {
        document.getElementById('command-toggle').click();
        const input = document.getElementById('command-input');
        input.value = 'session';
        input.dispatchEvent(new Event('input'));
        const results = document.querySelectorAll('.command-result');
        expect(results.length).toBeGreaterThan(0);
        expect(results.length).toBeLessThan(8);
    });

    it('shows an empty state when the query matches nothing', () => {
        document.getElementById('command-toggle').click();
        const input = document.getElementById('command-input');
        input.value = 'zzz-no-match-xyz';
        input.dispatchEvent(new Event('input'));
        expect(document.querySelector('.command-empty')).not.toBeNull();
    });

    it('resets the search and re-renders all results after closing', () => {
        document.getElementById('command-toggle').click();
        const input = document.getElementById('command-input');
        input.value = 'runtime';
        input.dispatchEvent(new Event('input'));
        document.getElementById('command-close').click();
        // Re-open: results should show all routes again
        document.getElementById('command-toggle').click();
        const results = document.querySelectorAll('.command-result');
        expect(results.length).toBeGreaterThanOrEqual(8);
    });
});

// ---------------------------------------------------------------------------
// Architecture and code panel — Refactor pressure notes (developer-portal:hero)
// ---------------------------------------------------------------------------
describe('Architecture and code panel (developer-portal:hero)', () => {
    beforeAll(() => {
        initApp('#developer-portal/hero');
    });

    it('renders an "Architecture and code" panel on the developer portal stage', () => {
        expect(getPanelByEyebrow('Architecture and code')).not.toBeNull();
    });

    it('shows the panel title "Maps, graphs, and refactor surfaces"', () => {
        const panel = getPanelByEyebrow('Architecture and code');
        const title = panel.querySelector('.panel-title');
        expect(title.textContent.trim()).toBe('Maps, graphs, and refactor surfaces');
    });

    it('shows the copy describing portal access intent', () => {
        const panel = getPanelByEyebrow('Architecture and code');
        const copy = panel.querySelector('.panel-copy');
        expect(copy.textContent.trim()).toBe(
            'The portal gives quick access, but tool pages stay focused.'
        );
    });

    it('renders exactly three items', () => {
        const panel = getPanelByEyebrow('Architecture and code');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(3);
    });

    it('includes "Architecture Map" as an item', () => {
        const panel = getPanelByEyebrow('Architecture and code');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Architecture Map');
    });

    it('includes "Architecture Graph" as an item', () => {
        const panel = getPanelByEyebrow('Architecture and code');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Architecture Graph');
    });

    it('includes "Refactor pressure notes" as an item', () => {
        const panel = getPanelByEyebrow('Architecture and code');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Refactor pressure notes');
    });

    it('uses a key-list layout (not a row-list)', () => {
        const panel = getPanelByEyebrow('Architecture and code');
        expect(panel.querySelector('.key-list')).not.toBeNull();
        expect(panel.querySelector('.row-list')).toBeNull();
    });
});

// ---------------------------------------------------------------------------
// Hash routing / navigation
// ---------------------------------------------------------------------------
describe('Hash routing', () => {
    it('defaults to the Daily Desk hero view when no hash is present', () => {
        initApp('');
        expect(document.getElementById('route-title').textContent.trim()).toBe('Daily Desk');
    });

    it('routes to the session view via #daily-desk/session', () => {
        initApp('#daily-desk/session');
        expect(document.getElementById('route-title').textContent.trim()).toBe('Active Session');
    });

    it('routes to Runtime Control via #runtime-control/hero', () => {
        initApp('#runtime-control/hero');
        expect(document.getElementById('route-title').textContent.trim()).toBe('Runtime Control');
    });

    it('routes to the Diagnostics view via #runtime-control/diagnostics', () => {
        initApp('#runtime-control/diagnostics');
        expect(document.getElementById('route-title').textContent.trim()).toBe('Diagnostics Detail');
    });

    it('routes to Developer Portal via #developer-portal/hero', () => {
        initApp('#developer-portal/hero');
        expect(document.getElementById('route-title').textContent.trim()).toBe('Developer Portal');
    });

    it('routes to Customer App via #customer-app/hero', () => {
        initApp('#customer-app/hero');
        expect(document.getElementById('route-title').textContent.trim()).toBe('Customer App');
    });

    it('renders view switcher chips for the active family', () => {
        initApp('#daily-desk/session');
        const chips = document.querySelectorAll('.view-chip');
        expect(chips).toHaveLength(2); // Hero + Session
    });

    it('marks only the current view chip as active', () => {
        initApp('#daily-desk/session');
        const chips = Array.from(document.querySelectorAll('.view-chip'));
        const activeChips = chips.filter((c) => c.classList.contains('is-active'));
        expect(activeChips).toHaveLength(1);
        expect(activeChips[0].textContent.trim()).toBe('Session');
    });

    it('renders family nav links for all four product families', () => {
        initApp('');
        const familyLinks = document.querySelectorAll('.family-link');
        expect(familyLinks).toHaveLength(4);
    });

    it('marks the active family link with is-active', () => {
        initApp('#runtime-control/hero');
        const links = Array.from(document.querySelectorAll('.family-link'));
        const active = links.find((l) => l.classList.contains('is-active'));
        expect(active).not.toBeNull();
        expect(active.querySelector('strong').textContent.trim()).toBe('Runtime Control');
    });
});

// ---------------------------------------------------------------------------
// Developer Portal hero view overall structure
// ---------------------------------------------------------------------------
describe('Developer Portal hero view overall structure', () => {
    beforeAll(() => {
        initApp('#developer-portal/hero');
    });

    it('sets the route title to "Developer Portal"', () => {
        expect(document.getElementById('route-title').textContent.trim()).toBe('Developer Portal');
    });

    it('sets the route stage to "High-fidelity hero"', () => {
        expect(document.getElementById('route-stage').textContent.trim()).toBe('High-fidelity hero');
    });

    it('sets the route kicker to include "Developer Portal"', () => {
        expect(document.getElementById('route-kicker').textContent).toContain('Developer Portal');
    });

    it('renders four metrics cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders the Architecture and code panel alongside sibling panels', () => {
        expect(getPanelByEyebrow('Architecture and code')).not.toBeNull();
        expect(getPanelByEyebrow('Publishing and evidence')).not.toBeNull();
        expect(getPanelByEyebrow('Automation lab')).not.toBeNull();
    });

    it('renders the Agent lab panel', () => {
        expect(getPanelByEyebrow('Agent lab')).not.toBeNull();
    });

    it('renders the Developer docs panel', () => {
        expect(getPanelByEyebrow('Developer docs')).not.toBeNull();
    });

    it('renders four dock items for the developer portal', () => {
        const dockItems = document.querySelectorAll('#activity-dock .dock-item');
        expect(dockItems).toHaveLength(4);
    });

    it('renders the hero lead panel with eyebrow "Workshop launcher"', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        expect(panelHero.querySelector('.panel-kicker').textContent.trim()).toBe('Workshop launcher');
    });

    it('renders the companion panel with eyebrow "Reference move"', () => {
        const panels = Array.from(document.querySelectorAll('.panel'));
        const companion = panels.find((p) => {
            const kicker = p.querySelector('.panel-kicker');
            return kicker && kicker.textContent.trim() === 'Reference move';
        });
        expect(companion).not.toBeUndefined();
    });
});

// ---------------------------------------------------------------------------
// Refactor pressure notes — Architecture metric alignment
// Verifies the "Architecture" metrics card correctly signals the two live
// architecture surfaces (map and graph) that accompany Refactor pressure notes
// in the Developer Portal storyboard.
// ---------------------------------------------------------------------------
describe('Refactor pressure notes — Architecture metric alignment (developer-portal:hero)', () => {
    beforeAll(() => {
        initApp('#developer-portal/hero');
    });

    it('renders an "Architecture" metric card', () => {
        const cards = Array.from(document.querySelectorAll('.metric-card'));
        const archCard = cards.find(
            (c) => c.querySelector('.metric-label').textContent.trim() === 'Architecture'
        );
        expect(archCard).not.toBeUndefined();
    });

    it('shows the Architecture metric value as "2 live"', () => {
        const cards = Array.from(document.querySelectorAll('.metric-card'));
        const archCard = cards.find(
            (c) => c.querySelector('.metric-label').textContent.trim() === 'Architecture'
        );
        expect(archCard.querySelector('.metric-value').textContent.trim()).toBe('2 live');
    });

    it('shows the Architecture metric meta text "map and graph"', () => {
        const cards = Array.from(document.querySelectorAll('.metric-card'));
        const archCard = cards.find(
            (c) => c.querySelector('.metric-label').textContent.trim() === 'Architecture'
        );
        expect(archCard.querySelector('.metric-meta').textContent.trim()).toBe('map and graph');
    });

    it('Architecture and code panel item count matches the number of live architecture surfaces plus refactor notes', () => {
        // The "Architecture" metric reports 2 live architecture surfaces (map and graph).
        // The panel contains those two plus "Refactor pressure notes" = 3 items total.
        const panel = getPanelByEyebrow('Architecture and code');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(3);
    });

    it('panel item order starts with architecture surfaces before refactor notes', () => {
        const panel = getPanelByEyebrow('Architecture and code');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts[0]).toBe('Architecture Map');
        expect(texts[1]).toBe('Architecture Graph');
        expect(texts[2]).toBe('Refactor pressure notes');
    });
});

// ---------------------------------------------------------------------------
// Developer Portal inspector
// Verifies the inspector panel for the developer portal hero surfaces the
// correct design rules that govern the "Refactor pressure notes" surface.
// ---------------------------------------------------------------------------
describe('Developer Portal inspector (developer-portal:hero)', () => {
    beforeAll(() => {
        initApp('#developer-portal/hero');
    });

    it('renders the inspector panel', () => {
        expect(document.getElementById('inspector').querySelector('.inspector-card')).not.toBeNull();
    });

    it('shows the inspector title "Portal rules"', () => {
        const title = document.getElementById('inspector').querySelector('h3');
        expect(title.textContent.trim()).toBe('Portal rules');
    });

    it('shows the inspector subtitle describing the portal intent', () => {
        const subtitle = document.getElementById('inspector').querySelector('.route-summary');
        expect(subtitle.textContent.trim()).toBe(
            'Launch and summarize, do not become a diagnostic duplicate.'
        );
    });

    it('renders inspector sections', () => {
        const sections = document.getElementById('inspector').querySelectorAll('.inspector-section');
        expect(sections.length).toBeGreaterThanOrEqual(1);
    });

    it('includes a "Keep" section in the inspector', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        expect(keepSection).not.toBeUndefined();
    });

    it('includes a "Reject" section in the inspector', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        expect(rejectSection).not.toBeUndefined();
    });

    it('"Keep" section lists grouped launcher by job', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        const items = Array.from(keepSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Grouped launcher by job');
    });

    it('"Reject" section lists flat tool-card grid', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        const items = Array.from(rejectSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Flat tool-card grid as the whole page');
    });
});

// ---------------------------------------------------------------------------
// Developer Portal dock
// Verifies the activity dock for the developer portal hero renders the correct
// status items and applies the right status-pill classes.
// ---------------------------------------------------------------------------
describe('Developer Portal dock (developer-portal:hero)', () => {
    beforeAll(() => {
        initApp('#developer-portal/hero');
    });

    it('renders a "Developer route" dock item', () => {
        const labels = Array.from(
            document.querySelectorAll('#activity-dock .dock-item strong')
        ).map((el) => el.textContent.trim());
        expect(labels).toContain('Developer route');
    });

    it('renders an "Automation lab" dock item', () => {
        const labels = Array.from(
            document.querySelectorAll('#activity-dock .dock-item strong')
        ).map((el) => el.textContent.trim());
        expect(labels).toContain('Automation lab');
    });

    it('renders an "Agent lab" dock item', () => {
        const labels = Array.from(
            document.querySelectorAll('#activity-dock .dock-item strong')
        ).map((el) => el.textContent.trim());
        expect(labels).toContain('Agent lab');
    });

    it('renders a "Runtime Control" dock item', () => {
        const labels = Array.from(
            document.querySelectorAll('#activity-dock .dock-item strong')
        ).map((el) => el.textContent.trim());
        expect(labels).toContain('Runtime Control');
    });

    it('applies status-pill-ready to the "Developer route" dock item (Ready)', () => {
        const items = Array.from(document.querySelectorAll('#activity-dock .dock-item'));
        const devRoute = items.find(
            (item) => item.querySelector('strong').textContent.trim() === 'Developer route'
        );
        expect(devRoute).not.toBeUndefined();
        const pill = devRoute.querySelector('.status-pill');
        expect(pill.classList.contains('status-pill-ready')).toBe(true);
    });

    it('applies status-pill-info to the "Automation lab" dock item (Active)', () => {
        const items = Array.from(document.querySelectorAll('#activity-dock .dock-item'));
        const labItem = items.find(
            (item) => item.querySelector('strong').textContent.trim() === 'Automation lab'
        );
        expect(labItem).not.toBeUndefined();
        const pill = labItem.querySelector('.status-pill');
        expect(pill.classList.contains('status-pill-info')).toBe(true);
    });

    it('applies status-pill-info to the "Runtime Control" dock item (Primary)', () => {
        const items = Array.from(document.querySelectorAll('#activity-dock .dock-item'));
        const runtimeItem = items.find(
            (item) => item.querySelector('strong').textContent.trim() === 'Runtime Control'
        );
        expect(runtimeItem).not.toBeUndefined();
        const pill = runtimeItem.querySelector('.status-pill');
        expect(pill.classList.contains('status-pill-info')).toBe(true);
    });
});

// ---------------------------------------------------------------------------
// Workshop launcher grouping — job categories and release state
// Verifies the Workshop launcher in developer-portal:hero correctly groups
// launchers by job and surfaces release state on staged tools.
// ---------------------------------------------------------------------------
describe('Workshop launcher grouping — job categories (developer-portal:hero)', () => {
    beforeAll(() => {
        initApp('#developer-portal/hero');
    });

    it('Workshop launcher panel body describes grouping by job and release state', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        const copy = panelHero.querySelector('.panel-copy');
        expect(copy.textContent).toContain('groups launchers by job and release state');
    });

    it('Workshop launcher copy states that dense diagnostics are deferred to Runtime Control', () => {
        const panelHero = document.querySelector('.panel-hero');
        const copy = panelHero.querySelector('.panel-copy');
        expect(copy.textContent).toContain('defers dense diagnostics back to Runtime Control');
    });

    it('renders "Open Runtime Control" as the first Workshop launcher action', () => {
        const panelHero = document.querySelector('.panel-hero');
        const actions = Array.from(panelHero.querySelectorAll('.action-pill')).map(
            (el) => el.textContent.trim()
        );
        expect(actions[0]).toBe('Open Runtime Control');
    });

    it('renders "Launch Architecture Map" as the second Workshop launcher action', () => {
        const panelHero = document.querySelector('.panel-hero');
        const actions = Array.from(panelHero.querySelectorAll('.action-pill')).map(
            (el) => el.textContent.trim()
        );
        expect(actions[1]).toBe('Launch Architecture Map');
    });

    it('renders "Review future products" as the third Workshop launcher action', () => {
        const panelHero = document.querySelector('.panel-hero');
        const actions = Array.from(panelHero.querySelectorAll('.action-pill')).map(
            (el) => el.textContent.trim()
        );
        expect(actions[2]).toBe('Review future products');
    });

    it('renders all five job-grouped launcher panels', () => {
        const panelEyebrows = Array.from(document.querySelectorAll('.panel .panel-kicker')).map(
            (el) => el.textContent.trim()
        );
        expect(panelEyebrows).toContain('Publishing and evidence');
        expect(panelEyebrows).toContain('Automation lab');
        expect(panelEyebrows).toContain('Agent lab');
        expect(panelEyebrows).toContain('Architecture and code');
        expect(panelEyebrows).toContain('Developer docs');
    });

    it('Publishing and evidence panel uses a key-list layout', () => {
        const panel = getPanelByEyebrow('Publishing and evidence');
        expect(panel.querySelector('.key-list')).not.toBeNull();
        expect(panel.querySelector('.row-list')).toBeNull();
    });

    it('Publishing and evidence panel lists three items', () => {
        const panel = getPanelByEyebrow('Publishing and evidence');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(3);
    });

    it('Automation lab panel lists four tools', () => {
        const panel = getPanelByEyebrow('Automation lab');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(4);
    });

    it('Automation lab panel includes "AutoDraft Studio"', () => {
        const panel = getPanelByEyebrow('Automation lab');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('AutoDraft Studio');
    });

    it('Agent lab panel uses a row-list layout to surface release state', () => {
        const panel = getPanelByEyebrow('Agent lab');
        expect(panel.querySelector('.row-list')).not.toBeNull();
        expect(panel.querySelector('.key-list')).toBeNull();
    });

    it('Agent lab panel renders exactly two rows', () => {
        const panel = getPanelByEyebrow('Agent lab');
        const dataRows = panel.querySelectorAll('.data-row');
        expect(dataRows).toHaveLength(2);
    });

    it('Agent lab panel shows "Agents route" row with "Developer beta" release state', () => {
        const panel = getPanelByEyebrow('Agent lab');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Agents route');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Developer beta');
    });

    it('Agent lab panel shows "Pairing state" row with "Experimental" release state', () => {
        const panel = getPanelByEyebrow('Agent lab');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Pairing state');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Experimental');
    });
});

// ---------------------------------------------------------------------------
// Workshop launcher — dense diagnostics deferral
// Verifies that dense diagnostics are explicitly deferred back to Runtime
// Control and that the inspector design rules reinforce this separation.
// ---------------------------------------------------------------------------
describe('Workshop launcher — dense diagnostics deferral (developer-portal:hero)', () => {
    beforeAll(() => {
        initApp('#developer-portal/hero');
    });

    it('Workshop launcher copy references deferral of diagnostics to Runtime Control and Command Center', () => {
        const panelHero = document.querySelector('.panel-hero');
        const copy = panelHero.querySelector('.panel-copy');
        expect(copy.textContent).toContain('Runtime Control and Command Center');
    });

    it('inspector "Keep" section lists "Release state and future-product callouts"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        const items = Array.from(keepSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Release state and future-product callouts');
    });

    it('inspector "Keep" section lists "Runtime requirements as support metadata"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        const items = Array.from(keepSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Runtime requirements as support metadata');
    });

    it('inspector "Reject" section lists "Command Center density on the front page"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        const items = Array.from(rejectSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Command Center density on the front page');
    });

    it('inspector "Reject" section lists "Customer-visible lab language"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        const items = Array.from(rejectSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Customer-visible lab language');
    });
});

// ---------------------------------------------------------------------------
// Developer Portal tool-detail — overall structure
// Verifies that navigating to developer-portal:tool-detail renders the correct
// title, stage label, metric cards, and lead / companion panels.
// ---------------------------------------------------------------------------
describe('Developer Portal tool-detail — overall structure (developer-portal:tool-detail)', () => {
    beforeAll(() => {
        initApp('#developer-portal/tool-detail');
    });

    it('sets the route title to "Tool Detail"', () => {
        expect(document.getElementById('route-title').textContent.trim()).toBe('Tool Detail');
    });

    it('sets the route stage to "Structural detail"', () => {
        expect(document.getElementById('route-stage').textContent.trim()).toBe('Structural detail');
    });

    it('sets the route kicker to include "Developer Portal"', () => {
        expect(document.getElementById('route-kicker').textContent).toContain('Developer Portal');
    });

    it('renders four metric cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders the "Selected tool" metric with value "AutoDraft Studio"', () => {
        const cards = Array.from(document.querySelectorAll('.metric-card'));
        const card = cards.find(
            (c) => c.querySelector('.metric-label').textContent.trim() === 'Selected tool'
        );
        expect(card).not.toBeUndefined();
        expect(card.querySelector('.metric-value').textContent.trim()).toBe('AutoDraft Studio');
    });

    it('renders the "Release state" metric with value "Developer beta"', () => {
        const cards = Array.from(document.querySelectorAll('.metric-card'));
        const card = cards.find(
            (c) => c.querySelector('.metric-label').textContent.trim() === 'Release state'
        );
        expect(card).not.toBeUndefined();
        expect(card.querySelector('.metric-value').textContent.trim()).toBe('Developer beta');
    });

    it('renders the lead panel with eyebrow "Single-tool readiness"', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        expect(panelHero.querySelector('.panel-kicker').textContent.trim()).toBe(
            'Single-tool readiness'
        );
    });

    it('renders "Launch tool" as the first lead panel action', () => {
        const panelHero = document.querySelector('.panel-hero');
        const actions = Array.from(panelHero.querySelectorAll('.action-pill')).map(
            (el) => el.textContent.trim()
        );
        expect(actions[0]).toBe('Launch tool');
    });

    it('renders "Review graduation criteria" as the third lead panel action', () => {
        const panelHero = document.querySelector('.panel-hero');
        const actions = Array.from(panelHero.querySelectorAll('.action-pill')).map(
            (el) => el.textContent.trim()
        );
        expect(actions[2]).toBe('Review graduation criteria');
    });

    it('renders the companion panel with eyebrow "Why this exists"', () => {
        const panels = Array.from(document.querySelectorAll('.panel'));
        const companion = panels.find((p) => {
            const kicker = p.querySelector('.panel-kicker');
            return kicker && kicker.textContent.trim() === 'Why this exists';
        });
        expect(companion).not.toBeUndefined();
    });
});

// ---------------------------------------------------------------------------
// Developer Portal tool-detail — launch readiness panel
// Verifies that the Launch readiness panel correctly uses a row-list layout
// and surfaces the three runtime dependency rows with their current state.
// ---------------------------------------------------------------------------
describe('Developer Portal tool-detail — launch readiness panel (developer-portal:tool-detail)', () => {
    beforeAll(() => {
        initApp('#developer-portal/tool-detail');
    });

    it('renders the "Launch readiness" panel', () => {
        expect(getPanelByEyebrow('Launch readiness')).not.toBeNull();
    });

    it('"Launch readiness" panel uses a row-list layout', () => {
        const panel = getPanelByEyebrow('Launch readiness');
        expect(panel.querySelector('.row-list')).not.toBeNull();
        expect(panel.querySelector('.key-list')).toBeNull();
    });

    it('"Launch readiness" panel renders exactly three rows', () => {
        const panel = getPanelByEyebrow('Launch readiness');
        const dataRows = panel.querySelectorAll('.data-row');
        expect(dataRows).toHaveLength(3);
    });

    it('shows "Frontend route" row with value "Available"', () => {
        const panel = getPanelByEyebrow('Launch readiness');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Frontend route');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Available');
    });

    it('shows "Backend service" row with value "Ready"', () => {
        const panel = getPanelByEyebrow('Launch readiness');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Backend service');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Ready');
    });

    it('shows "Gateway bridge" row with value "Needs attention"', () => {
        const panel = getPanelByEyebrow('Launch readiness');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Gateway bridge');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Needs attention');
    });
});

// ---------------------------------------------------------------------------
// Developer Portal tool-detail — graduation and staging panels
// Verifies graduation path, future product fit, proof inputs, and route
// hygiene panels are rendered with correct layout and item counts.
// ---------------------------------------------------------------------------
describe('Developer Portal tool-detail — graduation and staging panels (developer-portal:tool-detail)', () => {
    beforeAll(() => {
        initApp('#developer-portal/tool-detail');
    });

    it('renders the "Graduation path" panel', () => {
        expect(getPanelByEyebrow('Graduation path')).not.toBeNull();
    });

    it('"Graduation path" panel uses a key-list layout', () => {
        const panel = getPanelByEyebrow('Graduation path');
        expect(panel.querySelector('.key-list')).not.toBeNull();
        expect(panel.querySelector('.row-list')).toBeNull();
    });

    it('"Graduation path" panel lists three items', () => {
        const panel = getPanelByEyebrow('Graduation path');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(3);
    });

    it('"Graduation path" panel includes "Tighten customer-safe copy"', () => {
        const panel = getPanelByEyebrow('Graduation path');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Tighten customer-safe copy');
    });

    it('"Graduation path" panel includes "Prove workflow value with operators"', () => {
        const panel = getPanelByEyebrow('Graduation path');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Prove workflow value with operators');
    });

    it('"Graduation path" panel includes "Remove lab-only controls from the route"', () => {
        const panel = getPanelByEyebrow('Graduation path');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Remove lab-only controls from the route');
    });

    it('"Graduation path" panel lists all three workflow steps in correct order', () => {
        const panel = getPanelByEyebrow('Graduation path');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toEqual([
            'Tighten customer-safe copy',
            'Prove workflow value with operators',
            'Remove lab-only controls from the route'
        ]);
    });

    it('renders the "Future product fit" panel', () => {
        expect(getPanelByEyebrow('Future product fit')).not.toBeNull();
    });

    it('renders the "Proof inputs" panel', () => {
        expect(getPanelByEyebrow('Proof inputs')).not.toBeNull();
    });

    it('renders the "Route hygiene" panel', () => {
        expect(getPanelByEyebrow('Route hygiene')).not.toBeNull();
    });

    it('"Route hygiene" panel lists three items', () => {
        const panel = getPanelByEyebrow('Route hygiene');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(3);
    });
});

// ---------------------------------------------------------------------------
// Developer Portal tool-detail — inspector and dock
// Verifies the inspector surfaces readiness rules with a "Show" section and
// that the dock reflects the correct tool states.
// ---------------------------------------------------------------------------
describe('Developer Portal tool-detail — inspector and dock (developer-portal:tool-detail)', () => {
    beforeAll(() => {
        initApp('#developer-portal/tool-detail');
    });

    it('inspector title is "Readiness rules"', () => {
        const title = document.getElementById('inspector').querySelector('h3');
        expect(title.textContent.trim()).toBe('Readiness rules');
    });

    it('inspector subtitle describes explicit staging requirement', () => {
        const subtitle = document.getElementById('inspector').querySelector('.route-summary');
        expect(subtitle.textContent.trim()).toBe(
            'Future product tools need explicit staging.'
        );
    });

    it('inspector has a "Show" section', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const showSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Show'
        );
        expect(showSection).not.toBeUndefined();
    });

    it('"Show" section lists "Current release state"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const showSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Show'
        );
        const items = Array.from(showSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Current release state');
    });

    it('"Show" section lists "Graduation path"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const showSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Show'
        );
        const items = Array.from(showSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Graduation path');
    });

    it('renders four dock items', () => {
        const dockItems = document.querySelectorAll('#activity-dock .dock-item');
        expect(dockItems).toHaveLength(4);
    });

    it('renders a "Selected tool" dock item', () => {
        const labels = Array.from(
            document.querySelectorAll('#activity-dock .dock-item strong')
        ).map((el) => el.textContent.trim());
        expect(labels).toContain('Selected tool');
    });

    it('renders a "Readiness" dock item with "Attention" state', () => {
        const items = Array.from(document.querySelectorAll('#activity-dock .dock-item'));
        const readinessItem = items.find(
            (item) => item.querySelector('strong').textContent.trim() === 'Readiness'
        );
        expect(readinessItem).not.toBeUndefined();
        const pill = readinessItem.querySelector('.status-pill');
        expect(pill.classList.contains('status-pill-attention')).toBe(true);
    });

    it('renders a "Portal" dock item with status-pill-ready (Stable)', () => {
        const items = Array.from(document.querySelectorAll('#activity-dock .dock-item'));
        const portalItem = items.find(
            (item) => item.querySelector('strong').textContent.trim() === 'Portal'
        );
        expect(portalItem).not.toBeUndefined();
        const pill = portalItem.querySelector('.status-pill');
        expect(pill.classList.contains('status-pill-ready')).toBe(true);
    });
});

// ---------------------------------------------------------------------------
// Runtime Control hero — overall structure
// Verifies the runtime-control:hero route renders the correct title, stage
// label, metrics, and key panels for the operational dashboard.
// ---------------------------------------------------------------------------
describe('Runtime Control hero — overall structure (runtime-control:hero)', () => {
    beforeAll(() => {
        initApp('#runtime-control/hero');
    });

    it('sets the route title to "Runtime Control"', () => {
        expect(document.getElementById('route-title').textContent.trim()).toBe('Runtime Control');
    });

    it('sets the route stage to "High-fidelity hero"', () => {
        expect(document.getElementById('route-stage').textContent.trim()).toBe('High-fidelity hero');
    });

    it('sets the route kicker to include "Runtime Control"', () => {
        expect(document.getElementById('route-kicker').textContent).toContain('Runtime Control');
    });

    it('renders four metric cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders a "Runtime state" metric with value "Ready"', () => {
        const cards = Array.from(document.querySelectorAll('.metric-card'));
        const card = cards.find(
            (c) => c.querySelector('.metric-label').textContent.trim() === 'Runtime state'
        );
        expect(card).not.toBeUndefined();
        expect(card.querySelector('.metric-value').textContent.trim()).toBe('Ready');
    });

    it('renders a "Developer launchers" metric', () => {
        const cards = Array.from(document.querySelectorAll('.metric-card'));
        const card = cards.find(
            (c) => c.querySelector('.metric-label').textContent.trim() === 'Developer launchers'
        );
        expect(card).not.toBeUndefined();
    });

    it('renders the lead panel with eyebrow "Operational center"', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        expect(panelHero.querySelector('.panel-kicker').textContent.trim()).toBe(
            'Operational center'
        );
    });

    it('renders "Run doctor" as the first lead panel action', () => {
        const panelHero = document.querySelector('.panel-hero');
        const actions = Array.from(panelHero.querySelectorAll('.action-pill')).map(
            (el) => el.textContent.trim()
        );
        expect(actions[0]).toBe('Run doctor');
    });

    it('renders "Launch Developer Portal" as the third lead panel action', () => {
        const panelHero = document.querySelector('.panel-hero');
        const actions = Array.from(panelHero.querySelectorAll('.action-pill')).map(
            (el) => el.textContent.trim()
        );
        expect(actions[2]).toBe('Launch Developer Portal');
    });

    it('renders the "Unified doctor" panel', () => {
        expect(getPanelByEyebrow('Unified doctor')).not.toBeNull();
    });

    it('renders the "Support actions" panel', () => {
        expect(getPanelByEyebrow('Support actions')).not.toBeNull();
    });

    it('renders the "Developer tools" panel', () => {
        expect(getPanelByEyebrow('Developer tools')).not.toBeNull();
    });

    it('renders four dock items', () => {
        const dockItems = document.querySelectorAll('#activity-dock .dock-item');
        expect(dockItems).toHaveLength(4);
    });
});

// ---------------------------------------------------------------------------
// Runtime Control hero — Unified doctor panel
// Verifies the Unified doctor row-list panel surfaces the four subsystems
// with their correct trust-state values.
// ---------------------------------------------------------------------------
describe('Runtime Control hero — Unified doctor panel (runtime-control:hero)', () => {
    beforeAll(() => {
        initApp('#runtime-control/hero');
    });

    it('"Unified doctor" panel uses a row-list layout', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        expect(panel.querySelector('.row-list')).not.toBeNull();
        expect(panel.querySelector('.key-list')).toBeNull();
    });

    it('"Unified doctor" panel renders exactly four subsystem rows', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        const dataRows = panel.querySelectorAll('.data-row');
        expect(dataRows).toHaveLength(4);
    });

    it('shows "Frontend" subsystem row with value "Ready"', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Frontend');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Ready');
    });

    it('shows "Gateway" subsystem row with value "Unavailable"', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Gateway');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Unavailable');
    });

    it('shows "Backend" subsystem row with value "Needs attention"', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Backend');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Needs attention');
    });
});

// ---------------------------------------------------------------------------
// Runtime Control hero — Developer tools launcher panel
// Verifies the Developer tools list panel correctly surfaces all four
// workshop launch targets, enabling workflow grouping from Runtime Control.
// ---------------------------------------------------------------------------
describe('Runtime Control hero — Developer tools launcher panel (runtime-control:hero)', () => {
    beforeAll(() => {
        initApp('#runtime-control/hero');
    });

    it('"Developer tools" panel uses a key-list layout', () => {
        const panel = getPanelByEyebrow('Developer tools');
        expect(panel.querySelector('.key-list')).not.toBeNull();
        expect(panel.querySelector('.row-list')).toBeNull();
    });

    it('"Developer tools" panel lists four items', () => {
        const panel = getPanelByEyebrow('Developer tools');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(4);
    });

    it('"Developer tools" panel includes "Developer Portal"', () => {
        const panel = getPanelByEyebrow('Developer tools');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Developer Portal');
    });

    it('"Developer tools" panel includes "Architecture graph"', () => {
        const panel = getPanelByEyebrow('Developer tools');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Architecture graph');
    });

    it('"Developer tools" panel includes "Command Center"', () => {
        const panel = getPanelByEyebrow('Developer tools');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Command Center');
    });

    it('"Developer tools" panel includes "Developer docs"', () => {
        const panel = getPanelByEyebrow('Developer tools');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Developer docs');
    });
});

// ---------------------------------------------------------------------------
// Runtime Control hero — inspector design rules
// Verifies the inspector enforces the "no developer-portal duplication" rule
// and the correct Keep/Reject vocabulary for the operational dashboard.
// ---------------------------------------------------------------------------
describe('Runtime Control hero — inspector design rules (runtime-control:hero)', () => {
    beforeAll(() => {
        initApp('#runtime-control/hero');
    });

    it('inspector title is "Runtime rules"', () => {
        const title = document.getElementById('inspector').querySelector('h3');
        expect(title.textContent.trim()).toBe('Runtime rules');
    });

    it('inspector subtitle is "Compact, trustworthy, and supportable."', () => {
        const subtitle = document.getElementById('inspector').querySelector('.route-summary');
        expect(subtitle.textContent.trim()).toBe('Compact, trustworthy, and supportable.');
    });

    it('inspector has a "Keep" section', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Keep'
        );
        expect(keepSection).not.toBeUndefined();
    });

    it('"Keep" section lists "Central runtime dashboard"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Keep'
        );
        const items = Array.from(keepSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Central runtime dashboard');
    });

    it('"Reject" section lists "Developer portal content duplicated here"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        const items = Array.from(rejectSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Developer portal content duplicated here');
    });

    it('"Reject" section lists "First-view startup noise"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        const items = Array.from(rejectSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('First-view startup noise');
    });
});

// ---------------------------------------------------------------------------
// Runtime Control hero — dock states
// Verifies the dock items for runtime-control:hero display the correct labels
// and status-pill classes that reflect the current operational posture.
// ---------------------------------------------------------------------------
describe('Runtime Control hero — dock states (runtime-control:hero)', () => {
    beforeAll(() => {
        initApp('#runtime-control/hero');
    });

    it('renders a "Doctor" dock item', () => {
        const labels = Array.from(
            document.querySelectorAll('#activity-dock .dock-item strong')
        ).map((el) => el.textContent.trim());
        expect(labels).toContain('Doctor');
    });

    it('renders a "Gateway" dock item', () => {
        const labels = Array.from(
            document.querySelectorAll('#activity-dock .dock-item strong')
        ).map((el) => el.textContent.trim());
        expect(labels).toContain('Gateway');
    });

    it('applies status-pill-ready to the "Doctor" dock item (Ready)', () => {
        const items = Array.from(document.querySelectorAll('#activity-dock .dock-item'));
        const doctorItem = items.find(
            (item) => item.querySelector('strong').textContent.trim() === 'Doctor'
        );
        expect(doctorItem).not.toBeUndefined();
        expect(doctorItem.querySelector('.status-pill').classList.contains('status-pill-ready')).toBe(
            true
        );
    });

    it('applies status-pill-risk to the "Gateway" dock item (Risk)', () => {
        const items = Array.from(document.querySelectorAll('#activity-dock .dock-item'));
        const gatewayItem = items.find(
            (item) => item.querySelector('strong').textContent.trim() === 'Gateway'
        );
        expect(gatewayItem).not.toBeUndefined();
        expect(
            gatewayItem.querySelector('.status-pill').classList.contains('status-pill-risk')
        ).toBe(true);
    });
});

// ---------------------------------------------------------------------------
// Runtime Control diagnostics — overall structure
// Verifies that the runtime-control:diagnostics route renders the correct
// title, stage, metrics, and key diagnostic panels.
// ---------------------------------------------------------------------------
describe('Runtime Control diagnostics — overall structure (runtime-control:diagnostics)', () => {
    beforeAll(() => {
        initApp('#runtime-control/diagnostics');
    });

    it('sets the route title to "Diagnostics Detail"', () => {
        expect(document.getElementById('route-title').textContent.trim()).toBe(
            'Diagnostics Detail'
        );
    });

    it('sets the route stage to "Structural detail"', () => {
        expect(document.getElementById('route-stage').textContent.trim()).toBe('Structural detail');
    });

    it('sets the route kicker to include "Runtime Control"', () => {
        expect(document.getElementById('route-kicker').textContent).toContain('Runtime Control');
    });

    it('renders four metric cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders a "Logs" metric with value "4 streams"', () => {
        const cards = Array.from(document.querySelectorAll('.metric-card'));
        const card = cards.find(
            (c) => c.querySelector('.metric-label').textContent.trim() === 'Logs'
        );
        expect(card).not.toBeUndefined();
        expect(card.querySelector('.metric-value').textContent.trim()).toBe('4 streams');
    });

    it('renders a "Bundle size" metric with value "18.4 MB"', () => {
        const cards = Array.from(document.querySelectorAll('.metric-card'));
        const card = cards.find(
            (c) => c.querySelector('.metric-label').textContent.trim() === 'Bundle size'
        );
        expect(card).not.toBeUndefined();
        expect(card.querySelector('.metric-value').textContent.trim()).toBe('18.4 MB');
    });

    it('renders the lead panel with eyebrow "Support detail"', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        expect(panelHero.querySelector('.panel-kicker').textContent.trim()).toBe('Support detail');
    });

    it('renders the "Log streams" panel', () => {
        expect(getPanelByEyebrow('Log streams')).not.toBeNull();
    });

    it('renders the "Bundle contents" panel', () => {
        expect(getPanelByEyebrow('Bundle contents')).not.toBeNull();
    });

    it('renders the "Restart safety" panel', () => {
        expect(getPanelByEyebrow('Restart safety')).not.toBeNull();
    });

    it('renders the "Evidence chain" panel', () => {
        expect(getPanelByEyebrow('Evidence chain')).not.toBeNull();
    });
});

// ---------------------------------------------------------------------------
// Runtime Control diagnostics — panel content and layouts
// Verifies that the diagnostic panels use the correct layouts and surface
// the expected evidence items for support workflows.
// ---------------------------------------------------------------------------
describe('Runtime Control diagnostics — panel content and layouts (runtime-control:diagnostics)', () => {
    beforeAll(() => {
        initApp('#runtime-control/diagnostics');
    });

    it('"Log streams" panel uses a key-list layout', () => {
        const panel = getPanelByEyebrow('Log streams');
        expect(panel.querySelector('.key-list')).not.toBeNull();
        expect(panel.querySelector('.row-list')).toBeNull();
    });

    it('"Log streams" panel lists three log entries', () => {
        const panel = getPanelByEyebrow('Log streams');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(3);
    });

    it('"Bundle contents" panel uses a row-list layout', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        expect(panel.querySelector('.row-list')).not.toBeNull();
        expect(panel.querySelector('.key-list')).toBeNull();
    });

    it('"Bundle contents" panel renders exactly three rows', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        const dataRows = panel.querySelectorAll('.data-row');
        expect(dataRows).toHaveLength(3);
    });

    it('"Bundle contents" shows "Doctor snapshot" row with value "Included"', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Doctor snapshot');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Included');
    });

    it('"Restart safety" panel lists three scoped actions', () => {
        const panel = getPanelByEyebrow('Restart safety');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(3);
    });

    it('"Evidence chain" panel lists three evidence items', () => {
        const panel = getPanelByEyebrow('Evidence chain');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(3);
    });
});

// ---------------------------------------------------------------------------
// Runtime Control diagnostics — inspector design rules
// Verifies the inspector for the diagnostics route surfaces the correct
// "Visible" section rules for support workflows.
// ---------------------------------------------------------------------------
describe('Runtime Control diagnostics — inspector design rules (runtime-control:diagnostics)', () => {
    beforeAll(() => {
        initApp('#runtime-control/diagnostics');
    });

    it('inspector title is "Support rules"', () => {
        const title = document.getElementById('inspector').querySelector('h3');
        expect(title.textContent.trim()).toBe('Support rules');
    });

    it('inspector has a "Visible" section', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const visibleSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Visible'
        );
        expect(visibleSection).not.toBeUndefined();
    });

    it('"Visible" section lists "Evidence and timestamps"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const visibleSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Visible'
        );
        const items = Array.from(visibleSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Evidence and timestamps');
    });

    it('"Visible" section lists "Bundle export path"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const visibleSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Visible'
        );
        const items = Array.from(visibleSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Bundle export path');
    });

    it('"Visible" section lists "Safe actions"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const visibleSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Visible'
        );
        const items = Array.from(visibleSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Safe actions');
    });
});

// ---------------------------------------------------------------------------
// Graduation path guide — customer-safe copy documentation consistency
// Verifies that graduation-path-guide.md exists, references all three
// workflow steps, and that its documented steps match what app.js renders.
// ---------------------------------------------------------------------------
describe('Graduation path guide — customer-safe copy documentation (graduation-path-guide.md)', () => {
    let guideSource;

    beforeAll(() => {
        guideSource = fs.readFileSync(
            path.join(__dirname, 'graduation-path-guide.md'),
            'utf8'
        );
    });

    it('graduation-path-guide.md exists and is non-empty', () => {
        expect(guideSource).toBeTruthy();
        expect(guideSource.length).toBeGreaterThan(0);
    });

    it('guide documents "Tighten customer-safe copy" as the first graduation step', () => {
        expect(guideSource).toContain('Tighten customer-safe copy');
    });

    it('guide documents "Prove workflow value with operators" as the second graduation step', () => {
        expect(guideSource).toContain('Prove workflow value with operators');
    });

    it('guide documents "Remove lab-only controls from the route" as the third graduation step', () => {
        expect(guideSource).toContain('Remove lab-only controls from the route');
    });

    it('guide explains that productization is staged, not implied', () => {
        expect(guideSource).toContain('Productization is staged, not implied');
    });

    it('guide defines customer-safe copy rules with a table', () => {
        expect(guideSource).toContain('Customer-safe copy rules');
    });

    it('guide references the canonical graduation panel subtitle from app.js', () => {
        // The subtitle used in the list() call in app.js is "Productization is staged, not implied."
        expect(guideSource).toContain('Productization is staged, not implied');
    });

    it('guide documents route hygiene three-point check', () => {
        expect(guideSource).toContain('One launch CTA');
        expect(guideSource).toContain('One readiness summary');
        expect(guideSource).toContain('One graduation story');
    });

    it('guide references reference-board.md for Developer Portal context', () => {
        expect(guideSource).toContain('reference-board.md');
    });

    it('guide covers all three graduation steps in fixed order', () => {
        const step1Index = guideSource.indexOf('Tighten customer-safe copy');
        const step2Index = guideSource.indexOf('Prove workflow value with operators');
        const step3Index = guideSource.indexOf('Remove lab-only controls from the route');
        expect(step1Index).toBeLessThan(step2Index);
        expect(step2Index).toBeLessThan(step3Index);
    });
});

// ---------------------------------------------------------------------------
// Graduation path storyboard — cross-check with documentation
// Verifies that the three graduation steps rendered in app.js match the
// three steps documented in graduation-path-guide.md exactly.
// ---------------------------------------------------------------------------
describe('Graduation path storyboard — cross-check with documentation (developer-portal:tool-detail)', () => {
    let guideSource;

    beforeAll(() => {
        initApp('#developer-portal/tool-detail');
        guideSource = fs.readFileSync(
            path.join(__dirname, 'graduation-path-guide.md'),
            'utf8'
        );
    });

    it('every step rendered in the "Graduation path" panel is documented in graduation-path-guide.md', () => {
        const panel = getPanelByEyebrow('Graduation path');
        const renderedSteps = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        for (const step of renderedSteps) {
            expect(guideSource).toContain(step);
        }
    });

    it('"Proof inputs" panel customer-safe review state is covered by the guide', () => {
        expect(guideSource).toContain('customer-safe review state');
    });
});
