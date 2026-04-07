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
// chunk12 — Runtime Control hero overall structure
// Verifies that the runtime-control:hero view renders its full set of panels,
// metrics, inspector rules, and dock items, conforming to the REFACTOR-PRESSURE
// design principle that Runtime Control owns "compact, trustworthy, and
// supportable" workstation operations — distinct from the Developer Portal.
// ---------------------------------------------------------------------------
describe('Runtime Control hero overall structure (runtime-control:hero)', () => {
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

    it('renders four metrics cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders the lead panel with eyebrow "Operational center"', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        expect(panelHero.querySelector('.panel-kicker').textContent.trim()).toBe('Operational center');
    });

    it('renders the companion panel with eyebrow "Reference move"', () => {
        const panels = Array.from(document.querySelectorAll('.panel'));
        const companion = panels.find((p) => {
            const kicker = p.querySelector('.panel-kicker');
            return kicker && kicker.textContent.trim() === 'Reference move';
        });
        expect(companion).not.toBeUndefined();
    });

    it('renders the Unified doctor panel', () => {
        expect(getPanelByEyebrow('Unified doctor')).not.toBeNull();
    });

    it('renders the Support actions panel', () => {
        expect(getPanelByEyebrow('Support actions')).not.toBeNull();
    });

    it('renders the Developer tools panel', () => {
        expect(getPanelByEyebrow('Developer tools')).not.toBeNull();
    });

    it('renders the Watchdog panel', () => {
        expect(getPanelByEyebrow('Watchdog')).not.toBeNull();
    });

    it('renders the Recent events panel', () => {
        expect(getPanelByEyebrow('Recent events')).not.toBeNull();
    });

    it('renders four dock items', () => {
        const dockItems = document.querySelectorAll('#activity-dock .dock-item');
        expect(dockItems).toHaveLength(4);
    });
});

// ---------------------------------------------------------------------------
// chunk12 — Runtime Control hero: Unified doctor panel compliance
// Verifies the Unified doctor panel correctly uses a row-list layout and
// exposes all four subsystems using a single trust vocabulary, as required by
// the REFACTOR-PRESSURE.md "one report, four subsystems, one trust vocabulary"
// design principle.
// ---------------------------------------------------------------------------
describe('Runtime Control hero: Unified doctor panel (runtime-control:hero)', () => {
    beforeAll(() => {
        initApp('#runtime-control/hero');
    });

    it('renders the Unified doctor panel', () => {
        expect(getPanelByEyebrow('Unified doctor')).not.toBeNull();
    });

    it('shows the panel title "One report, four subsystems, one trust vocabulary"', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        const title = panel.querySelector('.panel-title');
        expect(title.textContent.trim()).toBe('One report, four subsystems, one trust vocabulary');
    });

    it('shows the copy describing unified reporting intent', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        const copy = panel.querySelector('.panel-copy');
        expect(copy.textContent.trim()).toContain('without parallel status universes');
    });

    it('uses a row-list layout (not a key-list)', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        expect(panel.querySelector('.row-list')).not.toBeNull();
        expect(panel.querySelector('.key-list')).toBeNull();
    });

    it('renders exactly four subsystem rows', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        const dataRows = panel.querySelectorAll('.data-row');
        expect(dataRows).toHaveLength(4);
    });

    it('shows Frontend subsystem as "Ready"', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Frontend');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Ready');
    });

    it('shows Gateway subsystem as "Unavailable"', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Gateway');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Unavailable');
    });

    it('includes all four subsystem labels', () => {
        const panel = getPanelByEyebrow('Unified doctor');
        const labels = Array.from(panel.querySelectorAll('.row-label')).map(
            (el) => el.textContent.trim()
        );
        expect(labels).toContain('Frontend');
        expect(labels).toContain('Backend');
        expect(labels).toContain('Gateway');
        expect(labels).toContain('Watchdog');
    });
});

