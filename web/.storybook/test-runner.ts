import { injectAxe, checkA11y } from "axe-playwright";
import type { TestRunnerConfig } from "@storybook/test-runner";
import { getStoryContext } from "@storybook/test-runner";

/**
 * Storybook test-runner configuration (T107 / FR-027 / SC-005).
 *
 * Pipes every published story through `axe-playwright` and fails the run
 * on any WCAG 2.2 AA violation. The threshold is enforced at zero
 * `violations` returned by axe — no severity filter, no exemptions.
 *
 * A story can opt out of the a11y scan story-by-story via
 * `parameters.a11y.disable = true` in its `*.stories.tsx`. The opt-out is
 * only meant for purpose-built failure-case stories (e.g., a fixture that
 * deliberately renders an empty `<button>` to prove a guard) and is
 * therefore enumerated explicitly so reviewers can audit it.
 *
 * Rule configuration is read from the story's `parameters.a11y.config`
 * (mirroring the `.storybook/preview.tsx` defaults) so individual stories
 * can request stricter or domain-specific rule sets without forking the
 * runner.
 */

const config: TestRunnerConfig = {
  async preVisit(page) {
    await injectAxe(page);
  },
  async postVisit(page, context) {
    const storyContext = await getStoryContext(page, context);

    const a11yParameter = storyContext.parameters?.a11y as
      | {
          disable?: boolean;
          config?: { rules?: Array<{ id: string; enabled?: boolean }> };
          element?: string;
        }
      | undefined;

    if (a11yParameter?.disable === true) {
      return;
    }

    // Translate the story's `parameters.a11y.config.rules` array into axe's
    // per-run `rules` map. Passing this through `axe.run` (via checkA11y's
    // axeOptions.rules) is more reliable than calling `axe.configure`
    // separately — axe-playwright builds a fresh run config each call, so
    // a global configure step can be ignored.
    const ruleOverrides = a11yParameter?.config?.rules?.reduce<
      Record<string, { enabled: boolean }>
    >((acc, rule) => {
      if (rule?.id && typeof rule.enabled === "boolean") {
        acc[rule.id] = { enabled: rule.enabled };
      }
      return acc;
    }, {});

    await checkA11y(
      page,
      a11yParameter?.element ?? "#storybook-root",
      {
        detailedReport: true,
        detailedReportOptions: { html: true },
        axeOptions: {
          runOnly: {
            type: "tag",
            values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"],
          },
          ...(ruleOverrides && Object.keys(ruleOverrides).length > 0
            ? { rules: ruleOverrides }
            : {}),
        },
      },
    );
  },
};

export default config;
