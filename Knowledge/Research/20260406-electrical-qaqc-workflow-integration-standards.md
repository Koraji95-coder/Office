# Electrical QA/QC Workflow Integration Standards

- Generated: 2026-04-06 10:17
- Perspective: EE Mentor
- Model: copilot
- Source: synthesis from checklist research + Watercare QA/QC PDF template

## Summary

This document explains how the QA/QC templates for General Electrical Construction Standards (sourced from the Watercare PDF template at `wslpwstoreprd.blob.core.windows.net`) integrate with the electrical drawing review and approval workflow documented in [`20260324-000659-electrical-drawing-qa-workflow-standards-review-checklist.md`](./20260324-000659-electrical-drawing-qa-workflow-standards-review-checklist.md). It covers mandatory test categories, the review-and-sign-off hierarchy, and how each checklist phase maps to a construction project milestone.

## Background: The QA/QC Template Gap

The checklist research file references four separate sources for QA/QC standards:

| Source | Scope |
|---|---|
| RAIC Appendix I | Internal drawing review from early design through construction |
| Scribd Engineering Drawings Review Checklist | General drawing compliance (30+ check items) |
| oneprojectarchitect.com ELECTRICAL (ELEC) Schematic Design Checklist | Code references, floor plan alignment, general notes |
| **Watercare QA/QC Templates for General Electrical Construction Standards (PDF)** | **Mandatory field tests for constructed electrical systems** |

The Watercare PDF (section 1.13) is the only source that addresses *post-installation* quality assurance — meaning it applies **after** drawings have been approved and construction is underway. Without documenting how it connects to the upstream drawing review process, teams can miss the hand-off point between design QA and construction QA.

## Workflow Integration Model

QA/QC for electrical construction projects should be treated as a **continuous chain**, not a single gate. The table below maps each source to its project phase:

| Phase | Activity | Applicable Checklist/Standard |
|---|---|---|
| 1 – Schematic Design | Verify code references, applicable standards, floor plan alignment | ELECTRICAL (ELEC) Schematic Design Checklist (oneprojectarchitect.com) |
| 2 – Design Development / IFC Drawings | Internal review for completeness, detail, and coordination | RAIC Appendix I – Internal Review of Drawings: Electrical |
| 3 – Issued-for-Construction (IFC) | Final drawing release gate; 30+ check items across categories | Engineering Drawings Review Checklist (Scribd) |
| 4 – Construction & Installation | Field QA/QC testing against constructed systems | **Watercare QA/QC Templates for Electrical Construction Standards** |
| 5 – Commissioning & Handover | Witness testing, sign-off sheets, as-built mark-ups | Watercare QA/QC Templates + project-specific commissioning plan |

## Mandatory Test Categories (Watercare Template – Section 1.13)

The Watercare QA/QC template defines minimum mandatory tests for the following electrical construction categories:

1. **General Electrical Installation** – earthing continuity, insulation resistance, polarity, and functional tests
2. **Cables and Conduit** – installation inspection, cable pulling records, megger test results
3. **Switchboards, Distribution Centres, and Control Centres** – termination checks, protection relay settings, interlocking verification, FAT/SAT records
4. **Motors and Drives** – rotation checks, no-load and full-load current measurements, thermal overload settings
5. **Lighting and Small Power** – circuit continuity, RCD trip-time testing, lux level verification
6. **Instrumentation and Control Wiring** – loop checks, signal calibration records, PLC I/O verification
7. **Earthing and Bonding Systems** – earth resistance measurements, bonding continuity records

Each category requires a signed QA/QC record sheet that is retained as a project deliverable.

## Review and Approval Hierarchy

To enforce the standards above, the following sign-off hierarchy should be applied:

```
Designer / Drafter
    └─▶ Check Engineer (peer review against RAIC Appendix I)
            └─▶ EE of Record (approval stamp on IFC drawings)
                    └─▶ Site Electrical Supervisor (Watercare template – field tests)
                            └─▶ Independent Commissioning Engineer (witness tests)
                                    └─▶ Client / Asset Owner (final handover acceptance)
```

