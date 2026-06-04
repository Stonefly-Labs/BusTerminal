/**
 * Spec 006 / T073 [US1] [TEST]. axe-playwright a11y test for the explorer
 * route on both dark and light themes.
 */

import { test } from "@playwright/test";
import { checkA11y, injectAxe } from "axe-playwright";

const THEMES = ["light", "dark"] as const;

for (const theme of THEMES) {
  test(`registry explorer is axe-clean on ${theme} theme`, async ({ page }) => {
    await page.addInitScript((t) => {
      window.localStorage.setItem("bt:theme", t);
    }, theme);
    await page.goto("/registry");
    await page.waitForSelector('[data-testid="registry-layout"]');
    await injectAxe(page);
    await checkA11y(page, '[data-testid="registry-layout"]', {
      detailedReport: true,
      detailedReportOptions: { html: true },
    });
  });
}
