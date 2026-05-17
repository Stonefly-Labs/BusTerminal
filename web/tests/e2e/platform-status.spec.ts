/**
 * Platform Status end-to-end smoke (T050 / SC-001).
 *
 * Exercises the US1 happy path:
 *   1. Land on `/` and observe the redirect to `/signin`.
 *   2. Click the dev-mode sign-in button.
 *   3. Land on `/platform-status` with the dev user's display name visible.
 *   4. See a correlation card with a non-empty trace ID.
 *
 * The test depends on the mock-auth fallback (`AZURE_AD_TENANT_ID=development`)
 * being active in the running dev server. CI configures these env vars.
 */

import { expect, test } from "@playwright/test";

test.describe("US1: platform-status round-trip", () => {
  test("root redirects to signin, dev sign-in lands on platform-status", async ({ page }) => {
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
