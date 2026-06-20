/**
 * Spec 009 / T069 / US2. Playwright E2E for the entity catalog walkthrough
 * (quickstart §US2):
 *   1. Land on /registry/search
 *   2. Apply the lifecycle filter — verify URL state
 *   3. Apply the service association filter — verify URL state
 *   4. Open a published entity detail page — verify the Spec 009 panels
 *      (discovery info + Azure metadata + curated metadata) render
 *
 * The published-entity endpoints are stubbed so the test doesn't depend on a
 * live discovery run — the goal here is the UI surfaces, not the worker.
 */

import { test, expect } from "@/tests/fixtures/auth";

test.describe("entity catalog (US2)", () => {
  test.use({ persona: "reader" });

  test("filters update URL state", async ({ page }) => {
    await page.goto("/registry/search?q=*");
    await page.waitForSelector('[data-testid="registry-search-page"]');

    // Lifecycle filter — click Active.
    await page.getByRole("checkbox", { name: "Active" }).click();
    await expect.poll(() => page.url()).toContain("lifecycleStatus=Active");

    // Service association filter — type a service id.
    await page.getByTestId("associated-service-input").fill("svc_alpha");
    await expect.poll(() => page.url()).toContain("associatedServiceId=svc_alpha");

    // Toggle Owner role narrowing — should now be enabled.
    const ownerChip = page.getByRole("checkbox", { name: "Owner" });
    await expect(ownerChip).toBeEnabled();
    await ownerChip.click();
    await expect.poll(() => page.url()).toContain("associationRole=Owner");
  });

  test("published entity detail renders the Spec 009 panels", async ({ page }) => {
    await page.route("**/api/entities/pe_*", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        headers: { etag: '"v1"' },
        body: JSON.stringify({
          id: "pe_AAAAAAAAAAAAAAAAAAAAAAAA",
          schemaVersion: "1.1",
          entityType: "Queue",
          environment: "dev",
          namespaceId: "ns_demo",
          name: "orders-inbox",
          displayName: "orders-inbox",
          compositeKey: "q:ns_demo/orders-inbox",
          description: "Orders received from the storefront.",
          businessPurpose: "Bridges checkout to fulfillment.",
          tags: ["domain:orders", "tier:critical"],
          lifecycleStatus: "Active",
          lifecycleStatusChangedUtc: "2026-06-17T14:32:00Z",
          firstDiscoveredUtc: "2026-06-15T09:01:11Z",
          lastSeenUtc: "2026-06-17T14:32:14Z",
          lastDiscoveryRunId: "dr_e2e",
          azureSourced: {
            $type: "Queue",
            status: "Active",
            azureResourceId: "/subscriptions/x/queues/orders-inbox",
            lockDuration: "PT1M",
            maxDeliveryCount: 10,
            duplicateDetection: { enabled: true, historyTimeWindow: "PT10M" },
            deadLettering: { deadLetterOnMessageExpiration: true },
            partitioning: { enabled: false },
            session: { enabled: false },
            forwarding: { forwardTo: null, forwardDeadLetteredMessagesTo: null },
            defaultTimeToLive: "P14D",
            maxSizeInMegabytes: 5120,
          },
          azureSourcedHash: "sha256:abc",
          serviceAssociations: [],
        }),
      });
    });

    await page.goto("/registry/Queue/pe_AAAAAAAAAAAAAAAAAAAAAAAA");
    await page.waitForSelector('[data-testid="published-entity-detail"]');
    await expect(page.getByTestId("entity-discovery-info")).toBeVisible();
    await expect(page.getByTestId("entity-azure-metadata")).toBeVisible();
    await expect(page.getByTestId("entity-registry-metadata")).toBeVisible();
    await expect(page.getByTestId("entity-lifecycle-badge")).toHaveText("Active");
    await expect(page.getByTestId("azure-lock-duration")).toHaveText("PT1M");
    await expect(page.getByTestId("entity-tags")).toContainText("domain:orders");
  });
});
