import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import type { Rule } from "eslint";

/**
 * Custom rule: no-physical-direction-utilities
 *
 * Per FR-022b / SC-012, BusTerminal is RTL-safe by construction. Tailwind
 * physical-direction utilities (`ml-*`, `mr-*`, `pl-*`, `pr-*`, `left-*`,
 * `right-*`, `text-left`, `text-right`) leak left/right assumptions into
 * the visual layer. Use logical equivalents instead:
 *   ml-* → ms-*   mr-* → me-*
 *   pl-* → ps-*   pr-* → pe-*
 *   left-* → start-*   right-* → end-*
 *   text-left → text-start   text-right → text-end
 *
 * Token files in `web/styles/` are exempt — they may need physical anchors
 * for low-level theming primitives.
 */
const PHYSICAL_DIRECTION_PATTERN =
  /(?:(?<=^|\s)(?:ml-|mr-|pl-|pr-|left-|right-)[\w./[\]#%-]+)|(?:(?<=^|\s)text-(?:left|right)(?=$|\s))/g;

const noPhysicalDirectionUtilities: Rule.RuleModule = {
  meta: {
    type: "problem",
    docs: {
      description:
        "Disallow Tailwind physical-direction utilities (use logical: ms-/me-/ps-/pe-/start-/end-/text-start/text-end) — FR-022b / SC-012",
    },
    schema: [],
    messages: {
      physical:
        "Tailwind physical-direction utility found: '{{match}}'. Use the logical equivalent (ms-/me-/ps-/pe-/start-/end-/text-start/text-end) instead — FR-022b / SC-012.",
    },
  },
  create(context) {
    function report(node: Rule.Node, raw: string) {
      const matches = raw.matchAll(PHYSICAL_DIRECTION_PATTERN);
      for (const match of matches) {
        context.report({
          node,
          messageId: "physical",
          data: { match: match[0].trim() },
        });
      }
    }
    return {
      Literal(node) {
        if (typeof node.value === "string") {
          report(node as Rule.Node, node.value);
        }
      },
      TemplateElement(node) {
        report(node as Rule.Node, node.value.raw);
      },
    };
  },
};

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  globalIgnores([
    ".next/**",
    "out/**",
    "build/**",
    "node_modules/**",
    "storybook-static/**",
    "next-env.d.ts",
    // Token files may need physical-direction anchors for low-level theming.
    "styles/**",
    "app/globals.css",
  ]),
  {
    name: "busterminal/no-physical-direction-utilities",
    files: ["**/*.{ts,tsx,js,jsx,mts,cts}"],
    ignores: [
      // Audit scripts mention the forbidden utility names as data; they
      // are the canonical source of truth for the same rule.
      "scripts/**",
    ],
    plugins: {
      busterminal: {
        rules: {
          "no-physical-direction-utilities": noPhysicalDirectionUtilities,
        },
      },
    },
    rules: {
      "busterminal/no-physical-direction-utilities": "error",
    },
  },
]);

export default eslintConfig;
