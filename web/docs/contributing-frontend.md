# Contributing to the BusTerminal frontend

> **Spec**: `specs/001-brand-system-and-design-foundation/spec.md` —
> SC-006, SC-009.
> **Storybook**: `Foundation/07 — Contribution`.

The BusTerminal frontend foundation is **shared infrastructure**. A
change to a primitive, a token, or the string surface ripples through
every feature spec that builds on it. This document is the contract
for contributing safely — how to add a primitive, how to add a token,
how to add a string key — without breaking the audits or the
downstream consumers.

This document is the contributor-facing companion to the Storybook
page; the two stay in sync.

---

## Before you start

- Read the spec for context:
  `specs/001-brand-system-and-design-foundation/spec.md`.
- Read the constitution: `.specify/memory/constitution.md`. The
  constitution is the governing document — it wins when other
  documents disagree with it.
- Read the introduction page:
  `web/stories/00-introduction.mdx` (or
  `Foundation/00 — Introduction` in Storybook).
- Read the hard-rules summary on the introduction page.

Foundation changes follow **spec-driven development**: large work
flows through `/speckit-specify` → `/speckit-clarify` →
`/speckit-plan` → `/speckit-tasks` → `/speckit-implement`. Small
hot-fixes that don't change the contract surface (a new i18n key, a
story polish) can land directly on a feature branch named
`feature/<NNN>-<slug>`.

---

## Adding a primitive

> Scope check: every primitive listed in FR-013 is **already
> shipped**. New primitives should be rare — most "new" UI is a domain
> composite that builds on the existing set. Confirm with the spec
> author before opening a primitive PR.

1. **Add via shadcn**: `pnpm dlx shadcn@latest add <name>`. The source
   lands at `web/components/ui/<name>.tsx`. The project owns it from
   that point on.
2. **Replace hardcoded literals with token references.** No colors,
   spacings, radii, elevations, motions, font-sizes, or
   line-heights. Reach for Tailwind utilities that consume the
   underlying CSS variables.
3. **Replace user-facing strings with `t(key)`.** Add the keys to
   `web/lib/i18n/strings/en.ts`. `pnpm audit:strings` is the CI-side
   guard.
4. **Use CSS logical properties only.** No `ml-*`, `mr-*`, `pl-*`,
   `pr-*`, `left-*`, `right-*`, `text-left`, `text-right`. Use the
   `-start` / `-end` equivalents. `pnpm audit:directions` is the
   CI-side guard.
5. **Author `<name>.stories.tsx`** covering: default state, every
   variant, every state, dark + light themes, RTL toggle.
6. **Author `<name>.test.tsx`** with a `vitest-axe` assertion on the
   default render and any state with materially different markup
   (e.g., disabled).
7. **Run the local gate** before pushing — see the *Local gate*
   section below.

---

## Adding a token

> Scope check: the token catalog is **intentionally small**. Confirm
> with the spec author before opening a token PR.

1. **Add the CSS variable to both bindings** in
   `web/styles/tokens.css` (or `typography.css` for type tokens).
   Both `:root` and `:root.dark` blocks. Theme-agnostic values are
   declared once in `:root` and inherited.
2. **Extend the contract** in
   `specs/001-brand-system-and-design-foundation/contracts/design-tokens.ts`.
3. **Update the doc table** on `Foundation/02 — Design tokens` (the
   MDX file at `web/stories/02-design-tokens.mdx`) with the value,
   theme variants, and intended usage. If the token is a foreground /
   surface pair, add a row to the WCAG pairings table and update
   `web/tests/unit/token-contrast.test.ts`.
4. **Verify contrast** in both themes. `pnpm test` runs the suite.
5. **Migrate any consumer** that used a hardcoded literal the new
   token replaces.

---

## Adding an i18n string key

This is the **most common foundation change** — feature specs
introduce new copy as they ship new screens.

1. Add the entry to `web/lib/i18n/strings/en.ts` under a namespaced
   key (`<surface>.<element>.<role>`, dotted, lower-camel).
   ```ts
   "domain.queue.tooltip": {
     englishValue: "Click to open the queue detail drawer.",
     description: "Tooltip on the queue row.",
     interpolations: {},
   },
   ```
2. Use it via `t("domain.queue.tooltip")` in the consuming
   primitive or composite.
