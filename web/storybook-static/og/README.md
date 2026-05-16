# Open Graph Image — Placeholder Pending

During foundation phase, the OG preview is produced dynamically by
[`app/opengraph-image.tsx`](../../app/opengraph-image.tsx) using
`ImageResponse`. Social embeds consume the runtime-generated 1200×630 PNG
at `/opengraph-image`.

The final exported `og-image.png` (1200 × 630) lands in this directory in
**T143**.
