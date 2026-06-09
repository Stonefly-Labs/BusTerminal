import { defineConfig, devices } from "@playwright/test";

/**
 * Playwright configuration for BusTerminal foundation.
 *
 * - Browser matrix: Chromium, Firefox, WebKit (FR-035a / SC-018)
 * - Primary viewport: 13"/mid-range laptop 1366×768 used for the
 *   SC-010 horizontal-overflow assertion and the SC-019 Web Vitals
 *   probe. T145 extends the viewport matrix (mobile / 1366 / 1920 /
 *   3840) for cross-browser smoke.
 * - Auth: handled by `web/tests/fixtures/auth.ts` — per-context
 *   `addInitScript` writes the persona into sessionStorage; the SPA's
 *   mock PCA (active when `NEXT_PUBLIC_AUTH_MODE=mock`) synthesises a
 *   signed-in session. No globalSetup, no IdP round-trip. See spec 007.
 */
const LAPTOP_VIEWPORT = { width: 1366, height: 768 } as const;

export default defineConfig({
  testDir: "./tests",
  testMatch: ["e2e/**/*.spec.ts", "a11y/**/*.spec.ts"],
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  ...(process.env.CI ? { workers: 1 } : {}),
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? "http://localhost:3000",
    trace: "on-first-retry",
    viewport: LAPTOP_VIEWPORT,
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], viewport: LAPTOP_VIEWPORT },
    },
    {
      name: "firefox",
      use: { ...devices["Desktop Firefox"], viewport: LAPTOP_VIEWPORT },
    },
    {
      name: "webkit",
      use: { ...devices["Desktop Safari"], viewport: LAPTOP_VIEWPORT },
    },
  ],
  ...(process.env.PLAYWRIGHT_SKIP_WEB_SERVER
    ? {}
    : {
        webServer: {
          command: "pnpm run dev",
          url: "http://localhost:3000",
          reuseExistingServer: !process.env.CI,
          timeout: 120_000,
        },
      }),
});