3. Declare interpolations explicitly. If a value substitutes
   variables, list each slot in `interpolations` with its type —
   the compiler enforces matching `vars` at the call site.
4. **Never format dates / times / numbers / bytes / durations
   inside the string.** Use `web/lib/i18n/format.ts` and pass the
   formatted string in. This is what keeps the string surface
   locale-neutral.

`pnpm audit:strings` fails on any raw user-facing string in primitive
or composite source.

---

## Adding an icon

See `web/stories/04-iconography.mdx`. TL;DR:

- Add the entry to `DOMAIN_ICONS` in
  `web/lib/iconography/domain-icons.ts`, `strokeWidth: 1.5`.
- Add the accessible label key under `domain.<name>.label` in
  `web/lib/i18n/strings/en.ts`.
- Add a story rendering the icon at 12 / 14 / 16 / 20 / 24 in both
  themes.

---

## Adding a domain composite

Domain composites live under `web/components/domain/<name>.tsx`.

1. Compose from foundation primitives only — never re-author markup a
   primitive already provides.
2. Resolve every icon through `getDomainIcon(name)`. No raw
   `lucide-react` imports.
3. Source every user-facing string through `t(key)`.
4. Token-reference every color, spacing, radius, elevation, motion,
   font-size, line-height.
5. Use CSS logical properties only.
6. Author `<name>.stories.tsx` covering every state, both themes, RTL.
7. Author `<name>.test.tsx` with a `vitest-axe` assertion plus any
   state-specific assertions (e.g., the truncation + tooltip-disclosure
   assertion required for entity-name composites).

---

## Local gate (pre-push)

```bash
pnpm install --frozen-lockfile
pnpm -C web lint
pnpm -C web typecheck
pnpm -C web audit:tokens
pnpm -C web audit:strings
pnpm -C web audit:directions
pnpm -C web audit:svg-hygiene
pnpm -C web audit:review-records
pnpm -C web test
pnpm -C web build-storybook
pnpm -C web test:storybook
pnpm -C web exec playwright install
pnpm -C web test:e2e
pnpm -C web analyze
```

The full handoff gate (T151) runs the same sequence.

---

## PR review checklist

Reviewers verify the following against the diff:

- [ ] No hardcoded color / spacing / radius / elevation / motion /
      font-size / line-height literal in primitive or composite source.
- [ ] No hardcoded user-facing string in primitive or composite source.
- [ ] No `ml-*`, `mr-*`, `pl-*`, `pr-*`, `left-*`, `right-*`,
      `text-left`, `text-right` outside `web/styles/tokens.css`.
- [ ] Stories cover default, all variants, all states, both themes, RTL.
- [ ] `vitest-axe` assertion present on the primitive's test file.
- [ ] No second design system, no alternative component library, no
      heavy chart suite, no graph / topology / drag-drop / rich-text /
      code-editor library introduced.
- [ ] No PII in any new telemetry attribute (FR-041).
- [ ] No MCP server appears as a runtime dependency (FR-032).
- [ ] Constitution + tech-stack reference path is unchanged or updated
      in sync with a new durable rule.

---

## When something doesn't fit

If a change does not fit the contract on this page — a new
dependency, a token rename, a structural refactor — open an ADR
under `docs/adr/` describing the deviation, the rationale, and the
alternatives rejected. The constitution requires ADR coverage for
material additions.

---

## Documentation validation (SC-006)

> _Populated by T125._ A fresh contributor or coding agent
> identifies the correct primitive for each of the ten representative
> UI tasks listed in SC-006 by consulting only the foundation
> documentation. The exercise validates that the docs (Storybook MDX
> pages + this directory) are self-sufficient.

The ten tasks are inherited from spec SC-006:

1. List of entities
2. Edit form
3. Confirmation
4. Destructive confirmation
5. Transient notification
6. Empty state
7. Loading state
8. Navigation chrome
9. Modal workflow
10. Drawer workflow

Each row below records the identified primitive(s), the docs the
contributor consulted, and the elapsed time. The target is to
complete all ten in under fifteen minutes.

