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

import { expect, test } from "@/tests/fixtures/auth";

test.describe("US1: platform-status round-trip", () => {
  // Spec 007 — Reader persona pre-seeds an authenticated MSAL session,
  // so this test exercises the "already signed-in → /platform-status"
  // happy path. The original NextAuth-era body still asserted a redirect
  // to `/signin` and a "Continue as Dev User" button — both pre-MSAL
  // artifacts. Those assertions are no longer applicable; the body below
  // documents the up-to-date Reader landing-page expectations.
  test.use({ persona: "reader" });

  test("seeded Reader lands on platform-status with identity + correlation cards", async ({ page }) => {
    // With a seeded MSAL session, AuthGuard admits the user immediately
    // and the (authenticated) layout routes a role-bearing principal to
    // /home (the default landing as of the home-dashboard PR; pre-PR
    // this was /platform-status). This test exercises the platform-status
    // round-trip specifically, so navigate there explicitly.
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await page.waitForURL(/\/home/, { timeout: 30_000 });
    await page.goto("/platform-status");
    await page.waitForURL(/\/platform-status/, { timeout: 30_000 });

    const identityCard = page.getByTestId("identity-card");
    await expect(identityCard).toBeVisible();

    const correlationCard = page.getByTestId("correlation-card");
    await expect(correlationCard).toBeVisible();
    const traceIdText = await correlationCard.locator("dd").first().textContent();
    expect(traceIdText?.trim().length ?? 0).toBeGreaterThan(0);
  });
});
