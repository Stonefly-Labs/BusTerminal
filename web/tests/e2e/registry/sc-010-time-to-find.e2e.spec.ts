/**
 * Spec 006 / T129b [TEST] / SC-010.
 *
 * Time-to-find assertion: with a populated registry, the operator types a
 * partial name in the global search affordance from an arbitrary page and
 * reaches the entity's detail page in under 30 seconds (wall-clock from first
 * keystroke to detail-page `domcontentloaded`).
 *
 * The measured duration is also emitted via the existing observability
 * adapter as a sanctioned `custom` event named `sc010.time-to-find` so that
 * production runs surface the metric in App Insights.
 *
 * Requires a populated registry. The dev environment seeds one as part of
 * the deploy; locally, run quickstart §5 first.
 *
 * Status: `test.fixme` pending the MSAL E2E auth fixture promised by T093
 * (Phase 9 polish, spec 003) — the page sits behind `AuthGuard`.
 */

import { test, expect, type Page } from "@playwright/test";

const SC010_BUDGET_MS = 30_000;
const TYPEAHEAD_SLACK_MS = 5_000;

async function getFirstSeededName(page: Page): Promise<string | null> {
  // Hit the registry list endpoint to discover at least one entity name we
  // can search for. Falls back to scraping the explorer tree.
  const fromApi = await page.evaluate(async () => {
    try {
      const res = await fetch("/api/registry?top=1");
      if (!res.ok) return null;
      const body = (await res.json()) as { items?: Array<{ name?: string }> };
      const first = body.items?.[0]?.name;
      return typeof first === "string" && first.length >= 3 ? first : null;
    } catch {
      return null;
    }
  });
  if (fromApi) return fromApi;

  // Fallback: any tree-node text content.
  const treeText = await page
    .locator('[data-testid="registry-tree-node"]')
    .first()
    .textContent()
    .catch(() => null);
  if (treeText && treeText.trim().length >= 3) {
    const first = treeText.trim().split(/\s+/)[0];
    return first ?? null;
  }
  return null;
}

test.describe("registry — SC-010 time-to-find", () => {
  test.fixme("operator reaches a known entity from an arbitrary page in under 30s", async ({
    page,
  }) => {
    // Start on an arbitrary authenticated route — not /registry itself — so
    // the path "open search from anywhere → land on detail" is exercised.
    await page.goto("/platform-status", { waitUntil: "domcontentloaded" });

    // The global search trigger lives in the NavigationHeader and is
    // keyboard-shortcut-bound to Cmd/Ctrl+K. Discover a seed name first so
    // we know what to type.
    await page.goto("/registry", { waitUntil: "domcontentloaded" });
    await page.waitForSelector('[data-testid="registry-layout"]');
    const seedName = await getFirstSeededName(page);
    test.skip(
      seedName === null,
      "No entities present in the target environment — populate per quickstart §5 first.",
    );
    const partial = (seedName as string).slice(0, Math.min(4, seedName!.length));

    // Re-anchor on an arbitrary page so the search path is "from anywhere".
    await page.goto("/platform-status", { waitUntil: "domcontentloaded" });

    // Open the global search dialog via the keyboard shortcut.
    await page.keyboard.press(
      process.platform === "darwin" ? "Meta+K" : "Control+K",
    );
    await page.waitForSelector(
      '[data-testid="registry-global-search-dialog"]',
      { state: "visible" },
    );

    // Start the timer right before the first keystroke.
    const t0 = Date.now();
    await page.getByLabel("Search registry").fill(partial);

    // Submit — the dialog navigates to /registry/search?q=…
    await page.getByRole("button", { name: "Search" }).click();
    await page.waitForURL(/\/registry\/search\?q=/, {
      timeout: TYPEAHEAD_SLACK_MS,
    });

    // First result row → click it. We don't assert ranking here — SC-010 is
    // about reachability under the wall-clock budget, not relevance.
    const firstResult = page
      .getByTestId("registry-search-results-table")
      .locator("tbody tr a")
      .first();
    await firstResult.waitFor({ state: "visible", timeout: TYPEAHEAD_SLACK_MS });
    await firstResult.click();

    // Wait for the detail page to commit to `domcontentloaded`.
    await page.waitForURL(/\/registry\/[^/]+\/[^/]+$/, {
      timeout: SC010_BUDGET_MS,
    });
    await page.waitForLoadState("domcontentloaded");
    const elapsedMs = Date.now() - t0;

    // Hard assertion against the SC-010 budget.
    expect(elapsedMs).toBeLessThan(SC010_BUDGET_MS);

    // Emit the metric via `performance.measure` so it surfaces in the
    // Application Insights browser SDK's PerformanceObserver pipeline (which
    // forwards User Timing entries to the connected resource). Two outputs:
    //
    //   1) A PerformanceMeasure with the SC-010 name + duration so the
    //      observability adapter picks it up automatically.
    //   2) A console log so CI captures the value even when the no-op
    //      observability adapter is active.
    //
    // We avoid statically importing `@/lib/observability` here because this
    // file runs as a Playwright spec under tsc's test-only typing scope and
    // the project's path alias does not resolve from the test runtime.
    await page.evaluate((durationMs) => {
      try {
        const t0 = performance.now() - durationMs;
        performance.measure("sc010.time-to-find", { start: t0, duration: durationMs });
      } catch {
        // PerformanceMeasure with a start/duration descriptor is supported
        // in every browser we target; swallow on the unlikely failure path.
      }
      // eslint-disable-next-line no-console
      console.info(`[sc010.time-to-find] duration_ms=${Math.round(durationMs)}`);
    }, elapsedMs);
  });
});
