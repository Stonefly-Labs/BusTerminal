/**
 * SC-008 Playwright smoke — no-platform-role no-access landing (T092).
 *
 * Intent: a user who is authenticated but holds zero BusTerminal roles must
 * land on `/no-access` within **2 seconds** of completing MSAL sign-in. The
 * page must render the `[data-testid="no-access-page"]` Card so the user can
 * see their identity and the request-access instructions without inspecting
 * their token (`web/app/(auth)/no-access/page.tsx`).
 *
 * Timing assertion (per T092 spec): capture a timestamp at the post-MSAL-
 * redirect navigation event, await the no-access page selector with a 2000 ms
 * Playwright timeout, then independently assert `(Date.now() - postRedirectTs)
 * <= 2000`. Both assertions must pass — the Playwright timeout guards the
 * `toBeVisible` poll; the wall-clock assertion guards the full sign-in→landed
 * window even if the page commit beat the visibility poll by accident.
 *
 * Status: `test.fixme` pending the MSAL E2E auth fixture promised by T093 in
 * Phase 9 polish. MSAL no longer ships a frontend mock provider — sign-in goes
 * to the real dev tenant — so an automated Playwright sign-in needs either:
 *   (a) a dev-tenant no-role test user with a stored MSAL session, or
 *   (b) a Playwright fixture that pre-seeds `sessionStorage` with a synthetic
 *       MSAL account that returns an empty `roles` claim.
 *
 * Until (a) or (b) is wired, SC-008 is exercised manually per
 * `specs/003-auth-and-identity/quickstart.md` § Part D. The no-access page's
 * data-testid contract is also covered by the layout-redirect logic in
 * `web/app/(authenticated)/layout.tsx` which has component-level coverage.
 */

import { expect, test } from "@playwright/test";

const NO_ACCESS_BUDGET_MS = 2_000;

test.describe("SC-008: no-role users land on /no-access within 2s", () => {
  test.fixme("authenticated zero-role user sees the no-access page within 2s", async ({ page }) => {
    // Pre-condition: the test user is a real Entra account in the dev tenant
    // with NO BusTerminal app-role assignment. Sign-in flow is wired by the
    // MSAL fixture (T093). After sign-in, MSAL redirects back to the SPA;
    // the (authenticated) layout reads `effectiveRoles` from `/whoami` and
    // pushes the browser to `/no-access` when the set is empty.

    // Capture the timestamp at the moment we observe the post-MSAL redirect
    // commit, *before* awaiting the no-access page's rendered DOM. This is
    // the start of the SC-008 budget.
    const postRedirectTs = await new Promise<number>((resolve) => {
      const off = page.on("framenavigated", (frame) => {
        if (frame === page.mainFrame() && /\/no-access(\/|$|\?)/.test(frame.url())) {
          resolve(Date.now());
          page.off("framenavigated", off as never);
        }
      });
      void page.goto("/");
    });

    // Visibility poll bounded by the 2 s budget. Fails fast if the no-access
    // Card never paints.
    await expect(page.getByTestId("no-access-page")).toBeVisible({
      timeout: NO_ACCESS_BUDGET_MS,
    });

    // Wall-clock guard against any pathological case where `toBeVisible`
    // returned within its timeout but the actual elapsed time still exceeded
    // the SC-008 budget (e.g., the visibility poll happened to fire at the
    // last possible tick).
    const elapsedMs = Date.now() - postRedirectTs;
    expect(
      elapsedMs,
      `SC-008 budget exceeded: no-access rendered in ${elapsedMs} ms (budget ${NO_ACCESS_BUDGET_MS} ms)`,
    ).toBeLessThanOrEqual(NO_ACCESS_BUDGET_MS);

    // Sanity-check the page surfaces identity for the request-access flow.
    await expect(page.getByTestId("no-access-display-name")).toBeVisible();
    await expect(page.getByTestId("no-access-oid")).toBeVisible();
    await expect(page.getByTestId("no-access-sign-out")).toBeVisible();
  });
});