// ---------------------------------------------------------------------------
// chunk12 — Runtime Control hero: inspector design rules compliance
// Verifies the inspector for runtime-control:hero surfaces the correct Keep
// and Reject rules. The "Keep" list must include centralised runtime dashboard
// and integrated logs; the "Reject" list must exclude multiple health
// vocabularies and Developer Portal content duplication.
// ---------------------------------------------------------------------------
describe('Runtime Control hero: inspector rules (runtime-control:hero)', () => {
    beforeAll(() => {
        initApp('#runtime-control/hero');
    });

    it('shows the inspector title "Runtime rules"', () => {
        const title = document.getElementById('inspector').querySelector('h3');
        expect(title.textContent.trim()).toBe('Runtime rules');
    });

    it('shows the inspector subtitle "Compact, trustworthy, and supportable."', () => {
        const subtitle = document.getElementById('inspector').querySelector('.route-summary');
        expect(subtitle.textContent.trim()).toBe('Compact, trustworthy, and supportable.');
    });

    it('includes a "Keep" section', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        expect(keepSection).not.toBeUndefined();
    });

    it('includes a "Reject" section', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        expect(rejectSection).not.toBeUndefined();
    });

    it('"Keep" section lists "Central runtime dashboard"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        const items = Array.from(keepSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Central runtime dashboard');
    });

    it('"Keep" section lists "Integrated logs and terminal access"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        const items = Array.from(keepSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Integrated logs and terminal access');
    });

    it('"Reject" section lists "Multiple health vocabularies"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        const items = Array.from(rejectSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Multiple health vocabularies');
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
});

// ---------------------------------------------------------------------------
// chunk12 — Runtime Control diagnostics overall structure
// Verifies that the runtime-control:diagnostics view renders its full set of
// panels, metrics, and dock items, and that its inspector enforces the
// "diagnostics should help resolve issues, not add a new workflow" principle.
// ---------------------------------------------------------------------------
describe('Runtime Control diagnostics overall structure (runtime-control:diagnostics)', () => {
    beforeAll(() => {
        initApp('#runtime-control/diagnostics');
    });

    it('sets the route title to "Diagnostics Detail"', () => {
        expect(document.getElementById('route-title').textContent.trim()).toBe('Diagnostics Detail');
    });

    it('sets the route stage to "Structural detail"', () => {
        expect(document.getElementById('route-stage').textContent.trim()).toBe('Structural detail');
    });

    it('renders four metrics cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders the lead panel with eyebrow "Support detail"', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        expect(panelHero.querySelector('.panel-kicker').textContent.trim()).toBe('Support detail');
    });

    it('renders the Log streams panel', () => {
        expect(getPanelByEyebrow('Log streams')).not.toBeNull();
    });

    it('renders the Bundle contents panel', () => {
        expect(getPanelByEyebrow('Bundle contents')).not.toBeNull();
    });

    it('renders the Restart safety panel', () => {
        expect(getPanelByEyebrow('Restart safety')).not.toBeNull();
    });

    it('renders the Evidence chain panel', () => {
        expect(getPanelByEyebrow('Evidence chain')).not.toBeNull();
    });

    it('renders the Support note panel', () => {
        expect(getPanelByEyebrow('Support note')).not.toBeNull();
    });

    it('renders four dock items', () => {
        const dockItems = document.querySelectorAll('#activity-dock .dock-item');
        expect(dockItems).toHaveLength(4);
    });
});

// ---------------------------------------------------------------------------
// chunk12 — Runtime Control diagnostics: Bundle contents panel compliance
// Verifies the Bundle contents panel uses a row-list layout with all three
// required bundle components (Doctor snapshot, Runtime logs, Config masks).
// ---------------------------------------------------------------------------
describe('Runtime Control diagnostics: Bundle contents panel (runtime-control:diagnostics)', () => {
    beforeAll(() => {
        initApp('#runtime-control/diagnostics');
    });

    it('shows the panel title "Support export should be predictable"', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        const title = panel.querySelector('.panel-title');
        expect(title.textContent.trim()).toBe('Support export should be predictable');
    });

    it('shows the copy describing bundle legibility requirement', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        const copy = panel.querySelector('.panel-copy');
        expect(copy.textContent.trim()).toBe('Bundle content must be legible before export.');
    });

    it('uses a row-list layout (not a key-list)', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        expect(panel.querySelector('.row-list')).not.toBeNull();
        expect(panel.querySelector('.key-list')).toBeNull();
    });

    it('renders exactly three bundle component rows', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        const dataRows = panel.querySelectorAll('.data-row');
        expect(dataRows).toHaveLength(3);
    });

    it('includes "Doctor snapshot" as a bundle component', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        const labels = Array.from(panel.querySelectorAll('.row-label')).map(
            (el) => el.textContent.trim()
        );
        expect(labels).toContain('Doctor snapshot');
    });

    it('includes "Runtime logs" as a bundle component', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        const labels = Array.from(panel.querySelectorAll('.row-label')).map(
            (el) => el.textContent.trim()
        );
        expect(labels).toContain('Runtime logs');
    });

    it('includes "Config masks" as a bundle component', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        const labels = Array.from(panel.querySelectorAll('.row-label')).map(
            (el) => el.textContent.trim()
        );
        expect(labels).toContain('Config masks');
    });

    it('shows Config masks value as "Included"', () => {
        const panel = getPanelByEyebrow('Bundle contents');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Config masks');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Included');
    });
});

