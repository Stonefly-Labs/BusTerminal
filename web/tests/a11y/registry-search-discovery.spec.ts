/**
 * Spec 009 / T068 / US2. axe-playwright a11y coverage for the extended
 * /registry/search route (with the new lifecycle + association filters)
 * and a published-entity detail page.
 */

import { test } from "@playwright/test";
import { checkA11y, injectAxe } from "axe-playwright";

const THEMES = ["light", "dark"] as const;

for (const theme of THEMES) {
  test(`registry search with discovery filters is axe-clean on ${theme}`, async ({ page }) => {
    await page.addInitScript((t) => {
      window.localStorage.setItem("bt:theme", t);
    }, theme);
    await page.goto(
      "/registry/search?q=orders&lifecycleStatus=Active&associatedServiceId=svc_alpha&associationRole=Owner",
    );
    await page.waitForSelector('[data-testid="registry-search-page"]');
    await injectAxe(page);
    await checkA11y(page, '[data-testid="registry-search-page"]', {
      detailedReport: true,
      detailedReportOptions: { html: true },
    });
  });
}

for (const theme of THEMES) {
  test(`published entity detail page is axe-clean on ${theme}`, async ({ page }) => {
    await page.addInitScript((t) => {
      window.localStorage.setItem("bt:theme", t);
    }, theme);
    // Stub the published-entity detail endpoint so the page renders without
    // a live backend.
    await page.route("**/api/entities/pe_*", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        headers: { etag: '"etag-axe"' },
        body: JSON.stringify({
          id: "pe_AAAAAAAAAAAAAAAAAAAAAAAA",
          schemaVersion: "1.1",
          entityType: "Queue",
          environment: "dev",
          namespaceId: "ns_axe",
          name: "axe-queue",
          displayName: "axe-queue",
          compositeKey: "q:ns_axe/axe-queue",
          lifecycleStatus: "Active",
          lifecycleStatusChangedUtc: "2026-06-17T14:32:00Z",
          firstDiscoveredUtc: "2026-06-17T14:30:00Z",
          lastSeenUtc: "2026-06-17T14:32:00Z",
          lastDiscoveryRunId: "dr_axe",
          azureSourced: {
            $type: "Queue",
            status: "Active",
            azureResourceId: "/subscriptions/x/queues/axe-queue",
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
    await injectAxe(page);
    await checkA11y(page, '[data-testid="published-entity-detail"]', {
      detailedReport: true,
      detailedReportOptions: { html: true },
    });
  });
}
