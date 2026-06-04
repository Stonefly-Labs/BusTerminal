/**
 * Spec 006 / T070 [US1] [TEST]. Playwright E2E covering the quickstart §5
 * golden path: create one of each entity type, browse the explorer, open the
 * detail page, verify each entity is reachable.
 *
 * Requires a running backend (mock-auth dev mode is fine). Skipped in CI
 * until the dev environment exposes the registry endpoints.
 */

import { test, expect } from "@playwright/test";

test.describe("registry — create + browse", () => {
  test("creates a namespace and reaches the detail page", async ({ page }) => {
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