// ---------------------------------------------------------------------------
// chunk12 — Runtime Control diagnostics: inspector design rules compliance
// Verifies the inspector for runtime-control:diagnostics surfaces the correct
// Visible section items, ensuring diagnostics keeps the surface investigative
// and not theatrical.
// ---------------------------------------------------------------------------
describe('Runtime Control diagnostics: inspector rules (runtime-control:diagnostics)', () => {
    beforeAll(() => {
        initApp('#runtime-control/diagnostics');
    });

    it('shows the inspector title "Support rules"', () => {
        const title = document.getElementById('inspector').querySelector('h3');
        expect(title.textContent.trim()).toBe('Support rules');
    });

    it('shows the inspector subtitle about resolving issues', () => {
        const subtitle = document.getElementById('inspector').querySelector('.route-summary');
        expect(subtitle.textContent.trim()).toBe(
            'Diagnostics should help resolve issues, not add a new workflow.'
        );
    });

    it('includes a "Visible" section', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const visibleSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Visible'
        );
        expect(visibleSection).not.toBeUndefined();
    });

    it('"Visible" section lists "Current issue summary"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const visibleSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Visible'
        );
        const items = Array.from(visibleSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Current issue summary');
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
});

// ---------------------------------------------------------------------------
// chunk12 — Developer Portal tool-detail overall structure
// Verifies that the developer-portal:tool-detail view renders its full set of
// panels and enforces the staging / graduation design principle: future product
// tools need explicit staging, not accidental product exposure.
// ---------------------------------------------------------------------------
describe('Developer Portal tool-detail overall structure (developer-portal:tool-detail)', () => {
    beforeAll(() => {
        initApp('#developer-portal/tool-detail');
    });

    it('sets the route title to "Tool Detail"', () => {
        expect(document.getElementById('route-title').textContent.trim()).toBe('Tool Detail');
    });

    it('sets the route stage to "Structural detail"', () => {
        expect(document.getElementById('route-stage').textContent.trim()).toBe('Structural detail');
    });

    it('renders four metrics cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders the lead panel with eyebrow "Single-tool readiness"', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        expect(panelHero.querySelector('.panel-kicker').textContent.trim()).toBe(
            'Single-tool readiness'
        );
    });

    it('renders the Launch readiness panel', () => {
        expect(getPanelByEyebrow('Launch readiness')).not.toBeNull();
    });

    it('renders the Graduation path panel', () => {
        expect(getPanelByEyebrow('Graduation path')).not.toBeNull();
    });

    it('renders the Future product fit panel', () => {
        expect(getPanelByEyebrow('Future product fit')).not.toBeNull();
    });

    it('renders the Proof inputs panel', () => {
        expect(getPanelByEyebrow('Proof inputs')).not.toBeNull();
    });

    it('renders the Route hygiene panel', () => {
        expect(getPanelByEyebrow('Route hygiene')).not.toBeNull();
    });

    it('renders four dock items', () => {
        const dockItems = document.querySelectorAll('#activity-dock .dock-item');
        expect(dockItems).toHaveLength(4);
    });
});

