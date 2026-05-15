# Favicon Set — Placeholder Pending

During the foundation phase, favicons are produced dynamically by
[`app/icon.tsx`](../../app/icon.tsx) and
[`app/apple-icon.tsx`](../../app/apple-icon.tsx) using `ImageResponse`.
Browsers and OS launchers consume the runtime-generated PNGs at
`/icon` and `/apple-icon` per Next.js App Router conventions.

The final exported favicon set (16, 32, 48, 64, 128, 180, 192, 256, 512 px)
is committed to this directory in **T142** as part of the brand-asset
finalization phase — at which point the metadata wiring in
[`app/layout.tsx`](../../app/layout.tsx) is switched over to consume the
static files.

This README is intentionally kept once the static set lands so the
foundation's audit (`audit:svg-hygiene` / `audit:review-records`) has
documentation of the migration moment.