- **Drawing QA gate** (Phases 1–3): Controlled by the EE of Record and the RAIC / Engineering Drawings Review Checklist.
- **Construction QA gate** (Phases 4–5): Controlled by the Site Electrical Supervisor and the Watercare QA/QC template.

## Integration with Daily Desk Workflows

When using Daily Desk to manage electrical design tasks:

1. **Index the PDF template** – import the Watercare QA/QC PDF into the Knowledge folder so Daily Desk can surface mandatory test requirements during construction planning queries.
2. **Tag drawing packages by phase** – annotate project notes with the relevant phase (Schematic Design, IFC, etc.) so the desk can recommend the correct checklist.
3. **Automate review reminders** – use Job Schedules (Phase 8 feature) to trigger a daily-run workflow that reminds the team of pending QA sign-offs at each phase gate.
4. **Store test records** – keep completed QA/QC sign-off sheets (scanned PDFs or `.md` summaries) in `Knowledge/` so they become part of the project coaching profile.

## Action Moves

- Download and store the Watercare QA/QC PDF template locally in `Knowledge/` for offline access and indexing by Daily Desk.
- Create a project-specific QA/QC sign-off log (`.md` or `.pdf`) for each active electrical construction project, referencing section 1.13 category headings.
- Apply the RAIC Appendix I checklist at the Design Development phase before issuing drawings for construction.
- Schedule a weekly Phase 4/5 QA status review using Daily Desk's Job Scheduler to track open punch items.
- Cross-reference the floor plan alignment check (oneprojectarchitect.com ELEC checklist) with as-built mark-ups at Phase 5 handover.

## Sources

### PDF QA/QC Templates for General Electrical Construction Standards

- Domain: wslpwstoreprd.blob.core.windows.net
- URL: https://wslpwstoreprd.blob.core.windows.net/kentico-media-libraries-prod/watercarepublicweb/media/watercare-media-library/electrical-standards/qa_templates_for_electrical_construction_standards.pdf
- Search Snippet: 1.13 QA/QC template Minimum mandatory tests: ... 3. Switchboards, distribution centres and control centres

### Checklist – Internal Review of Drawings: Electrical – RAIC

- Domain: chop.raic.ca
- URL: https://chop.raic.ca/appendix-i-checklist-internal-review-of-drawings-electrical
- Search Snippet: The primary purpose of this checklist is quality assurance. Checking should commence early in the preparation of construction drawings and continue throughout their development.

### PDF Checklist ELECTRICAL (ELEC) Schematic Design

- Domain: oneprojectarchitect.com
- URL: https://oneprojectarchitect.com/wp-content/uploads/2025/05/ELECTRICAL-ELEC-CHECKLIST.pdf
- Search Snippet: Code & General Notes – Review code references and confirm the applicable electrical codes and standards are listed.

### Engineering Drawings Review Checklist

- Domain: www.scribd.com
- URL: https://www.scribd.com/document/343077002/Engineering-Drawings-Review-Checklist-example
- Search Snippet: This document is a drawing review checklist used to check drawings for various engineering, general, and part drawing requirements. It contains over 30 check items across different categories.

### Related Research (this repository)

- File: [`Knowledge/Research/20260324-000659-electrical-drawing-qa-workflow-standards-review-checklist.md`](./20260324-000659-electrical-drawing-qa-workflow-standards-review-checklist.md)
- File: [`Knowledge/Research/20260324-000306-electrical-design-automation-review-first-workflow-operator-approval.md`](./20260324-000306-electrical-design-automation-review-first-workflow-operator-approval.md)
- File: [`Knowledge/Research/20260324-000621-electrical-drafting-production-control-workflow-software-review-approvals-market.md`](./20260324-000621-electrical-drafting-production-control-workflow-software-review-approvals-market.md)
