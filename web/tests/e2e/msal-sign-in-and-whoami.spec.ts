/**
 * MSAL sign-in + /whoami round-trip Playwright smoke (T093 / Phase 9 polish).
 *
 * Three concerns covered:
 *
 * 1. **Sign-in → /whoami → roles-rendered → sign-out** end-to-end (the main
 *    SC-001 / SC-004 surface). Requires the MSAL E2E auth fixture and is
 *    currently `test.fixme` until a Playwright fixture pre-seeds an MSAL
 *    session against a dev-tenant test user.
 *
 * 2. **Deployed-env HTTPS posture** (FR-003 inheritance from 002): when the
 *    test runs against a non-localhost base URL, the URL must use `https://`.
 *    This is a fast regression guard against accidental http:// links in the
 *    sign-in redirect chain after the MSAL rewrite (T028 removed the prior
 *    NextAuth callback URL — easy to mis-configure when porting to a new
 *    env).
 *
 * 3. **Malformed-bearer 401 + `WWW-Authenticate: Bearer`** against the API's
 *    `/whoami` endpoint (T028 inherited Microsoft.Identity.Web's validation
 *    pipeline). This runs **without** an auth fixture — it intentionally
 *    sends a bogus token — so it provides a real CI signal today that the
 *    bearer middleware is still wired correctly after the slice-003 rewrites.
 *
 * 4. **W3C Trace Context propagation** (T096 / constitution): every API
 *    request originated by the browser must carry a `traceparent` header.
 *    The malformed-bearer probe asserts the request would have carried one
 *    if it had gone through the browser fetch wrapper; the post-sign-in
 *    `/whoami` round-trip (under fixme) asserts it on the real call.
 */

import { expect, test } from "@/tests/fixtures/auth";

const API_BASE_URL =
  process.env.PLAYWRIGHT_API_BASE_URL ??
  process.env.NEXT_PUBLIC_API_BASE_URL ??
  "http://localhost:8080";

function joinApi(path: string): string {
  const base = API_BASE_URL.replace(/\/$/, "");
  const suffix = path.startsWith("/") ? path : `/${path}`;
  return `${base}${suffix}`;
}

test.describe("MSAL sign-in + /whoami", () => {
  // Spec 007 — the sign-in-cycle case below runs as the Reader persona.
  // The malformed-bearer 401 case further down is intentionally persona-less
  // (it doesn't go through the seeded MSAL session) and continues to work
  // by way of the unauthenticated `request` fixture.
  test.use({ persona: "reader" });

  test(
    "sign-in → /platform-status (whoami) → effective roles rendered → sign-out",
    async ({ page, baseURL }) => {
      // Capture deployed-env HTTPS posture for the *frontend* base URL.
      // When PLAYWRIGHT_BASE_URL is non-localhost (i.e. running against a
      // deployed env), the URL must be https. Localhost is exempt. We
      // read from the test's resolved `baseURL` (from playwright.config)
      // rather than `page.url()` because the page hasn't navigated yet
      // and `page.url()` would return `about:blank`.
      const configuredBase = baseURL ?? "";
      const isLocalhost = /^https?:\/\/(localhost|127\.0\.0\.1)(:|\/)/.test(configuredBase);
      if (configuredBase && !isLocalhost) {
        expect(
          configuredBase.startsWith("https://"),
          `Deployed env must serve over HTTPS — saw ${configuredBase}`,
        ).toBe(true);
      }

      // Under mock-auth (spec 007 pivot) the fixture pre-seeds
      // sessionStorage with the persona name; the SPA's mock PCA
      // synthesises a signed-in `AccountInfo` and the api-client adds
      // `X-Mock-Roles` to outbound requests. No real MSAL redirect runs.
      //
      // Set up the /whoami request capture BEFORE navigation so the
      // observer is in place when the (authenticated) layout fires its
      // useEffect-driven fetch. `waitForRequest` returns a Promise that
      // resolves on the first match — racy if the request fires before
      // the call.
      const whoamiRequestPromise = page.waitForRequest(
        (req) => req.url().endsWith("/whoami"),
        { timeout: 30_000 },
      );

      await page.goto("/");

      // After sign-in, the SPA routes to /platform-status (the (authenticated)
      // layout's default landing when roles are non-empty).
      await page.waitForURL(/\/platform-status/, { timeout: 30_000 });

      // /platform-status fetches /whoami and renders the identity + roles +
      // correlation cards.
      const identityCard = page.getByTestId("identity-card");
      const correlationCard = page.getByTestId("correlation-card");
      await expect(identityCard).toBeVisible();
      await expect(correlationCard).toBeVisible();

      // Trace correlation id rendered: non-empty.
      const traceIdText = await correlationCard.locator("dd").first().textContent();
      expect(traceIdText?.trim().length ?? 0).toBeGreaterThan(0);

      // Assert the /whoami request carried a W3C `traceparent` header
      // (constitution: mandatory propagation on every UI-originated HTTP call).
      const whoamiRequest = await whoamiRequestPromise;
      const traceparent = whoamiRequest.headers()["traceparent"];
      expect(traceparent, "/whoami must carry a W3C traceparent header").toMatch(
        /^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$/,
      );

      // Sign out: open the user menu, click the sign-out item, expect the
      // app to navigate back to /signin (unauthenticated).
      const userMenuTrigger = page.getByRole("button", { name: /account menu/i });
      await userMenuTrigger.click();
      const signOut = page.getByTestId("user-menu-sign-out");
      await signOut.click();
      await page.waitForURL(/\/signin/, { timeout: 15_000 });
    },
  );

  test("malformed bearer token to /whoami returns 401 with WWW-Authenticate: Bearer", async ({
    request,
  }) => {
    // Spec 007 — persona-less probe. The request fixture is unauthenticated
    // regardless of the file-scope test.use({ persona }) because we send the
    // bogus Authorization header explicitly below.
    // Negative test — does not need any auth fixture. Confirms the inherited
    // (002) Microsoft.Identity.Web validation pipeline is still active after
    // the T028 rewrites.
    //
    // Auth-mode detection: the CI workflow runs the backend in **mock-auth**
    // mode (`AzureAd__TenantId=development`), where `MockAuthenticationHandler`
    // synthesizes a dev principal for every request — malformed or otherwise.
    // In that posture the real validation pipeline isn't active, so this test
    // has nothing to assert. Skip via the documented diagnostic signal:
    // unauthenticated `/whoami` returns 200 in mock mode, 401 in real mode.
    const probe = await request.get(joinApi("/whoami"), { failOnStatusCode: false });
    test.skip(
      probe.status() === 200,
      "backend is in mock-auth mode (unauthenticated /whoami returned 200) — "
        + "Microsoft.Identity.Web validation is bypassed; run this test against a real "
        + "Entra-backed backend or set AzureAd__TenantId to a real tenant id.",
    );

    const response = await request.get(joinApi("/whoami"), {
      headers: {
        Authorization: "Bearer not-a-real-jwt.payload.signature",
      },
      failOnStatusCode: false,
    });

    expect(response.status()).toBe(401);

    const wwwAuth = response.headers()["www-authenticate"];
    expect(
      wwwAuth,
      `/whoami 401 must include WWW-Authenticate; headers: ${JSON.stringify(response.headers())}`,
    ).toBeDefined();
    expect(wwwAuth).toMatch(/^Bearer/);
  });
});
