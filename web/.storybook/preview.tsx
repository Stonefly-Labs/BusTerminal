import type { Preview } from "@storybook/nextjs";
import { withThemeByClassName } from "@storybook/addon-themes";
import type { ReactRenderer } from "@storybook/react";

import "../app/globals.css";

/**
 * Global Storybook preview configuration (T042).
 *
 * Composes:
 *   - `withThemeByClassName` for the dark / light toggle (mirrors the
 *     `next-themes` class strategy used by the app).
 *   - Inline `viewport.viewports` declarations covering the breakpoint set
 *     called out in SC-010 (mid-range laptop reference) and the
 *     edge-case viewports referenced in the spec (mobile, tablet, desktop,
 *     4K).
 *   - A `direction` global + decorator that sets `dir="rtl"` on the story
 *     root so reviewers can exercise SC-011 from any story.
 */

const VIEWPORTS = {
  mobile: {
    name: "Mobile (390×844 — iPhone 13)",
    styles: { width: "390px", height: "844px" },
    type: "mobile" as const,
  },
  tablet: {
    name: "Tablet (768×1024)",
    styles: { width: "768px", height: "1024px" },
    type: "tablet" as const,
  },
  laptop: {
    name: "Laptop (1366×768) — SC-010 reference",
    styles: { width: "1366px", height: "768px" },
    type: "desktop" as const,
  },
  desktop: {
    name: "Desktop (1920×1080)",
    styles: { width: "1920px", height: "1080px" },
    type: "desktop" as const,
  },
  workstation: {
    name: "Workstation (3840×2160 — 4K)",
    styles: { width: "3840px", height: "2160px" },
    type: "desktop" as const,
  },
};

const preview: Preview = {
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    a11y: {
      config: {
        rules: [
          // Reduced motion is exercised in a dedicated Playwright spec; let
          // the a11y addon focus on contrast / labels / structure per story.
          { id: "color-contrast", enabled: true },
        ],
      },
    },
    viewport: {
      viewports: VIEWPORTS,
      defaultViewport: "laptop",
    },
    backgrounds: { disable: true },
    layout: "centered",
  },
  globalTypes: {
    direction: {
      name: "Direction",
      description: "Document writing direction",
      defaultValue: "ltr",
      toolbar: {
        icon: "transfer",
        items: [
          { value: "ltr", title: "LTR" },
          { value: "rtl", title: "RTL" },
        ],
        dynamicTitle: true,
      },
    },
  },
  decorators: [
    withThemeByClassName<ReactRenderer>({
      themes: { light: "", dark: "dark" },
      defaultTheme: "dark",
      parentSelector: "html",
    }),
    (Story, context) => {
      const dir = context.globals.direction === "rtl" ? "rtl" : "ltr";
      return (
        <div
          dir={dir}
          className="bg-surface-canvas text-foreground-default min-h-[200px] p-6"
        >
          <Story />
        </div>
      );
    },
  ],
};

export default preview;