// ---------------------------------------------------------------------------
// chunk12 — Developer Portal tool-detail: Launch readiness and Graduation path
// Verifies that the launch readiness panel uses a row-list layout to expose
// runtime dependency states, and that the graduation path panel uses a
// key-list layout to enumerate the three steps toward customer-safe release.
// ---------------------------------------------------------------------------
describe('Developer Portal tool-detail: Launch readiness and Graduation path (developer-portal:tool-detail)', () => {
    beforeAll(() => {
        initApp('#developer-portal/tool-detail');
    });

    it('Launch readiness panel uses a row-list layout', () => {
        const panel = getPanelByEyebrow('Launch readiness');
        expect(panel.querySelector('.row-list')).not.toBeNull();
        expect(panel.querySelector('.key-list')).toBeNull();
    });

    it('Launch readiness panel renders exactly three dependency rows', () => {
        const panel = getPanelByEyebrow('Launch readiness');
        const dataRows = panel.querySelectorAll('.data-row');
        expect(dataRows).toHaveLength(3);
    });

    it('Launch readiness panel shows "Frontend route" as "Available"', () => {
        const panel = getPanelByEyebrow('Launch readiness');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Frontend route');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Available');
    });

    it('Launch readiness panel shows "Gateway bridge" requiring attention', () => {
        const panel = getPanelByEyebrow('Launch readiness');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('Gateway bridge');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Needs attention');
    });

    it('Graduation path panel uses a key-list layout', () => {
        const panel = getPanelByEyebrow('Graduation path');
        expect(panel.querySelector('.key-list')).not.toBeNull();
        expect(panel.querySelector('.row-list')).toBeNull();
    });

    it('Graduation path panel lists three graduation steps', () => {
        const panel = getPanelByEyebrow('Graduation path');
        const items = panel.querySelectorAll('.key-row strong');
        expect(items).toHaveLength(3);
    });

    it('Graduation path panel includes the customer-safe copy step', () => {
        const panel = getPanelByEyebrow('Graduation path');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Tighten customer-safe copy');
    });

    it('Graduation path panel includes the lab-only controls removal step', () => {
        const panel = getPanelByEyebrow('Graduation path');
        const texts = Array.from(panel.querySelectorAll('.key-row strong')).map(
            (el) => el.textContent.trim()
        );
        expect(texts).toContain('Remove lab-only controls from the route');
    });
});

// ---------------------------------------------------------------------------
// chunk12 — Customer App hero overall structure
// Verifies that the customer-app:hero view renders its full set of panels and
// metrics, and that the inspector enforces the "Premium, calm, and
// product-safe" design principle — no architecture pressure, no agent memory
// surfaces, no repo hotspots in the customer viewport.
// ---------------------------------------------------------------------------
describe('Customer App hero overall structure (customer-app:hero)', () => {
    beforeAll(() => {
        initApp('#customer-app/hero');
    });

    it('sets the route title to "Customer App"', () => {
        expect(document.getElementById('route-title').textContent.trim()).toBe('Customer App');
    });

    it('sets the route stage to "High-fidelity hero"', () => {
        expect(document.getElementById('route-stage').textContent.trim()).toBe('High-fidelity hero');
    });

    it('sets the route kicker to include "Customer App"', () => {
        expect(document.getElementById('route-kicker').textContent).toContain('Customer App');
    });

    it('renders four metrics cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders the lead panel with eyebrow "Mission board"', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        expect(panelHero.querySelector('.panel-kicker').textContent.trim()).toBe('Mission board');
    });

    it('renders the Project readiness panel', () => {
        expect(getPanelByEyebrow('Project readiness')).not.toBeNull();
    });

    it('renders the Review pressure panel', () => {
        expect(getPanelByEyebrow('Review pressure')).not.toBeNull();
    });

    it('renders the Issue sets panel', () => {
        expect(getPanelByEyebrow('Issue sets')).not.toBeNull();
    });

    it('renders the Transmittal queue panel', () => {
        expect(getPanelByEyebrow('Transmittal queue')).not.toBeNull();
    });

    it('renders the Deadlines panel', () => {
        expect(getPanelByEyebrow('Deadlines')).not.toBeNull();
    });

    it('renders four dock items', () => {
        const dockItems = document.querySelectorAll('#activity-dock .dock-item');
        expect(dockItems).toHaveLength(4);
    });
});

