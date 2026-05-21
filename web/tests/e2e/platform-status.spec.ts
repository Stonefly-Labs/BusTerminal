/**
 * Platform Status end-to-end smoke (T050 / SC-001).
 *
 * Exercises the US1 happy path:
 *   1. Land on `/` and observe the redirect to `/signin`.
 *   2. Click the dev-mode sign-in button (Credentials provider — no OAuth).
 *   3. Land on `/platform-status` with the dev user's display name visible.
 *   4. See a correlation card with a non-empty trace ID.
 *
 * Requires both servers to be reachable on their default ports:
 *   - frontend (this Playwright webServer) on `:3000`
 *   - backend (.NET, started by the surrounding workflow) on `:8080`,
 *     with mock auth active so `/whoami` returns the synthetic principal.
 */

import { expect, test } from "@playwright/test";

test.describe("US1: platform-status round-trip", () => {
  // Inherited from 002. The "Continue as Dev User" button was provided by
  // NextAuth's Credentials provider, which spec 003 removed (FR-003). MSAL
  // has no no-IDP path, so the assertion against a synthetic dev-user sign-in
  // can no longer run without a real Entra round-trip. Restored by T093 in
  // Phase 9 polish, which lands the MSAL E2E auth fixture.
  test.fixme("root redirects to signin, dev sign-in lands on platform-status", async ({ page }) => {
    await page.goto("/", { waitUntil: "domcontentloaded" });

    await expect(page).toHaveURL(/\/signin/);

    const signInButton = page.getByRole("button", { name: /continue as dev user|sign in with microsoft entra id/i });
    await expect(signInButton).toBeVisible();
    await signInButton.click();

    await page.waitForURL(/\/platform-status/, { timeout: 30_000 });

    const identityCard = page.getByTestId("identity-card");
    await expect(identityCard).toBeVisible();
    await expect(identityCard).toContainText(/dev user/i);

    const correlationCard = page.getByTestId("correlation-card");
    await expect(correlationCard).toBeVisible();
    const traceIdText = await correlationCard.locator("dd").first().textContent();
    expect(traceIdText?.trim().length ?? 0).toBeGreaterThan(0);
  });
});
