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
