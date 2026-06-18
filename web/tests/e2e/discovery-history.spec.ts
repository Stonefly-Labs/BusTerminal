/**
 * Spec 009 / T085 / US3. End-to-end walkthrough for the discovery history
 * surface. Mirrors the US3 acceptance scenario from `quickstart.md`:
 *
 *   1. Trigger several discovery runs on a registered namespace —
 *      including at least one that is engineered to fail (worker fault
 *      injection, e.g. point the registered namespace at a non-existent
 *      ARM resource so the FetchQueues phase returns NotFound).
 *   2. Open the namespace overview, click into the discovery-runs list.
 *   3. Assert the runs appear in reverse-chronological order with
 *      correct status badges and counts.
 *   4. Click into the engineered-failure run.
 *   5. Assert the failure card surfaces the operator-friendly category,
 *      phase, and message — and that the message does NOT leak ARM
 *      resource paths or entity display names (FR-021 / R-12 / SC-006).
 *
 * Status: `test.fixme` — depends on the same MSAL persona fixture as the
 * Phase 3 E2E tests (`discovery-flow.spec.ts`). Behaviour is covered by:
 *   - `discovery-runs-table.test.tsx` — UI render + navigation.
 *   - `discovery-run-detail.test.tsx` — failure card mapping.
 *   - `ListDiscoveryRunsContractTests.cs` + `…PaginationTests.cs` — API.
 *   - `FailureMessageSanitizerTests.cs` (Phase 3) — message redaction.
 */

import { test } from "@playwright/test";

test.describe("US3 — discovery history end-to-end", () => {
  test.fixme(
    "operator inspects history and identifies a failed run's root cause",
    async ({ page }) => {
      await page.goto("/platform-status");
    },
  );
});
