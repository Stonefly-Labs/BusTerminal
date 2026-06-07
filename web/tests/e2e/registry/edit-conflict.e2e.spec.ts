/**
 * Spec 006 / T071 [US1] [TEST]. Playwright E2E covering the quickstart §8
 * conflict walkthrough: two browser sessions edit the same entity; the
 * second save triggers the conflict modal; the operator chooses Discard or
 * Force overwrite.
 *
 * Requires a running backend. Conflict simulation needs two contexts to
 * race the same ETag, so this test runs `test.describe.serial`.
 *
 * Status: `test.fixme` pending the MSAL E2E auth fixture promised by T093
 * (Phase 9 polish, spec 003) — the page sits behind `AuthGuard`.
 */

import { test, expect } from "@playwright/test";

test.describe.serial("registry — edit + conflict", () => {
  test.fixme("force-overwrite path completes successfully", async ({ page, context }) => {
    await page.goto("/registry");
    // Smoke: page renders the layout.
    await expect(page.getByText(/Service Bus Registry/i)).toBeVisible();

    // The actual conflict-modal flow needs seeded data; treat this as a
    // smoke test until the dev fixture exposes a deterministic editable
    // entity. The conflict modal logic itself is unit-tested in
    // registry-conflict-modal.test.tsx (T069).
    const _ctx = context; // silence eslint
    void _ctx;
  });
});
