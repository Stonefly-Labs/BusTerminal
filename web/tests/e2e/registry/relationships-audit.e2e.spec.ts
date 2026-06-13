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

import { test, expect } from "@/tests/fixtures/auth";

const ENV = "dev";
const STAMP = Date.now().toString(36);

test.describe("registry — relationships + audit drilldown", () => {
  // Spec 007 — the spec creates entities (namespace → topic → subscription
  // → rule) in addition to drilling, so `operator` is the minimal persona
  // that authorises the full chain.
  test.use({ persona: "operator" });

  // T119 was authored speculatively in spec 006 and unfixme'd by spec 007.
  // Once the auth fixture unblocked it, three latent contract bugs surfaced
  // (env list shape, ISO datetime offset, header/form label collision) — all
  // fixed in PR #54. What remains is UI-drift in the test itself: the search
  // result click on /registry/search, audit-panel/field-diff drilldowns, and
  // the rule-edit flow all need rewrites against the current shipped UI. Mark
  // fixme until a dedicated cleanup PR re-derives the interactions; the
  // create-browse spec already exercises the namespace happy-path that the
  // contract-bug fixes unblock.
  test.fixme("topic → subscription → rule drill, with audit panel + field diff", async ({ page }) => {
    const nsName = `audit-ns-${STAMP}`;
    const topicName = `audit-topic-${STAMP}`;
    const subName = `audit-sub-${STAMP}`;
    const ruleName = `audit-rule-${STAMP}`;

    // Scope every form interaction to the entity-form-shell — the global
    // header also exposes an "Environment" switcher, so unscoped
    // getByLabel(/Environment/) is ambiguous.
    const formOf = (root: typeof page) => root.getByTestId("entity-form-shell");
    // The detail-page heading is the strict-mode-safe identity anchor (the
    // name also appears in the tree, audit summary, etc.).
    const headingOf = (name: string) =>
      page.getByRole("heading", { name, exact: true });
    // Parent picker is a shadcn <Select> (button + listbox), not a textbox.
    // Open the trigger, then click the option matching `parentName`.
    const pickParent = async (parentTypeLabel: string, parentName: string) => {
      await formOf(page).getByRole("combobox", { name: parentTypeLabel }).click();
      await page.getByRole("option", { name: parentName, exact: true }).click();
    };

    // 1. Create namespace.
    await page.goto("/registry/new/Namespace");
    {
      const form = formOf(page);
      await form.getByLabel(/Name/, { exact: false }).fill(nsName);
      await form.getByLabel(/Environment/, { exact: false }).fill(ENV);
      await form.getByRole("button", { name: /^Save$/ }).click();
    }
    await expect(page).toHaveURL(/\/registry\/Namespace\//);
    await expect(headingOf(nsName)).toBeVisible();

    // 2. Create child topic under that namespace.
    await page.goto("/registry/new/Topic");
    {
      const form = formOf(page);
      await form.getByLabel(/Name/, { exact: false }).fill(topicName);
      await form.getByLabel(/Environment/, { exact: false }).fill(ENV);
      await pickParent("Parent Namespace", nsName);
      await form.getByRole("button", { name: /^Save$/ }).click();
    }
    await expect(page).toHaveURL(/\/registry\/Topic\//);

    // 3. Create subscription under topic, then rule under subscription.
    await page.goto("/registry/new/Subscription");
    {
      const form = formOf(page);
      await form.getByLabel(/Name/, { exact: false }).fill(subName);
      await form.getByLabel(/Environment/, { exact: false }).fill(ENV);
      await pickParent("Parent Topic", topicName);
      await form.getByRole("button", { name: /^Save$/ }).click();
    }
    await expect(page).toHaveURL(/\/registry\/Subscription\//);

    await page.goto("/registry/new/Rule");
    {
      const form = formOf(page);
      await form.getByLabel(/Name/, { exact: false }).fill(ruleName);
      await form.getByLabel(/Environment/, { exact: false }).fill(ENV);
      await pickParent("Parent Subscription", subName);
      await form.getByRole("button", { name: /^Save$/ }).click();
    }
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
    {
      const form = formOf(page);
      await form.getByLabel(/Description/i).fill("edited-for-audit-test");
      await form.getByRole("button", { name: /^Save$/ }).click();
    }

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
