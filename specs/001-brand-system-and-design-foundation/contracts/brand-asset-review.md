# Brand Asset Review Contract (Template)

**Spec references**:

- [FR-002](../spec.md) — working logo system delivered as SVG-first dark/light variants
- [FR-002a](../spec.md) — AI-assisted brand assets MAY be used; MUST be manually refined, hand-cleaned, plain SVG (no opaque rasters, no embedded base64)
- [FR-002b](../spec.md) — a licensing/originality review MUST be **recorded** for any AI-assisted asset **before** it is committed
- [SC-017](../spec.md) — all committed brand assets render cleanly 16 px – 512 px, ship dark/light variants, and carry a recorded human originality/licensing review for any AI-assisted asset
- [Research R3](../research.md#r3-ai-assisted-brand-asset-production-and-required-human-review-fr-002a--fr-002b) — five-stage pipeline

This file is the **template** that every committed `REVIEW.md` MUST instantiate. One `REVIEW.md` lives adjacent to each AI-assisted brand asset's SVG sources (e.g., `web/brand/wordmark/REVIEW.md`, `web/brand/glyph/REVIEW.md`).

A `REVIEW.md` whose checklist is incomplete or unsigned is treated as a **commit blocker** by the `pnpm audit:review-records` gate.

---

## Required `REVIEW.md` Structure

```markdown
# Brand Asset Originality & Licensing Review

**Asset name**: <e.g., wordmark | glyph | favicon | social-preview | repo-banner>
**Asset paths**:
  - Source SVG (dark): <repo-relative path>
  - Source SVG (light): <repo-relative path>
  - Exported variants: <repo-relative directory or list>

**Stage 1 tool**: <e.g., Recraft v3 | DALL-E 3 | Figma AI | Midjourney v6 | manual>
**Stage 1 prompt(s)**: <verbatim copy of the prompt(s) used, or "n/a — manual" if not AI-assisted>
**Stage 2 vector tool**: <e.g., Figma | Affinity Designer | Inkscape>
**Reviewer**: <human contributor GitHub handle>
**Review date**: <YYYY-MM-DD>

## Checks performed

- [ ] **Trademark search** — USPTO TESS, WIPO Global Brand Database, and trademarkia.com searched for the wordmark text "BusTerminal", the glyph silhouette, and any combined-mark variation. Result: <summary, with screenshots or links where relevant>
- [ ] **Reverse-image search** — TinEye + Google Lens against the candidate raster and final SVG render at 512 px. Result: <summary>
- [ ] **Visual inspection for inadvertent reproduction** — confirmed the asset does not visually replicate any well-known existing logo, icon family, or AI-generated likeness of a person, organization, or product. Result: <summary>
- [ ] **License confirmation** — confirmed the asset is redistributable under the project's open-source license (<license name>). Result: <summary>
- [ ] **SVG hygiene** — opened the committed SVG and confirmed: plain `<path>` / `<g>` / `<rect>` elements only; no `<image>` tags; no base64 `data:` URIs; no embedded raster references. Result: <summary>

## Render verification

- [ ] Renders cleanly at 16 px
- [ ] Renders cleanly at 32 px
- [ ] Renders cleanly at 64 px
- [ ] Renders cleanly at 128 px
- [ ] Renders cleanly at 256 px
- [ ] Renders cleanly at 512 px

## Decision

- [ ] **Approved for commit**
- [ ] **Rework required** (notes below)

## Notes

<free-form reviewer notes>

## Signature

By committing this `REVIEW.md`, the reviewer attests that the checks above were performed in good faith and that, to their knowledge, the asset is original, redistributable, and free of third-party trademark conflicts.

**Reviewer signature**: <GitHub handle> · <YYYY-MM-DD>
```

---

## Audit-Gate Behavior (`pnpm audit:review-records`)

The audit script performs the following checks on every `REVIEW.md` under `web/brand/`:

1. **File presence** — every committed SVG source under `web/brand/**/*.svg` whose neighboring directory does not contain a `placeholder.flag` file MUST have a `REVIEW.md` in the same directory.
2. **Required headings present** — the headings `## Checks performed`, `## Render verification`, `## Decision`, and `## Signature` MUST all exist.
3. **All "Checks performed" items checked** — every `- [ ]` checkbox under `## Checks performed` MUST be `- [x]` (i.e., none left unchecked).
4. **All "Render verification" items checked** — every `- [ ]` under `## Render verification` MUST be `- [x]`.
5. **Decision is "Approved for commit"** — exactly one of the two decision items is `- [x]` and it is the "Approved for commit" line.
6. **Signature line present** — the `**Reviewer signature**: <GitHub handle> · <YYYY-MM-DD>` line exists and the handle is not the literal placeholder `<GitHub handle>`.

A failure of any check fails the gate. The gate runs in CI on every PR that touches `web/brand/**` and on the foundation handoff branch.

---

## Placeholder Mark Exemption

The Phase A – E placeholder mark is created by a human contributor (no AI tools used), is committed as plain SVG, and is the only asset under `web/brand/` accompanied by a `placeholder.flag` empty file. The `placeholder.flag` exempts it from the `REVIEW.md` requirement.

When the placeholder is replaced in Phase F:

1. The final assets land in the same paths.
2. Each AI-assisted final asset's directory adds a complete `REVIEW.md` per the template above.
3. The `placeholder.flag` file is deleted.
4. The audit runs cleanly.

This handoff is the bright line at which the foundation is considered "brand-complete" for SC-017.
