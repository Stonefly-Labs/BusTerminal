/**
 * Spec 009 / T041 / US1. Accessibility audit for the namespace overview
 * page with the discovery affordances (Discover button + status panel)
 * present.
 *
 * Status: `test.fixme` — depends on the same MSAL persona fixture as the
 * E2E discovery flow (T039). Once that lands, this test signs in as a
 * NamespaceAdministrator, lands on a seeded namespace's overview, and runs
 * axe against the surface.
 *
 * Component-level a11y is covered today via `vitest-axe` matchers wired in
 * the test setup.
 */

import { test } from "@playwright/test";

test.describe("Namespace overview a11y — discovery surface", () => {
  test.fixme("no WCAG 2.2 AA violations on the namespace overview with discovery panel", async ({ page }) => {
    await page.goto("/platform-status");
  });
});
