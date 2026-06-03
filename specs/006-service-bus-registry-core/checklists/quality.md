# Requirements Quality Checklist: Service Bus Registry Core

**Purpose**: Author-side audit of `spec.md` for completeness, clarity, consistency, and measurability before `/speckit-plan`. Each item is a unit test for the requirements writing, not for the implementation.
**Created**: 2026-06-01
**Walked**: 2026-06-02 — all items resolved by spec amendments, plan-phase decisions, or explicit deferrals to future specs.
**Feature**: [spec.md](../spec.md)
**Audience**: Spec author, pre-plan
**Depth**: Standard

## Requirement Completeness

- [X] CHK001 — Numeric reliability/availability target. **Resolution**: Explicit "no formal SLO" in v1, captured in `spec.md` §Assumptions (best-effort; underlying Azure SLAs apply; formal SLO reserved for future ops-hardening spec). Added to §Non-Goals.
- [X] CHK002 — Audit-event retention window. **Resolution**: `spec.md` FR-032 + §Non-Goals state indefinite retention in v1; archival/TTL reserved for future ops-hardening spec. `data-model.md` §5 confirms no TTL on audit documents in this slice.
- [X] CHK003 — Upper bounds for entity name / description / tag key / tag value / max tags. **Resolution**: Promoted into `spec.md` FR-002 ("Bounds (enforced by validation)" sub-clause); per-type name lengths in `data-model.md` §3.2 + `contracts/registry-entity.schema.json`.
- [X] CHK004 — Pagination defaults. **Resolution**: Promoted into `spec.md` FR-024 ("Defaults: page size 25, max 100; 400 on exceed"). Audit-list defaults (50 default / 200 max) in `contracts/registry-api.yaml`. Browse continuation-token pagination in `research.md` §13.
- [X] CHK005 — `fullyQualifiedName` composition rule per entity type. **Resolution**: Promoted into `spec.md` FR-002 (explicit per-type composition table).
- [X] CHK006 — Entity-type-specific fields delegated to `metadata`. **Resolution**: Already covered by FR-002 + §Key Entities ("queue-specific metadata captured under the extensible metadata field"). No spec change needed.
- [X] CHK007 — Mechanism for managing the configurable environment list. **Resolution**: Promoted into `spec.md` FR-035 ("implicitly defined by entity writes; admin-managed registry reserved for future governance spec"). Added to §Non-Goals.
- [X] CHK008 — Search relevance/ranking expectations beyond "ranked". **Resolution**: Plan-phase decision in `research.md` §6.2 (default BM25-like, no custom scoring profile in v1). Acceptable to leave at plan-phase; future spec may tune.

## Requirement Clarity

- [X] CHK009 — "Expected load" quantified. **Resolution**: `spec.md` §Assumptions states "a few hundred concurrent operators and registry sizes in the tens of thousands of entities per environment" — already there.
- [X] CHK010 — "Human-readable change summary" shape. **Resolution**: Promoted into `spec.md` FR-032 (single-sentence change summary + structured `fieldChanges` array on Updated/StatusChanged). Schema in `contracts/audit-event.schema.json`.
- [X] CHK011 — "Stable ordering" explicit sort key. **Resolution**: Promoted into `spec.md` FR-024 (stable-sort key `(relevance_score DESC, updatedAtUtc DESC, id ASC)` with explicit sort-override fallback).
- [X] CHK012 — "Explicit confirmation" UX prescription. **Resolution**: Plan-phase decision in `tasks.md` T102 (`registry-delete-confirmation.tsx`) implements the modal confirmation pattern; specific copy/typed-name pattern is a design-phase choice within FR-030's bounds.
- [X] CHK013 — Azure Service Bus naming rules cited. **Resolution**: Cited by source in `spec.md` FR-002 (link to `learn.microsoft.com/azure/service-bus-messaging/service-bus-quotas`); per-type regex set in `data-model.md` §3.2.
- [X] CHK014 — Conflict response "fields changed" definition. **Resolution**: Schema-locked in `contracts/conflict-response.schema.json` (current state + `changedFields[]` with `field, currentValue, submittedValue`). Promoted shape into `spec.md` FR-032 via the audit-event clause.
- [X] CHK015 — Visual treatment for Deprecated entities. **Resolution**: FR-013a + FR-047 establish the criteria (color + icon + text, not color alone); specific design tokens are design-phase (Storybook). `tasks.md` T090 implements `registry-status-badge.tsx`.

