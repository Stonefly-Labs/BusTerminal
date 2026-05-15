import type { StorybookConfig } from "@storybook/nextjs";

/**
 * Storybook 10.x configuration for BusTerminal (T041).
 *
 * Framework: `@storybook/nextjs` (Next.js 16 App Router compatibility).
 * Addons: `@storybook/addon-a11y` (axe-core per story; FR-027), and
 * `@storybook/addon-themes` (dark/light toggle + side-by-side preview).
 *
 * Note: Storybook 9+ folds `@storybook/test`, `@storybook/addon-interactions`,
 * and `@storybook/addon-viewport` into the core `storybook` package — they
 * are NOT separate dependencies in this project. The RTL `dir` toggle is
 * implemented inline in `preview.tsx` via a `globalTypes` entry and a
 * decorator so we don't need a separate addon package.
 */

const config: StorybookConfig = {
  framework: {
    name: "@storybook/nextjs",
    options: {
      // Story-level Next.js App Router shim. RSC support is not yet needed
      // for primitives, which are Client Components by definition.
      nextConfigPath: "../next.config.ts",
    },
  },
  stories: [
    "../stories/**/*.mdx",
    "../components/**/*.stories.@(ts|tsx)",
    "../app/**/*.stories.@(ts|tsx)",
  ],
  addons: [
    "@storybook/addon-a11y",
    "@storybook/addon-themes",
  ],
  staticDirs: ["../public"],
  docs: {
    defaultName: "Documentation",
  },
  typescript: {
    check: false,
    reactDocgen: "react-docgen-typescript",
  },
};

export default config;
