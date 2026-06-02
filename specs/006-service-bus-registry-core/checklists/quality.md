# Requirements Quality Checklist: Service Bus Registry Core

**Purpose**: Author-side audit of `spec.md` for completeness, clarity, consistency, and measurability before `/speckit-plan`. Each item is a unit test for the requirements writing, not for the implementation.
**Created**: 2026-06-01
**Feature**: [spec.md](../spec.md)
**Audience**: Spec author, pre-plan
**Depth**: Standard

## Requirement Completeness

- [ ] CHK001 — Is the numeric reliability/availability target for the registry quantified (uptime %, RTO, RPO)? [Gap]
- [ ] CHK002 — Is an audit-event retention window specified (how long are audit records kept, can they be archived)? [Gap, Spec §FR-032 to §FR-034]
- [ ] CHK003 — Are upper bounds specified for entity name length, description length, tag key length, tag value length, and the maximum number of tags per entity? [Gap, Spec §FR-002]
- [ ] CHK004 — Are pagination defaults defined (default page size, maximum page size, what happens when a request exceeds the max)? [Gap, Spec §FR-024]
- [ ] CHK005 — Is the composition rule for `fullyQualifiedName` specified for each entity type (e.g., `namespace/queue` vs `namespace/topics/<topic>/subscriptions/<sub>/rules/<rule>`)? [Completeness, Spec §FR-002]
- [ ] CHK006 — Are entity-type-specific fields (e.g., queue delivery behavior, topic settings) clearly delegated to the extensible `metadata` field, or are required type-specific fields enumerated? [Completeness, Spec §FR-002]
- [ ] CHK007 — Is the mechanism for managing the configurable environment list (who configures it, where it lives, how a new environment becomes available to operators) specified? [Gap, Spec §FR-035, §FR-036]
- [ ] CHK008 — Are search relevance/ranking expectations specified beyond the word "ranked" (e.g., name-prefix matches outrank metadata matches)? [Gap, Spec §Story 2 AC #1]

## Requirement Clarity

- [ ] CHK009 — Is "expected load" in FR-043/FR-044/FR-045 and SC-002/SC-003/SC-004 quantified with specific concurrency and registry-size assumptions, or only described qualitatively in the Assumptions section? [Clarity, Spec §FR-043 to §FR-045, §SC-002 to §SC-004, §Assumptions]
- [ ] CHK010 — Is the shape and content of "human-readable change summary" (FR-032) defined (free-form sentence vs structured diff vs field-list)? [Ambiguity, Spec §FR-032]
- [ ] CHK011 — Is "stable ordering" for paginated search results (FR-024) defined by an explicit sort key (e.g., score then id, name ascending)? [Clarity, Spec §FR-024]
- [ ] CHK012 — Is "explicit confirmation" for deletion (FR-013, FR-030) prescribed with a specific UX pattern (e.g., modal with typed-name confirmation, single-click confirm), or left to planning? [Clarity, Spec §FR-013, §FR-030]
- [ ] CHK013 — Are the Azure Service Bus naming rules referenced by FR-015 cited by source/version, or restated, so frontend and backend can validate identically? [Clarity, Spec §FR-015, §FR-017]
- [ ] CHK014 — Is the "identification of which fields changed" returned by the conflict response (FR-020) defined as a field-name list, a diff payload, or both? [Ambiguity, Spec §FR-020]
- [ ] CHK015 — Is the visual treatment that "must visually distinguish `Deprecated` entities" (FR-013a) specified with measurable criteria, or left to the design phase? [Clarity, Spec §FR-013a]

## Requirement Consistency

- [ ] CHK016 — Does the duplicate-name rule in FR-014 ("same parent scope and environment") read consistently for top-level Namespaces, which have no parent? Specifically: is the Namespace uniqueness rule "name unique per environment" written explicitly anywhere? [Consistency, Spec §FR-014, §Story 1 AC #5]
- [ ] CHK017 — Is the relationship between `owner` (a first-class field on every entity, FR-002) and tags that signal ownership (Key Entities §Tag) clarified so operators know which is canonical? [Consistency, Spec §FR-002, §Key Entities — Tag]
- [ ] CHK018 — Is `status` change from `Active` to `Deprecated` an "edit" (covered by FR-012's mutable-field rule) or a separate first-class action (FR-013a)? Is the distinction reflected consistently in API surface, audit-event categorization, and UI? [Consistency, Spec §FR-012, §FR-013a]
- [ ] CHK019 — FR-026 says browse and detail are served from the persistent store. Is the source-of-truth rule applied consistently to tree expansion, child enumeration on detail pages, and relationship traversal — or could any of those legitimately read from the search index? [Consistency, Spec §FR-026, §FR-007, §FR-027, §FR-028]
- [ ] CHK020 — Are FR-024 (paginated search) and FR-027 (tree explorer with expand/collapse) consistent about whether tree expansion is server-paginated, lazy-loaded, or eagerly loaded? [Consistency, Gap, Spec §FR-024, §FR-027]

## Acceptance Criteria Quality

- [ ] CHK021 — Are the "expected load" qualifiers in SC-002, SC-003, and SC-004 measurable without circular reference (i.e., is "expected load" defined numerically somewhere a test plan can cite)? [Measurability, Spec §SC-002 to §SC-004]
- [ ] CHK022 — Is SC-008's "passes automated WCAG 2.2 AA accessibility checks with zero violations" measurable without naming a specific scanner (which can drift)? Is the criterion stated as "any conformant WCAG 2.2 AA evaluator", or is a named tool acceptable? [Measurability, Spec §SC-008]
- [ ] CHK023 — Are Story 2 AC #3 ("within a reasonable indexing window — a few seconds at most under normal conditions") and SC-005 ("under five seconds at the 95th percentile under normal indexing-pipeline conditions") reconciled into one stated target? [Consistency, Spec §Story 2 AC #3, §SC-005]
- [ ] CHK024 — Are the operator-journey timing claims (SC-001 "under 10 minutes", SC-010 "under 30 seconds") accompanied by enough scenario specification (registry preload, network conditions, operator familiarity) to be reproducibly measured? [Measurability, Spec §SC-001, §SC-010]

## Scenario & Edge Case Coverage

- [ ] CHK025 — Is the recovery path for index-pipeline failures whose retries exhaust (FR-025 says "permanent failures MUST be observable in telemetry") specified — does an operator action exist to re-trigger indexing, or is the dead-letter only visible passively? [Gap, Spec §FR-025]
- [ ] CHK026 — Are session-expiry mid-form scenarios specified beyond "preserve form data where reasonable" (Edge Cases) — what does "reasonable" mean, and is re-auth + auto-resume in scope? [Ambiguity, Spec §Edge Cases — unauth]
- [ ] CHK027 — Is the case-collision behavior for tag keys (FR-002: case-insensitive match, case-preserving display, "first-written casing wins") covered by an acceptance scenario or edge case so QA can validate it? [Coverage, Spec §FR-002]
- [ ] CHK028 — Are bulk operations (multi-select edit, multi-delete, import/export) explicitly declared out of scope, or are they unaddressed by accident? [Gap, Coverage]
- [ ] CHK029 — Is the scenario "parent entity is `Deprecated` — can children still be created beneath it?" addressed, or is the behavior left to be inferred? [Gap, Coverage, Spec §FR-013a]

## Non-Functional & Cross-Cutting

- [ ] CHK030 — Are data-at-rest and data-in-transit encryption requirements specified at the spec level, or are they assumed-inherited from the constitution/tech-stack? [Coverage, Spec §Security Requirements]
- [ ] CHK031 — Are data residency / region constraints specified for the persistent store and search service (relevant if any environment has compliance obligations)? [Gap]
- [ ] CHK032 — Is i18n/locale handling beyond "RTL-safe foundation" specified — specifically, are date/number/duration formatting requirements written into the spec or only inherited from the project convention? [Clarity, Spec §Assumptions]

## Dependencies & Assumptions

- [ ] CHK033 — Is the dependency on prior specs (003 Auth/Identity, 004 Core Domain Model, 005 Infrastructure Baseline) cited with section-level references rather than spec-level references, so plan-phase consumers know exactly which prior decisions they inherit? [Traceability, Spec §Assumptions]
- [ ] CHK034 — Is the assumption "initial deployments will be in tenants/environments scoped to messaging engineering teams" (justifying the no-RBAC choice in FR-037) recorded as an explicit deployment constraint that someone must verify before each environment go-live? [Assumption, Spec §FR-037, §Assumptions]

## Ambiguities & Conflicts

- [ ] CHK035 — Does "any authenticated tenant user" (FR-037, Clarifications Q4) need a definition of "tenant" — is it the Entra tenant configured in spec 003, or could it expand later? [Ambiguity, Spec §FR-037]
- [ ] CHK036 — Is there any remaining requirement in the spec that uses an unquantified adjective ("modern", "fast", "minimal", "enterprise-grade", "dense but readable" — the UX Requirements section) without a measurable translation in either FRs or SCs? [Ambiguity, Spec §UX Requirements (per source artifact)]

## Notes

- Items marked **[Gap]** point at things the spec does not say at all.
- Items marked **[Ambiguity]** point at things the spec says vaguely.
- Items marked **[Consistency]** point at potential tension between two requirements that may already be reconcilable but need a clarifying re-read.
- Items marked **[Clarity]** point at requirements that would benefit from more precise phrasing.
- Items marked **[Measurability]** point at SCs that may not be objectively testable as written.
- **Recommendation**: Resolve [Gap]s and [Ambiguity]s before `/speckit-plan` (these will become open questions during planning otherwise). [Consistency] and [Clarity] items can be tightened during the planning phase if they don't change scope.
- Items not resolved here should either (a) be added to the Clarifications section via another `/speckit-clarify` pass, (b) be promoted to explicit FRs/SCs by editing `spec.md`, or (c) be marked as intentionally deferred (with rationale).
