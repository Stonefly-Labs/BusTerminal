/**
 * Spec 006 / T120 [US3] [TEST]. axe-playwright a11y test for the registry
 * detail page (covering the relationships + audit panels) on both dark and
 * light themes.
 *
 * Requires a populated registry — the spec relies on at least one entity
 * existing so the detail route renders. The dev environment seeds one as
 * part of the deploy; locally, run quickstart §5 first.
 */

import { test } from "@playwright/test";
import { checkA11y, injectAxe } from "axe-playwright";

const THEMES = ["light", "dark"] as const;

for (const theme of THEMES) {
  test(`registry detail is axe-clean on ${theme} theme`, async ({ page }) => {
    await page.addInitScript((t) => {
      window.localStorage.setItem("bt:theme", t);
    }, theme);

    // Land on the explorer, pick the first namespace tree node, follow its
    // detail link. This keeps the test resilient to whatever ids exist in
    // the target environment.
    await page.goto("/registry");
    await page.waitForSelector('[data-testid="registry-layout"]');

    const firstEntityLink = page.locator('[data-testid="registry-tree-node"] a').first();
    await firstEntityLink.waitFor({ state: "visible" });
    await firstEntityLink.click();

    await page.waitForSelector('[data-testid="registry-detail-shell"]');
    // Wait for the relationships + audit panels to settle (loading → loaded
    // or empty). Either variant is axe-acceptable; we just need the
    // suspense-style loading state to clear so axe sees the final markup.
    await page.waitForFunction(() => {
      const rel = document.querySelector('[data-testid="registry-relationships-panel"]');
      const aud = document.querySelector('[data-testid="registry-audit-panel"]');
      const relVariant = rel?.getAttribute("data-variant");
      const audVariant = aud?.getAttribute("data-variant");
      return (
        relVariant && relVariant !== "loading" && audVariant && audVariant !== "loading"
      );
    });

    await injectAxe(page);
    await checkA11y(page, '[data-testid="registry-detail-shell"]', {
      detailedReport: true,
      detailedReportOptions: { html: true },
    });
  });
}