// ---------------------------------------------------------------------------
// chunk12 — Customer App hero: Project readiness panel compliance
// Verifies the Project readiness panel uses a row-list layout and lists the
// three projects with their delivery states. This panel anchors the
// customer-facing "mission board" framing.
// ---------------------------------------------------------------------------
describe('Customer App hero: Project readiness panel (customer-app:hero)', () => {
    beforeAll(() => {
        initApp('#customer-app/hero');
    });

    it('shows the panel title "Projects should read like deliverable state, not system state"', () => {
        const panel = getPanelByEyebrow('Project readiness');
        const title = panel.querySelector('.panel-title');
        expect(title.textContent.trim()).toBe(
            'Projects should read like deliverable state, not system state'
        );
    });

    it('shows the copy describing the readiness lens', () => {
        const panel = getPanelByEyebrow('Project readiness');
        const copy = panel.querySelector('.panel-copy');
        expect(copy.textContent.trim()).toBe('Readiness is the main customer lens.');
    });

    it('uses a row-list layout (not a key-list)', () => {
        const panel = getPanelByEyebrow('Project readiness');
        expect(panel.querySelector('.row-list')).not.toBeNull();
        expect(panel.querySelector('.key-list')).toBeNull();
    });

    it('renders exactly three project rows', () => {
        const panel = getPanelByEyebrow('Project readiness');
        const dataRows = panel.querySelectorAll('.data-row');
        expect(dataRows).toHaveLength(3);
    });

    it('includes "North Substation" as a project', () => {
        const panel = getPanelByEyebrow('Project readiness');
        const labels = Array.from(panel.querySelectorAll('.row-label')).map(
            (el) => el.textContent.trim()
        );
        expect(labels).toContain('North Substation');
    });

    it('shows North Substation as "Ready for review"', () => {
        const panel = getPanelByEyebrow('Project readiness');
        const dataRows = Array.from(panel.querySelectorAll('.data-row'));
        const labels = dataRows.map((r) => r.querySelector('.row-label').textContent.trim());
        const values = dataRows.map((r) => r.querySelector('.row-value').textContent.trim());
        const idx = labels.indexOf('North Substation');
        expect(idx).toBeGreaterThanOrEqual(0);
        expect(values[idx]).toBe('Ready for review');
    });
});

// ---------------------------------------------------------------------------
// chunk12 — Customer App hero: inspector design rules compliance
// Verifies the inspector for customer-app:hero enforces the "Premium, calm,
// product-safe" design principle. The Reject section must exclude architecture
// pressure, agent memory surfaces, repo hotspots, and diagnostics.
// ---------------------------------------------------------------------------
describe('Customer App hero: inspector rules (customer-app:hero)', () => {
    beforeAll(() => {
        initApp('#customer-app/hero');
    });

    it('shows the inspector title "Customer rules"', () => {
        const title = document.getElementById('inspector').querySelector('h3');
        expect(title.textContent.trim()).toBe('Customer rules');
    });

    it('shows the inspector subtitle "Premium, calm, and product-safe."', () => {
        const subtitle = document.getElementById('inspector').querySelector('.route-summary');
        expect(subtitle.textContent.trim()).toBe('Premium, calm, and product-safe.');
    });

    it('includes a "Keep" section', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        expect(keepSection).not.toBeUndefined();
    });

    it('includes a "Reject" section', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        expect(rejectSection).not.toBeUndefined();
    });

    it('"Keep" section lists "Project, issue, and deadline structure"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        const items = Array.from(keepSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Project, issue, and deadline structure');
    });

    it('"Keep" section lists "Mission-board framing"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        const items = Array.from(keepSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Mission-board framing');
    });

    it('"Reject" section lists "Architecture pressure"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        const items = Array.from(rejectSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Architecture pressure');
    });

    it('"Reject" section lists "Agent memory surfaces"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        const items = Array.from(rejectSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Agent memory surfaces');
    });

    it('"Reject" section lists "Diagnostics in the customer viewport"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        const items = Array.from(rejectSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Diagnostics in the customer viewport');
    });
});

