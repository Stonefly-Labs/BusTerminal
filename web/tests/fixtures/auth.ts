/**
 * Spec-007 Playwright auth fixture (mock-auth variant).
 *
 * Test authors import `test` and `expect` from this file instead of
 * `@playwright/test`. Setting `test.use({ persona: "reader" })` at file
 * scope (or inside a `test.describe`) causes the test's browser context
 * to start with an `addInitScript` that writes the persona name into
 * `sessionStorage["bt.e2e.persona"]` BEFORE any application code runs.
 *
 * The SPA's mock MSAL instance (`web/lib/auth/msal-mock.ts`) reads that
 * key on every account/token query and synthesises a signed-in
 * `AccountInfo`; the api-client (`web/lib/api-client.ts`) reads the same
 * key to add the `X-Mock-Roles` header to every outbound request; the
 * backend's existing `MockAuthenticationHandler` translates that header
 * into the request principal's role claims.
 *
 * No `storageState`, no `globalSetup`, no real IdP round-trip.
 *
 * See `specs/007-playwright-auth-fixture/contracts/fixture-api.md` for
 * the formal contract and `web/tests/auth/README.md` for the day-to-day
 * usage guide.
 */

/* eslint-disable react-hooks/rules-of-hooks --
 * Playwright's fixture API passes a continuation callback named `use` to
 * each fixture function. The eslint rule's heuristic mistakes that for
 * React's `use()` hook. This rule does not apply to test-fixture code.
 */

import { test as base, expect } from "@playwright/test";

import { E2E_PERSONA_SESSION_KEY, isPersona, PERSONA_CONFIGS, type Persona } from "@/tests/auth/personas";

export interface AuthOptions {
  /**
   * The persona whose synthetic mock-MSAL session should seed this
   * test's context. Omit to run with no seeded session (used by specs
   * that exercise the pre-auth UX, e.g. the malformed-bearer 401 case).
   */
  persona: Persona | undefined;
}

export const test = base.extend<AuthOptions>({
  // `option: true` makes `persona` settable via `test.use(...)`.
  persona: [undefined, { option: true }],

  // Override the `context` fixture so we can `addInitScript` before any
  // page in the context navigates. The init script writes the persona
  // into sessionStorage so the mock PCA and api-client both see the
  // same value on first access.
  context: async ({ context, persona }, use) => {
    if (persona !== undefined) {
      if (!isPersona(persona)) {
        throw new Error(
          `[spec-007 auth fixture] Unknown persona "${persona}". Allowed: ${Object.keys(
            PERSONA_CONFIGS,
          ).join(", ")}.`,
        );
      }
      await context.addInitScript(
        ({ key, value }) => {
          try {
            window.sessionStorage.setItem(key, value);
          } catch {
            /* sessionStorage may be unavailable in unusual contexts; the
             * mock PCA will report a recognisable error in that case. */
          }
        },
        { key: E2E_PERSONA_SESSION_KEY, value: persona },
      );
    }
    await use(context);
  },
});

export { expect };
