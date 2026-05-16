# BusTerminal Brand Pipeline

This directory is the **source-of-truth root** for every brand asset published
by the foundation. See [Research R3](../../specs/001-brand-system-and-design-foundation/research.md#r3-ai-assisted-brand-asset-production-and-required-human-review-fr-002a--fr-002b)
for the full five-stage pipeline and the originality / licensing review gate.

## Subdirectories

| Path | Contents |
|---|---|
| [`wordmark/`](./wordmark) | Full wordmark — dark + light variants, README, placeholder flag, future REVIEW.md. |
| [`glyph/`](./glyph) | Compact glyph mark — dark + light variants, README, placeholder flag, future REVIEW.md. |

## Audit surfaces

| Audit | Enforces |
|---|---|
| `pnpm audit:svg-hygiene` | Every SVG under `web/brand/**` and `web/public/brand/**` is plain (no `<image>`, no base64 data URIs). |
| `pnpm audit:review-records` | Every AI-assisted asset has a signed `REVIEW.md` before commit (FR-002b). |

## Current state

The wordmark and glyph are **placeholders** — see each subdirectory's README.
The final assets land in T141 / T142 with the required `REVIEW.md` artifacts.
