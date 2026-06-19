/**
 * Spec 009 / T102 / US4. End-to-end walkthrough for the entity curation
 * surface — covers the FR-016 "preservation across rediscovery" acceptance
 * scenario from `quickstart.md`.
 *
 * Status: `test.fixme` — same caveat as the other Spec 009 E2E tests
 * (discovery-flow.spec.ts, discovery-history.spec.ts). Sign-in flows
 * through real Entra; an automated walkthrough needs either a dev-tenant
 * test user with a stored MSAL session or the synthetic-account
 * Playwright fixture that lands with the Phase 9 polish work.
 *
 * Component-level coverage today:
 *   - `<PublishedEntityEditForm>` (Vitest, T100).
 *   - `<ServiceAssociationEditor>` (Vitest, T099).
 *   - `UpdateEntityMetadataContractTests` (xUnit, T093) — wire-shape.
 *   - `MetadataPreservationIntegrationTests` (xUnit, T096) — FR-016 at the
 *     store layer.
 *
 * Once the persona fixture lands, this file should:
 *   1. Sign in as `BusTerminal.Admin`.
 *   2. Seed a published entity via the API surface or the Spec 008 onboard +
 *      Spec 009 discovery path.
 *   3. Edit the entity — set description, businessPurpose, tags, contact.
 *   4. Add a `(svc_owner, Owner)` association via the dialog.
 *   5. Trigger a fresh discovery run via the namespace overview.
 *   6. Re-open the entity detail and verify every curated field + the
 *      association survived.
 *   7. Archive the entity — verify lifecycleStatus = Archived and that
 *      subsequent discovery does not flip it back.
 */

import { test } from "@playwright/test";

test.describe("Entity curation (US4)", () => {
  test.fixme(
    "edits curated metadata, adds an association, and verifies survival across rediscovery",
    async ({ page }) => {
      await page.goto("/platform-status");
    },
  );

  test.fixme(
    "archiving sticks across a subsequent discovery run",
    async ({ page }) => {
      await page.goto("/platform-status");
    },
  );
});
