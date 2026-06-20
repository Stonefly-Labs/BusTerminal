/**
 * Spec 009 / T084 / US3. Accessibility audit for the new history pages
 * (`/namespaces/{id}/discovery-runs` list + `/{runId}` detail).
 *
 * Status: `test.fixme` — same caveat as the other discovery E2E tests
 * (`discovery-flow.spec.ts`, `namespace-overview-discovery.spec.ts`).
 * Sign-in flows through real Entra, so an automated walkthrough requires
 * either (a) a dev-tenant test user with a stored MSAL session, or (b)
 * the synthetic-account Playwright fixture that lands with the Phase 9
 * polish work.
 *
 * Component-level a11y for the table and detail panels is covered today
 * via vitest-axe matchers in the component test setup, and the existing
 * shadcn primitives (Card, Badge, Table, Alert) ship with documented WCAG
 * 2.2 AA compliance.
 *
 * Once the persona fixture lands, this file should:
 *   1. Seed at least one Succeeded run and one Failed run for a namespace.
 *   2. Sign in as `BusTerminal.Reader` (any authenticated user — read-only).
 *   3. Navigate to `/namespaces/{seededNs}/discovery-runs/`, run axe.
 *   4. Click into the Failed run, run axe again on the detail surface.
 */

import { test } from "@playwright/test";

test.describe("Discovery history a11y", () => {
  test.fixme("no WCAG 2.2 AA violations on the discovery history list", async ({ page }) => {
    await page.goto("/platform-status");
  });

  test.fixme("no WCAG 2.2 AA violations on the discovery run detail", async ({ page }) => {
    await page.goto("/platform-status");
  });
});
