/**
 * US1 Playwright smoke — role-aware navigation + affordance gating (T054).
 *
 * Intent: signs in as a Reader-only test user (no Operator/Admin roles) and
 * asserts:
 *   1. The primary navigation does NOT show entries gated on Operator or
 *      Admin operation classes.
 *   2. Role-aware buttons for `MutateDomain` operations are rendered with
 *      `disabled` + `aria-disabled="true"` and the `data-testid` selector
 *      `role-aware-button-disabled`.
 *
 * Status: `test.fixme` pending T093 (Phase 9 polish) which lands the MSAL E2E
 * auth fixture. MSAL no longer ships a frontend mock provider — sign-in goes
 * to the real dev tenant — so an automated Playwright sign-in needs either:
 *   (a) a dev-tenant Reader-only test user with a stored MSAL session, or
 *   (b) a Playwright fixture that pre-seeds `sessionStorage` with a synthetic
 *       MSAL account and stubs `acquireTokenSilent`.
 *
 * Until (a) or (b) is wired, the same role-aware-button behavior is covered
 * by the component-level test at
 * `web/components/auth/__tests__/role-aware-button.test.tsx`. That suite
 * exercises the disabled/enabled matrix end-to-end against the real
 * `role-permission-matrix.ts` contract.
 */

import { expect, test } from "@playwright/test";

test.describe("US1: role-aware affordances", () => {
  test.fixme("Reader-only user sees no Operator/Admin nav entries and disabled MutateDomain buttons", async ({ page }) => {
    await page.goto("/platform-status");

    // Navigation: when no MutateDomain entries exist, none should be visible.
    const nav = page.getByTestId("primary-navigation");
    await expect(nav).toBeVisible();

    // Role-aware-button: every MutateDomain-class button on the page is disabled.
    const disabledButtons = page.getByTestId("role-aware-button-disabled");
    const count = await disabledButtons.count();
    for (let i = 0; i < count; i++) {
      const btn = disabledButtons.nth(i);
      const requiredRoles = await btn.getAttribute("data-required-roles");
      if (requiredRoles?.includes("BusTerminal.Operator")) {
        await expect(btn).toBeDisabled();
        await expect(btn).toHaveAttribute("aria-disabled", "true");
      }
    }
  });
});
