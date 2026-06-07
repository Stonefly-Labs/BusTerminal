/**
 * Spec 006 / T119 [US3] [TEST]. Playwright E2E covering quickstart §7:
 *
 *   1. Create a namespace → topic → subscription → rule chain.
 *   2. From the topic detail page, drill into the subscription via the
 *      relationships panel (row click), then drill from subscription to rule.
 *   3. On every detail page, the audit panel lists newest-first events with
 *      actor, timestamp, and change summary.
 *   4. Clicking an Updated/StatusChanged event reveals the field-diff popover.
 *
 * Requires a running backend (mock-auth dev mode is fine).
 *
 * Status: `test.fixme` pending the MSAL E2E auth fixture promised by T093
 * (Phase 9 polish, spec 003) — the page sits behind `AuthGuard`.
 */

import { test, expect } from "@playwright/test";

const ENV = "dev";
const STAMP = Date.now().toString(36);

test.describe("registry — relationships + audit drilldown", () => {
  test.fixme("topic → subscription → rule drill, with audit panel + field diff", async ({ page }) => {
    const nsName = `audit-ns-${STAMP}`;
    const topicName = `audit-topic-${STAMP}`;
    const subName = `audit-sub-${STAMP}`;
    const ruleName = `audit-rule-${STAMP}`;

    // 1. Create namespace.
    await page.goto("/registry/new/Namespace");
    await page.getByLabel(/Name/, { exact: false }).fill(nsName);
    await page.getByLabel(/Environment/, { exact: false }).fill(ENV);
    await page.getByRole("button", { name: /^Save$/ }).click();
    await expect(page).toHaveURL(/\/registry\/Namespace\//);
    await expect(page.getByText(nsName)).toBeVisible();

    // 2. Create child topic under that namespace.
    await page.goto("/registry/new/Topic");
    await page.getByLabel(/Name/, { exact: false }).fill(topicName);
    await page.getByLabel(/Environment/, { exact: false }).fill(ENV);
    // The parent picker is component-specific; test the happy path that the
    // existing form-wiring (T097) already handles by typing the parent name.
    await page.getByLabel(/Parent/i).fill(nsName);
    await page.getByRole("button", { name: /^Save$/ }).click();
    await expect(page).toHaveURL(/\/registry\/Topic\//);

    // 3. Create subscription under topic, then rule under subscription.
    await page.goto("/registry/new/Subscription");
    await page.getByLabel(/Name/, { exact: false }).fill(subName);
    await page.getByLabel(/Environment/, { exact: false }).fill(ENV);
    await page.getByLabel(/Parent/i).fill(topicName);
    await page.getByRole("button", { name: /^Save$/ }).click();
    await expect(page).toHaveURL(/\/registry\/Subscription\//);

    await page.goto("/registry/new/Rule");
    await page.getByLabel(/Name/, { exact: false }).fill(ruleName);
    await page.getByLabel(/Environment/, { exact: false }).fill(ENV);
    await page.getByLabel(/Parent/i).fill(subName);
    await page.getByRole("button", { name: /^Save$/ }).click();
    await expect(page).toHaveURL(/\/registry\/Rule\//);

    // 4. Navigate to the topic detail page; the audit panel should list its
    //    create event, and the relationships panel should list the new sub.
    //    We approach via the explorer search affordance — quickstart §7
    //    requires the drill from the topic page itself.
    await page.goto("/registry/search");
    await page.getByPlaceholder(/Search/i).fill(topicName);
    await page.getByText(topicName).first().click();
    await expect(page).toHaveURL(/\/registry\/Topic\//);

    // 5. Relationships panel — verify the subscription row, then click it.
    const relationships = page.getByTestId("registry-relationships-panel");
    await expect(relationships).toHaveAttribute("data-variant", "loaded");
    await expect(relationships.getByText(subName)).toBeVisible();
    await relationships.getByText(subName).click();
    await expect(page).toHaveURL(/\/registry\/Subscription\//);

    // 6. Sub detail → click into the rule via the relationships panel.
    const subRelationships = page.getByTestId("registry-relationships-panel");
    await expect(subRelationships).toHaveAttribute("data-variant", "loaded");
    await subRelationships.getByText(ruleName).click();
    await expect(page).toHaveURL(/\/registry\/Rule\//);

    // 7. Rule detail → relationships panel reports leaf type, audit panel
    //    shows the Created event with actor+timestamp+summary.
    const ruleRelationships = page.getByTestId("registry-relationships-panel");
    await expect(ruleRelationships).toHaveAttribute("data-variant", "leaf");

    const audit = page.getByTestId("registry-audit-panel");
    await expect(audit).toHaveAttribute("data-variant", "loaded");
    const firstEvent = audit.getByTestId("registry-audit-event").first();
    await expect(firstEvent).toHaveAttribute("data-event-type", "Created");
    // Actor is the mock-auth dev user (set in MockAuthenticationHandler).
    await expect(firstEvent.getByText(/Dev User/)).toBeVisible();

    // 8. Edit the rule so an Updated event lands, then assert the diff popover.
    await page.getByRole("link", { name: /^Edit$/i }).click();
    await page
      .getByLabel(/Description/i)
      .fill("edited-for-audit-test");
    await page.getByRole("button", { name: /^Save$/ }).click();

    // Form navigates back; the audit panel should reflect the new event
    // immediately (T125 invalidation contract).
    await page.goto(page.url().replace(/\/edit$/, ""));
    const auditAfterEdit = page.getByTestId("registry-audit-panel");
    await expect(auditAfterEdit).toHaveAttribute("data-variant", "loaded");
    const newest = auditAfterEdit.getByTestId("registry-audit-event").first();
    await expect(newest).toHaveAttribute("data-event-type", "Updated");
    await expect(newest).toHaveAttribute("data-has-diff", "true");

    // 9. Click the change-summary trigger → field-diff popover should open.
    await newest.getByTestId("registry-audit-event-trigger").click();
    const diff = page.getByTestId("registry-audit-field-diff");
    await expect(diff).toBeVisible();
    await expect(diff.getByText("description")).toBeVisible();
  });
});
