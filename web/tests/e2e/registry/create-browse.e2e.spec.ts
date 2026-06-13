/**
 * Spec 006 / T070 [US1] [TEST]. Playwright E2E covering the quickstart §5
 * golden path: create one of each entity type, browse the explorer, open the
 * detail page, verify each entity is reachable.
 *
 * Requires a running backend (mock-auth dev mode is fine).
 *
 * Status: `test.fixme` pending the MSAL E2E auth fixture promised by T093
 * (Phase 9 polish, spec 003). The page sits behind `AuthGuard`, which
 * blocks on MSAL until an authenticated session is present — without the
 * fixture there is no way to seed one in CI. Same posture as
 * `role-aware-affordances`, `no-access-experience`, `platform-status`.
 */

import { test, expect } from "@/tests/fixtures/auth";

test.describe("registry — create + browse", () => {
  // Spec 007 — namespace creation is a `MutateDomain` operation per the
  // role-permission matrix; `operator` is the minimal persona that
  // authorises it.
  test.use({ persona: "operator" });

  test("creates a namespace and reaches the detail page", async ({ page }) => {
    await page.goto("/registry");
    // The registry page renders two headings whose text matches /Service Bus Registry/i
    // (the welcome H1 and the panel H2). Pin the panel H2 explicitly via exact match.
    await page
      .getByRole("heading", { name: "Service Bus Registry", exact: true })
      .waitFor();

    // Default to a happy path: click "New namespace".
    await page.getByRole("link", { name: /Register a namespace/i }).click();
    await expect(page).toHaveURL(/\/registry\/new\/Namespace/);

    const uniqueName = `orders-${Date.now().toString(36)}`;
    // Scope to the form — the global header also exposes an "Environment"
    // switcher, so an unscoped getByLabel(/Environment/) matches both.
    const form = page.getByTestId("entity-form-shell");
    await form.getByLabel(/Name/, { exact: false }).fill(uniqueName);
    await form.getByLabel(/Environment/, { exact: false }).fill("dev");
    await form.getByRole("button", { name: /^Save$/ }).click();

    // After save, the page navigates to the detail route.
    await expect(page).toHaveURL(/\/registry\/Namespace\//);
    // The name appears in several places on the detail page (heading, tree,
    // audit summary). Pin the heading to keep the assertion strict-mode-safe.
    await expect(page.getByRole("heading", { name: uniqueName, exact: true })).toBeVisible();
  });
});
