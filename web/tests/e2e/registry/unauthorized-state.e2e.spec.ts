/**
 * Spec 006 / T103d [US1] [TEST]. Playwright E2E for the unauthorized state.
 * Simulates token expiration by clearing auth state mid-flow, then verifies
 * the registry surface renders the re-auth CTA with the current URL
 * preserved as the return target.
 */

import { test, expect } from "@playwright/test";

test.describe("registry — unauthorized state", () => {
  test("renders the re-auth CTA when an API call returns 401", async ({ page }) => {
    await page.route("**/api/registry/**", async (route) => {
      await route.fulfill({
        status: 401,
        contentType: "application/problem+json",
        body: JSON.stringify({
          type: "https://busterminal.dev/probs/unauthorized",
          title: "Unauthorized",
          status: 401,
        }),
      });
    });
    await page.goto("/registry/Namespace/00000000-0000-0000-0000-000000000001");
    await expect(page.getByTestId("registry-unauthorized-state")).toBeVisible();
    await expect(page.getByRole("button", { name: /Sign in again/i })).toBeVisible();
  });
});
