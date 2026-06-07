/**
 * Spec 006 / T070 [US1] [TEST]. Playwright E2E covering the quickstart §5
 * golden path: create one of each entity type, browse the explorer, open the
 * detail page, verify each entity is reachable.
 *
 * Requires a running backend (mock-auth dev mode is fine).
 *
 * Status: `test.fixme` pending the MSAL E2E auth fixture promised by T093
 * (Phase 9 polish, spec 003). The page sits behind `AuthGuard`, which
 * blocks on MSAL until an authenticated session is present — without the
 * fixture there is no way to seed one in CI. Same posture as
 * `role-aware-affordances`, `no-access-experience`, `platform-status`.
 */

import { test, expect } from "@playwright/test";

test.describe("registry — create + browse", () => {
  test.fixme("creates a namespace and reaches the detail page", async ({ page }) => {
    await page.goto("/registry");
    await page.getByRole("heading", { name: /Service Bus Registry/i }).waitFor();

    // Default to a happy path: click "New namespace".
    await page.getByRole("link", { name: /Register a namespace/i }).click();
    await expect(page).toHaveURL(/\/registry\/new\/Namespace/);

    const uniqueName = `orders-${Date.now().toString(36)}`;
    await page.getByLabel(/Name/, { exact: false }).fill(uniqueName);
    await page.getByLabel(/Environment/, { exact: false }).fill("dev");
    await page.getByRole("button", { name: /^Save$/ }).click();

    // After save, the page navigates to the detail route.
    await expect(page).toHaveURL(/\/registry\/Namespace\//);
    await expect(page.getByText(uniqueName)).toBeVisible();
  });
});