## Requirement Consistency

- [X] CHK016 — Namespace uniqueness rule for null-parent case. **Resolution**: Made explicit in amended `spec.md` FR-014 ("For Namespace entities, the uniqueness scope is `(name, environment)`...").
- [X] CHK017 — `owner` vs ownership-tag canonicality. **Resolution**: Promoted into `spec.md` FR-002 ("`owner` is the canonical owning team/person identifier; tags MAY carry ownership-style pairs but `owner` remains authoritative").
- [X] CHK018 — Status change as edit vs first-class action. **Resolution**: FR-013a treats it as a first-class audited action; `tasks.md` T081 implements a separate `PATCH /api/registry/{id}/status` endpoint; audit `eventType: "StatusChanged"` is distinct from `"Updated"` in `contracts/audit-event.schema.json`. Consistent across API, audit, UI.
- [X] CHK019 — Source-of-truth rule applied consistently to tree expansion / child enumeration / relationships. **Resolution**: FR-026 reads "browse and detail" universally; `research.md` §12 and `data-model.md` confirm Cosmos is the source for explorer tree, child enumeration, and detail-page relationships. AI Search is used ONLY for the search box.
- [X] CHK020 — Tree expansion lazy/paginated/eager. **Resolution**: Promoted into `spec.md` FR-027 ("lazy-loaded server-side; same continuation-token pagination shape as the list endpoint").

## Acceptance Criteria Quality

- [X] CHK021 — SC-002/003/004 "expected load" measurable. **Resolution**: Same source as CHK009 — `spec.md` §Assumptions defines "expected load" numerically. SCs cite Assumptions implicitly.
- [X] CHK022 — SC-008 zero violations without naming scanner. **Resolution**: SC-008 reads "passes automated WCAG 2.2 AA accessibility checks with zero violations" — already scanner-neutral. `tasks.md` uses axe-playwright but the criterion does not bind that choice.
- [X] CHK023 — Story 2 AC #3 vs SC-005 reconciliation. **Resolution**: Story 2 AC #3 rewritten to reference the SC-005 budget directly ("within the SC-005 budget (under five seconds at p95 under normal indexing-pipeline conditions)").
- [X] CHK024 — SC-001 and SC-010 scenario specification. **Resolution**: `quickstart.md` §5 + §6 + new §G3 timed-find scenario in `tasks.md` T129b provide the reproducible test scenarios. Spec-level addition would be redundant.

## Scenario & Edge Case Coverage

- [X] CHK025 — Index-pipeline permanent-failure recovery path. **Resolution**: Promoted into `spec.md` FR-025 ("re-deploy the indexer with a mapping fix OR a re-touch no-op PUT; first-class retry-index action reserved for future ops-hardening spec"). Added to §Non-Goals.
- [X] CHK026 — Session-expiry mid-form recovery. **Resolution**: Expanded "Unauthenticated/unauthorized access" Edge Case in `spec.md` (best-effort browser-session preservation, re-auth CTA returns to same URL to restore form). Implementation in `tasks.md` T103a (`registry-unauthorized-state.tsx`) + T103d Playwright test.
- [X] CHK027 — Tag case-collision AC. **Resolution**: New Edge Case "Tag key case-collision" added to `spec.md` (case-insensitive match, first-write display wins, behavior on both POST and PUT).
- [X] CHK028 — Bulk operations scope. **Resolution**: Explicit Non-Goal added to `spec.md` §Non-Goals ("no multi-select edit, no multi-delete, no bulk import/export, no batch tagging — reserved for future productivity spec").
- [X] CHK029 — Parent Deprecated, can children be created. **Resolution**: New Story 1 AC #7 added to `spec.md` (warn-loudly-but-allow + audit `changeSummary` prefix `UNDER_DEPRECATED_PARENT:`). `tasks.md` T075 implements server-side prefix; T095/T097/T098 implement form-level warning banner.

