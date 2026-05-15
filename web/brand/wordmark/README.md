# BusTerminal Wordmark — Placeholder

This directory holds a **placeholder** wordmark used during foundation
development so primitive work can proceed without churning when the final
asset lands. The final asset replaces these files atomically in **T141**
(see `specs/001-brand-system-and-design-foundation/tasks.md` Phase 8).

## Contents

| File | Purpose |
|---|---|
| `wordmark-dark.svg` | Hand-authored placeholder; renders on dark surfaces. |
| `wordmark-light.svg` | Hand-authored placeholder; renders on light surfaces. |
| `placeholder.flag` | Marker that audits use to detect the placeholder is still in tree. |
| `REVIEW.md` (added in T141) | Originality + licensing review per [`contracts/brand-asset-review.md`](../../../specs/001-brand-system-and-design-foundation/contracts/brand-asset-review.md). |

## Rules

- **Plain SVG only** — no `<image>` tags, no base64 data URIs, no embedded
  raster references. Enforced by `pnpm audit:svg-hygiene` (FR-002a / SC-017).
- **No AI-generated content** in the placeholder. AI tooling is reserved for
  the production pipeline stages 1–3 documented in [Research R3](../../../specs/001-brand-system-and-design-foundation/research.md#r3-ai-assisted-brand-asset-production-and-required-human-review-fr-002a--fr-002b).
- **Drop-in replacement contract** — the final asset MUST keep the same
  filenames so consumers (`app/icon.tsx`, `app/opengraph-image.tsx`,
  `stories/01-brand.mdx`) do not change.

## Authoring history

The placeholder was authored by hand in `/specs/001-brand-system-and-design-foundation`
implementation phase 2; it has no upstream source and exists solely to make
the foundation buildable and visually coherent before the final asset is
ready.
