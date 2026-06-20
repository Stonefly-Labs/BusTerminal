/**
 * Spec 009 / T039 / US1. End-to-end discovery walkthrough.
 *
 * Status: `test.fixme` — same caveat as `role-aware-affordances.spec.ts`.
 * Sign-in flows through real Entra, so an automated walkthrough requires
 * either (a) a dev-tenant NamespaceAdministrator test user with a stored
 * MSAL session, or (b) the synthetic-account Playwright fixture that lands
 * with the Phase 9 polish work.
 *
 * The same behaviour is covered today by:
 *   - `web/components/discovery/discover-button.test.tsx` — click, toasts.
 *   - `api/BusTerminal.Api.Tests/Features/Discovery/...` — API surface.
 *   - `api/BusTerminal.Indexer.Tests/Discovery/...` — worker behavior.
 *
 * When the fixture lands the test below is wired against persona
 * `namespace-administrator` and asserts the happy-path described in
 * `specs/009-entity-discovery-publication/quickstart.md` (Scenario A — Run
 * discovery, watch counts populate, verify entity catalog).
 */

import { test } from "@playwright/test";

test.describe("US1 — discovery end-to-end", () => {
  test.fixme("namespace admin triggers discovery and sees the catalog populate", async ({ page }) => {
    // Phase 9 polish: replace this stub with the fixture-driven walkthrough.
    // 1. Sign in as a NamespaceAdministrator persona.
    // 2. Navigate to /namespaces/{seededNs}/.
    // 3. Click "Discover".
    // 4. Poll the status panel until the run reaches Succeeded.
    // 5. Click into the registry-search route, assert seeded entities appear.
    await page.goto("/platform-status");
  });
});
