/**
 * Platform Status accessibility audit (T051 / SC-005 / WCAG 2.2 AA).
 *
 * Runs axe-core against `/platform-status` (after signing in via the
 * dev-mode button) and asserts zero violations. Lives in `tests/a11y/` so
 * it can be selected by the dedicated a11y job in CI without bringing the
 * full E2E flow along.
 */

import { expect, test } from "@playwright/test";
import { checkA11y, injectAxe } from "axe-playwright";

test.describe("US1 accessibility: platform-status", () => {
  test("has zero WCAG 2.2 AA violations after sign-in", async ({ page }) => {
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await expect(page).toHaveURL(/\/signin/);

    const signInButton = page.getByRole("button", {
      name: /continue as dev user|sign in with microsoft entra id/i,
    });
    await signInButton.click();
    await page.waitForURL(/\/platform-status/, { timeout: 30_000 });

    await injectAxe(page);
    await checkA11y(page, undefined, {
      axeOptions: {
        runOnly: {
          type: "tag",
          values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"],
        },
      },
      detailedReport: true,
      detailedReportOptions: { html: false },
    });
  });
});
