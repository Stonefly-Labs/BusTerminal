/**
 * Theme-flash E2E (T105 / FR-006 / SC-004).
 *
 * Anti-FOUC contract enforced here:
 *
 *  1. With the system color scheme emulated as `dark` and persistence
 *     cleared, the first paint MUST already carry the `dark` class on
 *     `<html>` and the dark canvas color. There must be NO white flash
 *     during hydration.
 *
 *  2. With persistence cleared, the resolver MUST honor
 *     `prefers-color-scheme` for the initial paint:
 *     - `prefers-color-scheme: dark` → `<html class="dark">`
 *     - `prefers-color-scheme: light` → `<html>` (no `dark` class)
 *
 *  3. With the `bt:theme` localStorage entry set to `light` and the system
 *     emulated as `dark`, the stored preference WINS the first paint —
 *     the persisted value overrides the OS preference (FR-006).
 *
 * Implementation notes:
 *
 *  - We assert pre-hydration state by reading `<html>` attributes via a
 *    raw `page.evaluate(() => document.documentElement.className)`. The
 *    inline anti-FOUC script in `app/layout.tsx` runs synchronously in
 *    `<head>` with `strategy="beforeInteractive"`, so by the time
 *    `domcontentloaded` resolves the class is already in place.
 *
 *  - We compare the computed `background-color` of `<body>` against the
 *    canvas tokens. Tokens are authored in OKLCH but Playwright reports
 *    computed colors via `getComputedStyle()` as `rgb(...)` strings —
 *    we tolerate both dark and light canvas values by checking the
 *    relative luminance bucket (< 50 → dark, ≥ 200 → light).
 */

import { test, expect, type Page } from "@playwright/test";

const THEME_STORAGE_KEY = "bt:theme";

async function clearThemePersistence(page: Page): Promise<void> {
  await page.addInitScript((key) => {
    try {
      window.localStorage.removeItem(key);
    } catch {
      /* localStorage may be blocked in some browser modes; tests still run */
    }
  }, THEME_STORAGE_KEY);
}

async function seedThemePersistence(page: Page, value: "light" | "dark"): Promise<void> {
  await page.addInitScript(
    ({ key, val }) => {
      try {
        window.localStorage.setItem(key, val);
      } catch {
        /* see above */
      }
    },
    { key: THEME_STORAGE_KEY, val: value },
  );
}

async function getBodyBackgroundLuminance(page: Page): Promise<number> {
  return page.evaluate(() => {
    const bg = window.getComputedStyle(document.body).backgroundColor;
    // Use a 1×1 canvas to let the browser convert ANY CSS color format
    // (rgb/rgba, lab, oklch, hsl, hex, named) to sRGB bytes. Chrome ≥109
    // preserves the source format in getComputedStyle (e.g. `lab(...)` or
    // `oklch(...)`) when tokens are authored in those spaces, so a plain
    // rgba regex no longer suffices.
    const canvas = document.createElement("canvas");
    canvas.width = 1;
    canvas.height = 1;
    const ctx = canvas.getContext("2d");
    if (!ctx) return -1;
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, 1, 1);
    const pixel = ctx.getImageData(0, 0, 1, 1).data;
    const r = pixel[0] ?? 0;
    const g = pixel[1] ?? 0;
    const b = pixel[2] ?? 0;
    const a = pixel[3] ?? 0;
    if (a === 0) return -1;
    return 0.299 * r + 0.587 * g + 0.114 * b;
  });
}

test.describe("theme-flash (T105 / SC-004)", () => {
  test("with system=dark and no persistence, first paint is dark", async ({
    browser,
  }) => {
    // WebKit honors `colorScheme` reliably; emulate on the page level so all
    // three browser projects (chromium / firefox / webkit) behave the same.
    const context = await browser.newContext({ colorScheme: "dark" });
    const darkPage = await context.newPage();
    await clearThemePersistence(darkPage);

    await darkPage.goto("/", { waitUntil: "domcontentloaded" });

    const htmlClass = await darkPage.evaluate(() => document.documentElement.className);
    expect(htmlClass).toContain("dark");

    const luminance = await getBodyBackgroundLuminance(darkPage);
    // Dark canvas token resolves into the < 60 luminance bucket.
    expect(luminance).toBeGreaterThanOrEqual(0);
    expect(luminance).toBeLessThan(60);

    await context.close();
  });

  test("with system=light and no persistence, first paint is light", async ({
    browser,
  }) => {
    const context = await browser.newContext({ colorScheme: "light" });
    const lightPage = await context.newPage();
    await clearThemePersistence(lightPage);

    await lightPage.goto("/", { waitUntil: "domcontentloaded" });

    const htmlClass = await lightPage.evaluate(() => document.documentElement.className);
    expect(htmlClass).not.toContain("dark");

    const luminance = await getBodyBackgroundLuminance(lightPage);
    // Light canvas token resolves into the > 200 luminance bucket.
    expect(luminance).toBeGreaterThan(200);

    await context.close();
  });

  test("persisted preference wins over the OS preference", async ({ browser }) => {
    const context = await browser.newContext({ colorScheme: "dark" });
    const persistedPage = await context.newPage();
    await seedThemePersistence(persistedPage, "light");

    await persistedPage.goto("/", { waitUntil: "domcontentloaded" });

    const htmlClass = await persistedPage.evaluate(
      () => document.documentElement.className,
    );
    expect(htmlClass).not.toContain("dark");

    const luminance = await getBodyBackgroundLuminance(persistedPage);
    expect(luminance).toBeGreaterThan(200);

    await context.close();
  });
});
