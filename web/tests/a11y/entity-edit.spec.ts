/**
 * Spec 009 / T101 / US4. Accessibility audit for the entity edit surface
 * (`/registry/{entityType}/{pe_*}/edit`) including the
 * `<ServiceAssociationEditor>` dialog.
 *
 * Status: `test.fixme` — same caveat as the other Spec 009 E2E tests.
 * Sign-in flows through real Entra; an automated walkthrough needs either
 * a dev-tenant test user with a stored MSAL session or the synthetic-
 * account Playwright fixture that lands with the Phase 9 polish work.
 *
 * Component-level a11y for the form and dialog is covered today via the
 * Vitest+axe matchers in the component test setup. The shadcn primitives
 * (Dialog, Button, Input, Label, Select) ship with documented WCAG 2.2 AA
 * compliance.
 *
 * Once the persona fixture lands, this file should:
 *   1. Seed a published entity (Queue or Topic) with at least one Owner-role
 *      association so the caller can pass R-15.
 *   2. Sign in as a `BusTerminal.Admin` (or Owner-of-service) persona.
 *   3. Navigate to `/registry/Queue/{seededId}/edit`, run axe.
 *   4. Open the `<ServiceAssociationEditor>` dialog, run axe again with the
 *      dialog mounted (covers focus-trap + ARIA semantics on the modal).
 */

import { test } from "@playwright/test";

test.describe("Entity edit a11y", () => {
  test.fixme("no WCAG 2.2 AA violations on the published-entity edit form", async ({ page }) => {
    await page.goto("/platform-status");
  });

  test.fixme(
    "no WCAG 2.2 AA violations on the service association editor dialog",
    async ({ page }) => {
      await page.goto("/platform-status");
    },
  );
});