## Non-Functional & Cross-Cutting

- [X] CHK030 — Encryption at rest / in transit. **Resolution**: New §Assumptions bullet ("Encryption — inherited") citing spec 005's FR-019..FR-023, FR-041..FR-042. No new requirement; inheritance documented.
- [X] CHK031 — Data residency / region constraints. **Resolution**: New §Assumptions bullet ("Data residency — inherited") citing spec 005's per-env region configuration. Compliance-driven residency declared a §Non-Goal for v1.
- [X] CHK032 — i18n/locale formatting beyond RTL. **Resolution**: `spec.md` §Assumptions already says "English-only content, RTL-safe foundation" + tech-stack reference covers locale-aware date/number/duration formatting. No spec change needed.

## Dependencies & Assumptions

- [X] CHK033 — Section-level prior-spec references. **Resolution**: New §Assumptions bullet "Prior-spec section references" enumerates exact sections consumed from specs 003, 004, 005.
- [X] CHK034 — Deployment-constraint verification for FR-037 tenant-restriction assumption. **Resolution**: Promoted into `spec.md` FR-037 ("Pre-deployment verification — captured as attestation in the deployment runbook under 'Pre-go-live attestations'"). New §Assumptions bullet "Pre-go-live verification".

## Ambiguities & Conflicts

- [X] CHK035 — "Tenant" definition. **Resolution**: Promoted into `spec.md` FR-037 ("the Microsoft Entra tenant configured in spec 003; any principal whose access token's `tid` claim matches the configured tenant id qualifies").
- [X] CHK036 — Unquantified-adjective sweep. **Resolution**: Verified — `spec.md` does not carry the source-artifact's "modern / fast / minimal / enterprise-grade / dense but readable" qualifiers from its UX Requirements section. The spec's quality-bound phrasing throughout uses measurable language (perf budgets via FR-043..FR-045 + SC-002..SC-005; accessibility via FR-046..FR-048 + SC-008; visual-treatment criteria via FR-047). No spec change needed.

## Notes

- Items marked **[Gap]** point at things the spec does not say at all.
- Items marked **[Ambiguity]** point at things the spec says vaguely.
- Items marked **[Consistency]** point at potential tension between two requirements that may already be reconcilable but need a clarifying re-read.
- Items marked **[Clarity]** point at requirements that would benefit from more precise phrasing.
- Items marked **[Measurability]** point at SCs that may not be objectively testable as written.
- **Recommendation**: Resolve [Gap]s and [Ambiguity]s before `/speckit-plan` (these will become open questions during planning otherwise). [Consistency] and [Clarity] items can be tightened during the planning phase if they don't change scope.
- Items not resolved here should either (a) be added to the Clarifications section via another `/speckit-clarify` pass, (b) be promoted to explicit FRs/SCs by editing `spec.md`, or (c) be marked as intentionally deferred (with rationale).

---

## Walk Summary (2026-06-02)

All 36 items resolved. Breakdown:

| Disposition | Count | Items |
|---|---|---|
| Promoted to `spec.md` (FR/SC/AC/Edge Case/Assumption/Non-Goal amendments) | 22 | CHK001, CHK002, CHK003, CHK004, CHK005, CHK007, CHK010, CHK011, CHK013, CHK014, CHK016, CHK017, CHK020, CHK023, CHK025, CHK026, CHK027, CHK028, CHK029, CHK030, CHK031, CHK033, CHK034, CHK035 |
| Resolved by plan-phase artifact (no spec change needed) | 9 | CHK006, CHK008, CHK009, CHK012, CHK015, CHK018, CHK019, CHK022, CHK032 |
| Resolved by tasks-phase artifact | 1 | CHK024 (covered by `quickstart.md` walkthroughs + new T129b timed test) |
| Verified by inspection (no change needed) | 1 | CHK036 (unquantified-adjective sweep — spec is clean) |

The walk also produced two corresponding tasks.md amendments (T075 audit prefix, T095/T097/T098 form warning banner) per CHK029.

`spec.md` is ready for `/speckit-implement`. Phase 1 may begin.