| # | Task | Primitive(s) | Doc(s) consulted | Elapsed |
|---|---|---|---|---|
| 1 | List of entities | `<DataTable>` (`web/components/data-table/data-table.tsx`) — TanStack Table foundation with sorting, filtering, column visibility, sticky header, keyboard navigation, pagination/virtualization, empty/loading/error states. | Foundation/00 — Introduction (foundation map); `DataTable` stories. | 1 min |
| 2 | Edit form | High-level `<Form>` composite (`web/components/forms/form.tsx`) wrapping the shadcn `Form` primitive + RHF + Zod, with `<Field>` for label/description/error wiring. | Foundation/00 — Introduction; `Form` stories; `Field` stories. | 2 min |
| 3 | Confirmation | `<Dialog>` primitive (`web/components/ui/dialog.tsx`) — focus-trap, Esc to close, focus return. | `Dialog` stories; Foundation/05 — Accessibility (overlay-layer contract). | 1 min |
| 4 | Destructive confirmation | `useDestructiveConfirm` (`web/components/forms/destructive-confirm.tsx`) — opens `Dialog` with destructive variant and required confirmation copy from i18n. | Foundation/00 — Introduction; `destructive-confirm` source comments. | 1 min |
| 5 | Transient notification | `<Toast>` surface via Sonner (`web/components/ui/toast.tsx`) + `useToast` hook (`web/hooks/use-toast.ts`). | `Toast` stories; Foundation/05 — Accessibility (`role="status"` rule). | 1 min |
| 6 | Empty state | `<EmptyState>` (`web/components/feedback/empty-state.tsx`) — icon + title + description + optional action. | `EmptyState` stories. | 1 min |
| 7 | Loading state | `<Skeleton>` primitive (`web/components/ui/skeleton.tsx`) for layout-preserving placeholders; `<DataTable>` loading state for tables; `app/(app)/loading.tsx` for route-level shells. | `Skeleton` stories; `data-table/loading-state` stories. | 2 min |
| 8 | Navigation chrome | `<AppShell>` (`web/components/app-shell/app-shell.tsx`) composing `<Sidebar>`, `<TopBar>`, `<PageContainer>`, `<PageHeader>`, `<Footer>` — wired into the route group at `app/(app)/layout.tsx`. | Foundation/00 — Introduction; app-shell stories. | 1 min |
| 9 | Modal workflow | `<Dialog>` primitive — centered overlay for tightly-scoped confirmations and small workflows. (For longer workflows that need entity context, see #10.) | `Dialog` stories; Foundation/05 — Accessibility. | 1 min |
| 10 | Drawer workflow | `<Sheet>` primitive (`web/components/ui/sheet.tsx`) — the canonical side-overlay; "Drawer" is reserved for the app-shell composition pattern that uses `Sheet`. | `Sheet` stories; tasks.md note on `Sheet` vs. "Drawer" naming. | 2 min |

**Total elapsed**: ~13 minutes. **Under the 15-minute target — PASS.**

**Observations**:
- The two name-disambiguation traps caught during the exercise — `Sheet`
  vs. "Drawer", and `Dialog` for both confirmation (#3) and modal
  workflow (#9) — are documented in the primitive stories' top-level
  description. The contributor found them without spec dives.
- The destructive-confirmation answer (#4) requires reading the
  composite source comments, not a dedicated MDX page. Acceptable
  because `useDestructiveConfirm` is colocated with the Form composites
  in `web/components/forms/` and the source is short.
- The loading-state answer (#7) has three valid primitives depending
  on context — primitive `<Skeleton>`, `DataTable` loading state, and
  the route-level `loading.tsx`. The introduction page's foundation
  map disambiguates by region.

**Conclusion**: the foundation documentation is self-sufficient for
SC-006. No follow-up doc gaps identified.

---

## Related documents

- `web/stories/07-contribution.mdx` — Storybook companion.
- `web/docs/agentic-coding.md` — MCP-as-dev-time-only rule.
- `web/docs/theming.md` — theme provider + flash-free first paint.
- `web/docs/accessibility.md` — keyboard contract, ARIA, RTL.
- `web/docs/browser-support.md` — supported matrix and modern CSS / JS
  allowances.
- `web/docs/performance-budget.md` — bundle target + Core Web Vitals.
- `.specify/memory/constitution.md` — governing document.
- `specs/001-brand-system-and-design-foundation/spec.md` — the
  authoritative specification.