// ---------------------------------------------------------------------------
// chunk12 — Customer App project-detail overall structure
// Verifies that the customer-app:project-detail view renders its full set of
// panels and enforces delivery-facing depth: only milestones, issue groups,
// reference docs, and package prep belong here — no developer internals.
// ---------------------------------------------------------------------------
describe('Customer App project-detail overall structure (customer-app:project-detail)', () => {
    beforeAll(() => {
        initApp('#customer-app/project-detail');
    });

    it('sets the route title to "Project Detail"', () => {
        expect(document.getElementById('route-title').textContent.trim()).toBe('Project Detail');
    });

    it('sets the route stage to "Structural detail"', () => {
        expect(document.getElementById('route-stage').textContent.trim()).toBe('Structural detail');
    });

    it('renders four metrics cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders the lead panel with eyebrow "Project depth"', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        expect(panelHero.querySelector('.panel-kicker').textContent.trim()).toBe('Project depth');
    });

    it('renders the Milestones panel', () => {
        expect(getPanelByEyebrow('Milestones')).not.toBeNull();
    });

    it('renders the Reference library panel', () => {
        expect(getPanelByEyebrow('Reference library')).not.toBeNull();
    });

    it('renders the Issue lane panel', () => {
        expect(getPanelByEyebrow('Issue lane')).not.toBeNull();
    });

    it('renders the Package prep panel', () => {
        expect(getPanelByEyebrow('Package prep')).not.toBeNull();
    });

    it('renders the Customer trust panel', () => {
        expect(getPanelByEyebrow('Customer trust')).not.toBeNull();
    });

    it('renders four dock items', () => {
        const dockItems = document.querySelectorAll('#activity-dock .dock-item');
        expect(dockItems).toHaveLength(4);
    });

    it('shows the inspector title "Project detail rules"', () => {
        const title = document.getElementById('inspector').querySelector('h3');
        expect(title.textContent.trim()).toBe('Project detail rules');
    });

    it('shows the inspector subtitle about delivery-facing depth', () => {
        const subtitle = document.getElementById('inspector').querySelector('.route-summary');
        expect(subtitle.textContent.trim()).toBe('Only delivery-facing depth belongs here.');
    });

    it('"Visible" section lists "Milestones"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const visibleSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Visible'
        );
        expect(visibleSection).not.toBeUndefined();
        const items = Array.from(visibleSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Milestones');
    });

    it('"Visible" section lists "Package prep"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const visibleSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Visible'
        );
        const items = Array.from(visibleSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Package prep');
    });
});

// ---------------------------------------------------------------------------
// chunk12 — Daily Desk hero overall structure
// Verifies that the daily-desk:hero view renders its full set of panels,
// metrics, and dock items, and that the inspector enforces the "command-first
// operator console" design principle.
// ---------------------------------------------------------------------------
describe('Daily Desk hero overall structure (daily-desk:hero)', () => {
    beforeAll(() => {
        initApp('#daily-desk/hero');
    });

    it('sets the route title to "Daily Desk"', () => {
        expect(document.getElementById('route-title').textContent.trim()).toBe('Daily Desk');
    });

    it('sets the route stage to "High-fidelity hero"', () => {
        expect(document.getElementById('route-stage').textContent.trim()).toBe('High-fidelity hero');
    });

    it('sets the route kicker to include "Daily Desk"', () => {
        expect(document.getElementById('route-kicker').textContent).toContain('Daily Desk');
    });

    it('renders four metrics cards', () => {
        const metrics = document.querySelectorAll('.metric-card');
        expect(metrics).toHaveLength(4);
    });

    it('renders the lead panel with eyebrow "Command strip"', () => {
        const panelHero = document.querySelector('.panel-hero');
        expect(panelHero).not.toBeNull();
        expect(panelHero.querySelector('.panel-kicker').textContent.trim()).toBe('Command strip');
    });

    it('renders the Today stack panel', () => {
        expect(getPanelByEyebrow('Today stack')).not.toBeNull();
    });

    it('renders the Inbox glance panel', () => {
        expect(getPanelByEyebrow('Inbox glance')).not.toBeNull();
    });

    it('renders the Training panel', () => {
        expect(getPanelByEyebrow('Training')).not.toBeNull();
    });

    it('renders the Research panel', () => {
        expect(getPanelByEyebrow('Research')).not.toBeNull();
    });

    it('renders the Repo coach panel', () => {
        expect(getPanelByEyebrow('Repo coach')).not.toBeNull();
    });

    it('renders four dock items', () => {
        const dockItems = document.querySelectorAll('#activity-dock .dock-item');
        expect(dockItems).toHaveLength(4);
    });

    it('shows the inspector title "Borrow rules"', () => {
        const title = document.getElementById('inspector').querySelector('h3');
        expect(title.textContent.trim()).toBe('Borrow rules');
    });

    it('inspector "Keep" section lists "Global command-first entry"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const keepSection = sections.find((s) => s.querySelector('h4').textContent.trim() === 'Keep');
        const items = Array.from(keepSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Global command-first entry');
    });

    it('inspector "Reject" section lists "Card mosaics as the main layout"', () => {
        const sections = Array.from(
            document.getElementById('inspector').querySelectorAll('.inspector-section')
        );
        const rejectSection = sections.find(
            (s) => s.querySelector('h4').textContent.trim() === 'Reject'
        );
        const items = Array.from(rejectSection.querySelectorAll('li')).map((li) =>
            li.textContent.trim()
        );
        expect(items).toContain('Card mosaics as the main layout');
    });
});
