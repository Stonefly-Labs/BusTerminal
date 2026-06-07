/**
 * Spec 006 / T072 [US1] [TEST]. Playwright E2E for FR-009 — delete blocked
 * when the entity has children. Creates a namespace + queue then attempts
 * to delete the namespace and verifies the HasChildren modal renders.
 *
 * Status: `test.fixme` pending the MSAL E2E auth fixture promised by T093
 * (Phase 9 polish, spec 003) — the page sits behind `AuthGuard`.
 */

import { test, expect } from "@playwright/test";

test.describe("registry — delete blocked by children", () => {
  test.fixme("renders the HasChildren modal", async ({ page }) => {
    await page.goto("/registry");
    await expect(page.getByText(/Service Bus Registry/i)).toBeVisible();
    // The block-with-children logic is unit-tested at the API level
    // (DeleteEntityEndpointTests.Delete_WithChildren_Returns409_HasChildrenResponse)
    // and at the UI level via the delete-confirmation component.
  });
});
