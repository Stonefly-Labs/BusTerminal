/**
 * Spec 006 / T108 [US2] [TEST]. axe-playwright a11y test for the registry
 * search route on both dark and light themes.
 */

import { test } from "@playwright/test";
import { checkA11y, injectAxe } from "axe-playwright";

const THEMES = ["light", "dark"] as const;

for (const theme of THEMES) {
  test(`registry search is axe-clean on ${theme} theme`, async ({ page }) => {
    await page.addInitScript((t) => {
      window.localStorage.setItem("bt:theme", t);
    }, theme);
    await page.goto("/registry/search?q=orders");
    await page.waitForSelector('[data-testid="registry-search-page"]');
    await injectAxe(page);
    await checkA11y(page, '[data-testid="registry-search-page"]', {
      detailedReport: true,
      detailedReportOptions: { html: true },
    });
  });
}
