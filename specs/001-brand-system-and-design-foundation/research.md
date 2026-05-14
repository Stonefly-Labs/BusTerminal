# Phase 0 — Research: Brand System and Design Foundation

**Spec**: [`spec.md`](./spec.md)
**Plan**: [`plan.md`](./plan.md)
**Constitution**: [`../../.specify/memory/constitution.md`](../../.specify/memory/constitution.md)
**Tech Stack Reference**: [`../../speckit-artifacts/tech-stack.md`](../../speckit-artifacts/tech-stack.md)

This document resolves the open implementation questions identified during planning and records the decisions that the rest of the plan rests on. Each entry follows the **Decision / Rationale / Alternatives** pattern. Items called out in the `/speckit-plan` user input are addressed first.

---

## R1. Component Documentation System — Storybook vs. Equivalent

**Decision**: Adopt **Storybook 8.x** with the official `@storybook/nextjs` framework, configured for the App Router. Stories live alongside each component as `<Component>.stories.tsx`. Required addons:

- `@storybook/addon-a11y` — axe-core powered accessibility checks per story (satisfies FR-027 automation gate inside Storybook).
- `@storybook/addon-themes` — dark / light toggle and side-by-side preview.
- `@storybook/addon-interactions` + `@storybook/test` — interaction tests (keyboard, focus, click) for primitives that have stateful behavior (Tabs, Dialog, Combobox, Command).
- `@storybook/addon-viewport` — wide-desktop, laptop, tablet, mobile, and the 13" laptop reference viewport used by SC-010.
- `storybook-addon-rtl-direction` (or equivalent) — `dir="rtl"` toggle for the manual review required by SC-011.

Storybook static export is produced in CI and published as a build artifact (a deployable site is a future enhancement).

**Rationale**:

- **Coverage of acceptance criteria.** Storybook is the only mature option that simultaneously delivers per-story a11y validation (FR-027 / SC-005), dark/light parity preview (SC-005), interaction testing for primitives (User Story 1 / 3), responsive viewport preview (SC-010), and RTL toggle for the manual review (SC-011) — all from one tool with first-party Next.js integration.
- **Ecosystem alignment.** shadcn/ui examples, lucide-react, TanStack Table, React Hook Form, and Radix primitives all ship documentation and reference stories in Storybook format. Adopting the same format keeps contributor and agent muscle memory aligned with the upstream ecosystem (Decision Priorities #2 Developer Productivity, #3 Maintainability).
- **Agent discoverability.** The shadcn MCP server and most coding agents already understand Storybook layouts; an unconventional alternative would add an onboarding tax for agentic workflows (FR-031 / FR-032 — agentic implementation guidance).
- **Operational simplicity.** Storybook 8 is a mature, well-known tool with predictable upgrade behavior; an in-house docs system would invent a new maintenance burden contrary to Decision Priority #1.

**Alternatives considered**:

| Option | Why rejected |
|---|---|
| **Ladle** (Vite-based) | Faster cold start, but no first-party Next.js framework integration; a11y/RTL/interactions story tooling is thinner; would force a parallel build pipeline divorced from Next.js. |
| **Histoire** | Strong on Vue, weaker for React + RSC; smaller ecosystem; less agent familiarity. |
| **Custom Next.js docs route** | Would replicate Storybook features at high cost; impossible to claim parity on accessibility automation; bypasses FR-030's spirit of a discoverable component documentation system. |
| **No formal docs system; rely on inline JSDoc** | Fails FR-030 outright. Removed from consideration. |

**Impact on the plan**:

- Storybook is established in Phase A so primitive stories can be written as the components themselves are built (FR-013, FR-028).
- `@storybook/test-runner` + `axe-playwright` are wired into CI to fulfill FR-027 and the SC-005 / SC-009 quality gates.

---

## R2. Soft Initial-JS Bundle Target for the Application Shell (FR-035e / SC-020)

**Decision**: The application shell's First Load JS for the root authenticated route (`(app)/layout.tsx` rendered for `/`) carries a **soft target of ≤ 180 KB gzipped** with a hard alert threshold at **200 KB gzipped**. The actual measured baseline at foundation handoff (after Next.js 16 + shadcn/ui base primitives + next-themes + observability adapter shim + W3C Trace Context propagator are wired) is captured in `web/docs/performance-budget.md` and surfaced via `@next/bundle-analyzer` output uploaded as a CI artifact.

The CI bundle report SHALL be diffed against the recorded baseline on every PR; an increase greater than **+10%** OR a crossing of the **200 KB** hard alert threshold fails the PR check and requires explicit reviewer acknowledgement.

**Rationale**:

- **Anchored in realistic Next.js 16 + shadcn/ui shells.** Reference measurements from comparable App-Router applications using shadcn/ui + Radix primitives + next-themes + a thin observability adapter typically land in the **130 KB – 180 KB gzipped** First Load JS range for the shell route. 180 KB gives headroom for the foundation deliverables without sandbagging the budget.
- **Hard alert at 200 KB.** The Core Web Vitals "Good" thresholds in FR-035d / SC-019 (LCP ≤ 2.5s, INP ≤ 200ms) drive the upper bound. Industry data consistently shows First Load JS above ~200 KB gzipped puts mid-range hardware over broadband at risk of missing the LCP target without aggressive code-splitting, so 200 KB is the latest defensible alarm point for a foundation app shell.
- **Regression alert path.** Constitution Principle V (Operational Excellence) and FR-035e require detectability. `@next/bundle-analyzer` produces deterministic per-route bundle reports that diff cleanly across commits and are already a Next.js-native solution (Decision Priority #1 Operational Simplicity).
- **Soft, not hard, for the foundation.** The spec deliberately calls this a "soft target" (FR-035e). Feature PRs that legitimately need to push past 180 KB (e.g., the first chart-heavy screen using Recharts) can do so with reviewer acknowledgement and a written justification; the hard alert is the bright line.

**Tooling**:

- `@next/bundle-analyzer` runs in `next build` mode on every PR via a `pnpm analyze` script.
- The CI workflow uploads `web/.next/analyze/client.html` and a JSON summary (`web/.next/analyze/bundle-summary.json`) as a PR artifact.
- A small shell-size diff script (`scripts/bundle-diff.mjs`) compares the new summary against the committed baseline at `web/docs/performance-budget.json` and prints a PR comment with red/green status.

**Alternatives considered**:

| Option | Why rejected |
|---|---|
| **Defer the target entirely until handoff** | Violates FR-035e — the spec requires the target be published, not just the measurement. Without an anchor value, contributors cannot evaluate regressions during foundation development. |
| **Set a stricter target (e.g., 120 KB)** | Risks gaming: the foundation would have to defer baseline primitives (Command palette, Combobox, animation primitives) to under-counting tricks. The decision priorities place Performance below Operational Simplicity and Maintainability. |
| **Lazy load all primitives client-side** | Defeats FR-035f's directive that the foundation favor patterns protecting Web Vitals. Excessive client-only splitting harms INP and shifts complexity into feature code. |
| **Use Lighthouse-CI score as the only gate** | Lighthouse is noisy on local-runner hardware; bundle size is deterministic and reviewable. Lighthouse remains useful at a higher cadence but is not the regression alert path. |

**Open follow-up**: The measured baseline value is recorded in `web/docs/performance-budget.md` at the moment the foundation is feature-complete; that number is the canonical "shell size at foundation handoff" referenced by SC-020. The 180 KB / 200 KB targets in this document remain the budget envelope.

---

## R3. AI-Assisted Brand Asset Production and Required Human Review (FR-002a / FR-002b)

**Decision**: Brand assets follow a **five-stage pipeline** with a recorded human originality/licensing review as the gating commit criterion.

```text
Stage 1 — AI-assisted concept generation
Stage 2 — Vector authoring & hand cleanup (plain SVG, no rasters/base64)
Stage 3 — Variant export (wordmark / compact mark / favicon set / social preview / repo banner)
Stage 4 — Human originality & licensing review (recorded)
Stage 5 — Commit & integration into the foundation
```

The output of stage 4 — a short Markdown record — is the **hard precondition** for stage 5 and is what FR-002b means by "a licensing/originality review MUST be recorded for any AI-assisted asset before it is committed."

**Stage-by-stage definition**:

| Stage | Activity | Tools (suggested, not mandated) | Artifact |
|---|---|---|---|
| 1 | AI-assisted concept exploration against the brand traits (technical, reliable, modern, precise, efficient, operational, open) and the visual themes called out in the source artifact (transit terminal, message routing paths, hub-and-spoke, signal flow). | Recraft, Figma AI, DALL-E, Midjourney, Affinity Designer with AI plugin | A small set of concept rasters and exploratory vectors, **kept out of the production repo** in a scratch folder. |
| 2 | Manual vector authoring: redraw or refine the chosen concept in plain SVG. No embedded rasters. No base64 image data. Stroke widths, anchors, and naming follow project conventions. | Figma → "Export as SVG" + manual cleanup, Affinity Designer, Inkscape | The committed source SVG files in `web/brand/`. |
| 3 | Variant export for every required form: full wordmark (dark + light), compact glyph (dark + light), favicon set (`favicon.ico` + multi-size `.png` + Apple touch), social preview (`og-image.png` 1200×630), and the GitHub repo banner. | Same tools as Stage 2 plus an export script that ships in `web/scripts/brand-export.mjs`. | Files under `web/public/brand/` and `web/public/favicons/`. |
| 4 | Human originality and licensing review. The reviewer (a human contributor — agentic completion does not satisfy this requirement) confirms: no third-party trademark is reproduced, no copyrighted likeness is present, no near-reproduction of training data is identifiable, and the resulting asset is redistributable under the project's open-source license. Reviewer signs off in a Markdown record. | USPTO TESS (US trademark) lookup, trademarkia.com / WIPO Global Brand Database for international checks, reverse-image search against the candidate raster (TinEye / Google Lens) | `web/brand/<asset-name>/REVIEW.md` (template below). |
| 5 | Commit & integrate: SVG sources go into `web/brand/`, exported variants into `web/public/`, references into `app/layout.tsx` (favicons, OpenGraph) and the Storybook brand page. | n/a | The brand assets land on the feature branch and are documented in the foundation handoff. |

**`REVIEW.md` template** (placed alongside the SVG source for each AI-assisted asset):

```markdown
# Brand Asset Originality & Licensing Review

**Asset**: <name + relative paths>
**Stage 1 tool**: <e.g., Recraft v3 / DALL-E 3 / Figma AI / Midjourney>
**Reviewer**: <human contributor GitHub handle>
**Review date**: <YYYY-MM-DD>

## Checks performed

- [ ] Trademark search performed (USPTO TESS + WIPO Global Brand Database). Result: <summary>
- [ ] Reverse-image search against candidate rasters. Result: <summary>
- [ ] Visual inspection for inadvertent reproduction of well-known logos / icon families. Result: <summary>
- [ ] License confirmation: asset is redistributable under <repo license>. Result: <summary>
- [ ] SVG hygiene: plain paths, no embedded rasters, no base64. Result: <summary>

## Decision

- [x] Approved for commit
- [ ] Rework required (notes below)

## Notes
```

**Sequencing within the implementation plan**:

The brand-asset work runs as a **parallel track** to the primitive/token track from Phase A onward, but with a hard cutoff before Phase F (handoff). Until the final assets land, the foundation uses a **placeholder mark** (a simple monospace text "BusTerminal" wordmark with a 1-color geometric glyph) committed under the same `web/brand/` layout so that import paths and Storybook pages do not change when the real assets land. The placeholder is itself authored as plain SVG by a human contributor and does not require an AI review record.

```text
Phase A ──────────► Phase B ──────────► Phase C ──────────► Phase D ──────────► Phase E ──────────► Phase F
 tokens                primitives          composites           a11y/i18n            obs/perf            handoff
                       ▲                                                                                  ▲
                       │                                                                                  │
                       └──── placeholder brand mark in place ────┐                                        │
                                                                 │                                        │
                                                                 ▼                                        ▼
   Brand assets track  ───── Stage 1 ── Stage 2 ── Stage 3 ───── Stage 4 (review) ─────── Stage 5 (commit, before handoff)
```

**Rationale**:

- **Compliance with FR-002a / FR-002b is non-negotiable.** Splitting AI generation from human review and gating commit on a recorded artifact is the literal reading of those requirements.
- **Decoupled tracks.** Brand asset production has long cycle times (concept exploration, manual cleanup, review) that do not block the foundation's deeper structural work. Treating it as a parallel track lets the foundation make progress without waiting on a finalized mark (Decision Priority #2 Developer Productivity).
- **Placeholder mark eliminates rework.** Using a deliberately-minimal placeholder mark with the final path/filename conventions means primitive code, Storybook references, favicon links, and OpenGraph metadata don't change when the real assets land — they're hot-swapped in stage 5.
- **Open-source readiness.** The recorded review pattern is reusable for any future AI-assisted brand additions (illustrations, social-media headers, marketing visuals) and is auditable by community contributors evaluating whether to depend on the project (Constitution Open-Source Commitments).

**Alternatives considered**:

| Option | Why rejected |
|---|---|
| **Inline review in PR description only** | Hard to discover after merge; PR descriptions are not source-controlled; future audits of the SVG provenance would have to walk Git history into GitHub UI. The `REVIEW.md` adjacent to the SVG keeps the record close to the artifact. |
| **Block all primitive work until brand is final** | Adds days/weeks of serial delay for a foundation whose primary value is unblocking feature development. Decision Priority #2 favors parallelism. |
| **Skip placeholder mark; let primitives live without a logo until brand lands** | Forces churn in `app/layout.tsx`, favicon links, OG metadata, and Storybook brand stories once the real assets arrive. Placeholder eliminates the churn for the cost of a one-evening SVG sketch. |
| **Treat AI-assisted assets as "no commit" and ship rasters only** | Violates FR-002a (plain SVG required) and FR-002b (redistributable source required). |

---

## R4. Observability Adapter Interface and W3C Trace Context Propagation

**Decision**: A single `ObservabilityAdapter` interface ships in `web/lib/observability/`, with two implementations: `noopAdapter` (default, no environment variable required) and `applicationInsightsAdapter` (activated when `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` is set). The adapter is selected at module load time and exposed through a tiny `getAdapter()` accessor; React components and the fetch wrapper depend on the interface, not the implementation (FR-040).

**W3C Trace Context propagation is implemented independently of the adapter** in `web/lib/http/trace-context.ts`. Every UI-originated `fetch` call routes through `web/lib/http/client.ts`, which:

1. Generates a fresh `traceparent` header (`00-{32-hex traceId}-{16-hex spanId}-01`) when none is present in the current request scope, OR forwards the existing one when the request is on behalf of an active span.
2. Forwards `tracestate` unchanged when present.
3. Emits a span (or no-op) through the adapter so that AI receives the same trace ID it propagates to the backend (FR-039 / SC-014).

This means trace context is on the wire **regardless of whether the AI connection string is configured**, satisfying FR-039's "applies regardless of whether the Application Insights connection string is configured locally" requirement.

**Implementation libraries**:

- `@microsoft/applicationinsights-web` — the AI browser SDK; loaded only by the AI adapter, dynamically imported so the no-op path doesn't pay the JS cost (helps R2 / FR-035e).
- `web-vitals` (Google) — Web Vitals capture (LCP, INP, CLS, TTFB, FCP) per FR-037 / SC-016; sourced once, forwarded through the adapter.
- The W3C Trace Context generator is hand-rolled (~30 lines) so the no-op path has zero dependencies and the AI SDK is not on the critical client path.

**Rationale**:

- **Interface-first.** Constitutional Principle VI (Incremental Extensibility) plus FR-040's explicit "adapter interface" requirement. Future adapters (OTLP-direct, console-only for debugging, third-party providers) drop in without touching feature code.
- **Trace context independence.** The hard requirement in FR-039 / Constitution Principle V is met without forcing OSS contributors to enable AI. The 30-line generator is operationally simple and dependency-free (Decision Priority #1).
- **Dynamic import for AI SDK.** Keeps the no-op First Load JS path lean for OSS contributors and for the soft bundle target in R2.
- **PII hygiene.** The adapter interface accepts only sanctioned attribute shapes (correlation IDs, route paths, vital names + values, error category + component stack). PII fields are not part of the interface, so feature code cannot accidentally leak them (FR-041 / Constitution Principle IV).

**Alternatives considered**:

| Option | Why rejected |
|---|---|
| **Use the AI SDK's built-in distributed tracing without an adapter abstraction** | Couples feature code to the AI SDK; OSS contributors who never configure AI still pay for the dependency; future adapter swap is a wide refactor. |
| **Use OpenTelemetry JS browser SDK directly** | Higher upfront complexity than warranted for a foundation; the AI SDK's W3C output is sufficient for end-to-end correlation. The OTel browser SDK can be added as a future adapter without touching feature code. |
| **Only propagate trace context when AI is configured** | Direct violation of FR-039 and the constitution-derived "applies regardless of whether the Application Insights connection string is configured locally" rule. |

---

## R5. i18n String Surface and Locale-Aware Formatting (FR-022a — FR-022d)

**Decision**: User-facing strings for the foundation live in `web/lib/i18n/strings/en.ts`, exposed through a `t(key)` accessor. The surface is **dependency-free** for v1 (a hand-rolled, type-safe key/value module with no runtime translation library) so the foundation does not lock in a specific i18n runtime that a future translation spec may revise.

Locale-aware formatting helpers live in `web/lib/i18n/format.ts` and wrap the **`Intl` family** (`Intl.DateTimeFormat`, `Intl.RelativeTimeFormat`, `Intl.NumberFormat`, `Intl.ListFormat`):

- `formatDate(date, opts)`
- `formatTime(date, opts)`
- `formatRelativeTime(value, unit)`
- `formatDuration(ms)` — locale-aware composition of `Intl.NumberFormat` parts
- `formatNumber(value, opts)`
- `formatBytes(bytes)` — composed from `Intl.NumberFormat` with derived unit selection

Locale defaults to `navigator.language` (client) and the `Accept-Language` header (server / RSC), with a single override hook for future locale switching.

**RTL**: Tailwind v4's logical-property utilities (`ms-*`, `me-*`, `ps-*`, `pe-*`, `start-*`, `end-*`) are used exclusively. An ESLint rule (`eslint-plugin-tailwindcss` plus a custom rule that disallows the physical-direction utilities `ml-*`, `mr-*`, `pl-*`, `pr-*`, `left-*`, `right-*` outside the design-token files) enforces SC-012.

**Rationale**:

- **No premature library lock-in.** Adopting `next-intl` or `react-intl` now would commit the foundation to a specific message-loading runtime; the translation pipeline is explicitly deferred (FR-022a's parenthetical). A type-safe local module gives a swap point without changing call sites.
- **Native `Intl` is sufficient and standards-aligned.** The browser support matrix (last 2 versions of evergreen browsers) guarantees full `Intl` support including `Intl.RelativeTimeFormat` (no polyfill required), aligning with FR-035b.
- **ESLint enforcement is concrete and auditable.** SC-012 demands a zero-violation audit; a lint rule produces a deterministic CI gate.

**Alternatives considered**:

| Option | Why rejected |
|---|---|
| **`next-intl`** | Locks runtime behavior earlier than the spec requires; a future translation spec can adopt it without rewriting components if the local string surface is the contract. |
| **`react-intl`** | Mature but heavier bundle and message-format dialect lock-in. |
| **`@formatjs/intl` standalone** | Useful only when polyfilling older browsers; the support matrix doesn't need polyfills. |

---

## R6. Testing Strategy

**Decision**:

| Test type | Tool | Scope |
|---|---|---|
| Component / unit | **Vitest** + **React Testing Library** | All primitives in `components/ui/`, domain composites, hooks, formatters, observability adapter behavior. |
| Accessibility (component-level) | **`vitest-axe`** | Asserts axe-clean on every primitive's default state inside the component test file. |
| Accessibility (story-level) | **`@storybook/addon-a11y` + `@storybook/test-runner` + `axe-playwright`** | Runs axe across every published story in CI. |
| End-to-end | **Playwright** | The representative composed screen (User Story 1 / SC-001 / SC-007 / SC-019) on Chrome, Firefox, and WebKit. |
| Cross-browser smoke | **Playwright** with the configured projects matrix (Chromium, Firefox, WebKit) | Satisfies FR-035a / SC-018 by reusing the E2E spec across browsers. |
| Performance probe | **Playwright + Lighthouse worker** (optional addon) | Records LCP/INP/CLS for the representative screen on a tuned mid-range profile. |
| Bundle size | **`@next/bundle-analyzer`** + the shell-size diff script | Enforces R2's budget. |

**Rationale**: Mirrors the tech stack reference (section 8) exactly. No new tools introduced. CI runs `pnpm lint && pnpm typecheck && pnpm test && pnpm test:storybook && pnpm test:e2e && pnpm analyze`. SC-009 is met by ensuring each script runs cleanly on a clean checkout.

---

## R7. Theme Provider & Flash-Free First Paint (FR-006 / SC-004)

**Decision**: Use **`next-themes`** with the `class` strategy (`html.dark` / no class = light). The theme cookie is set by an inline script injected at the top of `<head>` via `next-themes`'s recommended pattern, which reads `localStorage` and the `prefers-color-scheme` media query before React hydrates — preventing FOUC.

Tailwind v4's `@custom-variant dark (&:is(.dark *))` directive (or equivalent) is used so CSS-variable themes flip on the `.dark` class.

**Rationale**: Industry-default approach for App Router; documented in shadcn/ui's own setup guide; satisfies FR-006 / SC-004 without a custom solution.

---

## R8. shadcn/ui Generation Workflow

**Decision**: Initialize shadcn/ui with `pnpm dlx shadcn@latest init` against the Next.js project, targeting `components/ui/`. Use the **"default" style** with the **slate base color** as the starting palette, then immediately override the CSS variables in `web/styles/tokens.css` to map onto the BusTerminal design tokens. The components themselves are committed to the repo and customized in-place (per FR-013 / FR-014 / Constitution frontend standard).

Components are added one-by-one (`shadcn add button input ...`) rather than via the `blocks` generator, to keep the audit trail per-primitive.

**Rationale**: shadcn/ui official path; agent-friendly (the shadcn MCP server already understands this workflow); avoids speculation about which blocks are needed.

---

## R9. Iconography (FR-021 / FR-022)

**Decision**: `lucide-react` is the icon family for both general and domain-specific use. Domain icons (queue, topic, subscription, dead-letter, namespace, message-flow, topology, discovery) are mapped to curated `lucide-react` icons via a constants module (`web/lib/iconography/domain-icons.ts`) so future swaps to a custom icon (if any are commissioned by a later spec) are one-file changes.

**Rationale**: Tech stack reference fixes the icon library. The mapping module preserves Constitutional Principle VI (Incremental Extensibility).

---

## R10. Chart Foundation (FR-029)

**Decision**: `recharts` is the standard chart library, wrapped by a thin `Chart*` primitive layer (`<ChartContainer>`, `<ChartLine>`, `<ChartBar>`, `<ChartArea>`) that:

- Injects theme-aware colors from the design tokens (no hardcoded color literals — SC-003).
- Wires accessibility labels and announces value changes per FR-026 (color is paired with text/label).
- Respects `prefers-reduced-motion` by disabling enter/update animations when set (FR-025).

**Rationale**: Mandated by the tech stack reference. Wrapping at a thin level rather than re-skinning Recharts internals is the lightest acceptable surface.

---

## Open Questions Resolved

All `NEEDS CLARIFICATION` items in the plan template are resolved by entries R1 – R10 above.

| Plan-template field | Resolution source |
|---|---|
| Language/Version | Tech stack reference §2 (TypeScript strict, Node 20.x LTS for tooling, Next.js 16.x) |
| Primary Dependencies | Tech stack reference §2 |
| Storage | N/A — frontend-only foundation; backend persistence is owned by future specs |
| Testing | R6 |
| Target Platform | Spec §FR-035a (browser matrix) |
| Project Type | Single Next.js application (`web/`) at repo root |
| Performance Goals | Spec §FR-035d (Core Web Vitals "Good") + R2 (bundle target) |
| Constraints | Spec §FR-035a / FR-035d + Constitution §IV (no PII default in telemetry, secrets only via env-gated vars) |
| Scale/Scope | Spec §Requirements (primitive set FR-013, domain composites FR-028) |

No unresolved clarifications remain at the end of Phase 0.
