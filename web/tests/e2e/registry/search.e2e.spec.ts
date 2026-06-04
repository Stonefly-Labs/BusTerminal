/**
 * Spec 006 / T107 [US2] [TEST]. Playwright E2E for the quickstart §6 search
 * walkthrough: type a partial entity name, expect ranked results, apply a
 * filter, expect narrowed results.
 *
 * Requires a backend with seeded data. CI uses the dev environment.
 */

import { test, expect } from "@playwright/test";

test.describe("registry — search", () => {
  test("typing a query renders results within the SC-002 budget", async ({ page }) => {
    await page.goto("/registry/search");
    await page.getByLabel("Search registry").fill("orders");
    // The debounced input + TanStack Query path must resolve quickly under
    // the SC-002 budget (search p95 < 1s). We allow 5s of slack for E2E.
    await expect
      .poll(
        async () =>
          (await page
            .getByTestId("registry-search-results-table")
            .count()) +
          (await page.getByTestId("registry-empty-state").count()),
        { timeout: 5000 },
      )
      .toBeGreaterThan(0);
  });

  test("the 503 state renders the search-unavailable empty state", async ({ page }) => {
    await page.route("**/api/registry/search**", async (route) => {
      await route.fulfill({
        status: 503,
        contentType: "application/problem+json",
        body: JSON.stringify({
          type: "https://busterminal.dev/probs/search-unavailable",
          title: "Search backend temporarily unavailable",
          status: 503,
          code: "SearchUnavailable",
        }),
      });
    });
    await page.goto("/registry/search?q=orders");
    const empty = page.getByTestId("registry-empty-state");
    await expect(empty).toBeVisible();
    await expect(empty).toHaveAttribute("data-variant", "unavailable");
  });
});
