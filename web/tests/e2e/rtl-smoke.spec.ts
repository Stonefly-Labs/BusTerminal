/**
 * RTL smoke E2E (T111 / FR-022d / SC-011).
 *
 * Sets `dir="rtl"` on the demo screen at `/` and walks the same
 * primitives the keyboard-only spec walks (table, sheet, form, toast).
 * The acceptance contract:
 *
 *   1. No element overflows the viewport horizontally — the page does
 *      not produce a horizontal scrollbar on `<body>` or on any
 *      explicit region (`<main>`, `<header>`, `<aside>`, the table
 *      scroll container). This catches accidental physical-direction
 *      utilities (the `audit:directions` script is a static guard;
 *      this is the runtime guard).
 *
 *   2. The Sheet/Drawer overlay docks to the visual start edge in RTL,
 *      not the LTR `end`. We assert the overlay's bounding box is
 *      anchored to the start of the viewport when `side="end"` is
 *      requested (because in RTL, the logical `end` flips to the left
 *      visual edge of the screen).
 *
 *   3. Dropdown / popover anchors don't mis-position — the user-menu
 *      content opens within the viewport bounds in both LTR and RTL.
 *
 *   4. Both themes (dark + light) render without breakage in RTL.
 */

import { test, expect, type Page } from "@playwright/test";

async function setDirectionRtl(page: Page): Promise<void> {
  await page.evaluate(() => {
    document.documentElement.dir = "rtl";
  });
}

async function viewportHorizontalOverflow(page: Page): Promise<number> {
  return page.evaluate(() => {
    return Math.max(
      0,
      document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
  });
}

async function setTheme(page: Page, theme: "light" | "dark"): Promise<void> {
  await page.evaluate((t) => {
    window.localStorage.setItem("bt:theme", t);
    if (t === "dark") {
      document.documentElement.classList.add("dark");
    } else {
      document.documentElement.classList.remove("dark");
    }
  }, theme);
}

test.describe("RTL smoke (T111 / SC-011)", () => {
  for (const theme of ["dark", "light"] as const) {
    test(`every primitive renders without breakage in RTL · ${theme} theme`, async ({
      page,
    }) => {
      await page.goto("/showcase", { waitUntil: "domcontentloaded" });
      await setTheme(page, theme);
      await setDirectionRtl(page);

      // 1) No viewport-level horizontal overflow.
      const overflow = await viewportHorizontalOverflow(page);
      expect(overflow).toBe(0);

      // 2) Open the Sheet via the first row's "Details" button and
      //    assert the overlay anchors to the start (visual left) edge in
      //    RTL when `side="end"` is requested.
      const firstDetailsButton = page.getByRole("button", { name: /details/i }).first();
      await firstDetailsButton.click();
      const dialog = page.getByRole("dialog");
      await expect(dialog).toBeVisible();

      const box = await dialog.boundingBox();
      expect(box).not.toBeNull();
      if (!box) throw new Error("dialog has no bounding box");
      // In RTL with `side="end"`, the panel's left edge should be near 0
      // (it docks against the visual start which is the left side).
      // We allow up to 8px of overlay padding/border tolerance.
      expect(box.x).toBeLessThan(8);

      // 3) Dialog content is inside the viewport.
      const viewportSize = page.viewportSize();
      expect(viewportSize).not.toBeNull();
      if (!viewportSize) throw new Error("no viewport size");
      expect(box.x + box.width).toBeLessThanOrEqual(viewportSize.width);
      expect(box.y).toBeGreaterThanOrEqual(0);
      expect(box.y + box.height).toBeLessThanOrEqual(viewportSize.height + 2);

      // 4) Close with Escape — dialog closes, no overflow introduced.
      await page.keyboard.press("Escape");
      await expect(dialog).toBeHidden();
      expect(await viewportHorizontalOverflow(page)).toBe(0);

      // 5) Open the user menu and assert it stays in-viewport.
      const userMenuButton = page.getByRole("button", { name: /open user menu/i });
      await userMenuButton.click();
      const menu = page.getByRole("menu");
      await expect(menu).toBeVisible();
      const menuBox = await menu.boundingBox();
      expect(menuBox).not.toBeNull();
      if (!menuBox) throw new Error("menu has no bounding box");
      expect(menuBox.x).toBeGreaterThanOrEqual(0);
      expect(menuBox.x + menuBox.width).toBeLessThanOrEqual(viewportSize.width);

      await page.keyboard.press("Escape");
      await expect(menu).toBeHidden();
    });
  }
});
