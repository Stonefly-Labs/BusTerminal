#!/usr/bin/env node
/**
 * audit-tokens (T036 / SC-003).
 *
 * Scans foundation source for hardcoded design literals that should flow
 * through the design-token system instead. Failures are fatal — exits with
 * code 1 and prints every offending file:line.
 *
 * Rules (all match outside string-literal commentary):
 *   - Hex colors (`#abc`, `#abcdef`, `#abcdef12`).
 *   - rgb()/rgba()/hsl()/hsla()/oklch()/oklab() literals.
 *   - Spacing/radius/elevation literals in arbitrary Tailwind values
 *     (`p-[12px]`, `rounded-[6px]`, `gap-[10rem]`).
 *   - Inline `style={{ color: '#fff' }}` style objects with hard color hexes.
 *
 * Scope:
 *   - `web/components/**`
 *   - `web/lib/**`  (excluding the design-system + observability subtrees,
 *     which define the system itself)
 *
 * Tokens live in `web/styles/tokens.css` / `web/styles/typography.css` and
 * are surfaced to TypeScript via `web/lib/design-system/tokens.ts`.
 */

import { readFile } from "node:fs/promises";
import { resolve, relative } from "node:path";
import { glob } from "node:fs/promises";

const ROOT = resolve(import.meta.dirname, "..");
const RULES = [
  {
    name: "hex-color",
    pattern: /#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b/g,
    description: "Hex color literal — use a token from web/styles/tokens.css.",
  },
  {
    name: "rgb-or-hsl-color",
    pattern: /\b(?:rgba?|hsla?|oklch|oklab)\s*\(/g,
    description: "Color function literal — use a token from web/styles/tokens.css.",
  },
  {
    name: "arbitrary-tailwind-spacing",
    pattern: /(?:^|[\s"'`])(?:p|m|gap|inset|top|right|bottom|left|w|h|space-x|space-y|rounded|shadow)-(?:[a-z]+-)?\[[^\]]+\]/g,
    description: "Arbitrary Tailwind value — pick a token-backed scale step or extend tokens.css.",
  },
];

// Source files we audit. Foundation utilities that DEFINE tokens (the
// design-system + observability subtrees, plus styles/) are exempt.
const INCLUDE_PATTERNS = [
  "components/**/*.{ts,tsx}",
  "lib/**/*.{ts,tsx}",
  "app/**/*.{ts,tsx}",
];

const EXCLUDE_DIRS = [
  "node_modules",
  ".next",
  "lib/design-system",
  "lib/observability",
  "styles",
];

// Specific file exemptions. `app/icon.tsx`, `app/apple-icon.tsx`, and
// `app/opengraph-image.tsx` use `ImageResponse` to render PNGs and cannot
// resolve CSS custom properties; they import raw color constants from
// `lib/design-system/raster-colors.ts`, which is the only sanctioned source
// of hardcoded color values in the foundation.
const EXCLUDE_FILES = new Set([
  "app/icon.tsx",
  "app/apple-icon.tsx",
  "app/opengraph-image.tsx",
]);

// Stories and unit tests can occasionally need raw demo values to exercise
// edge cases; they don't ship to users.
const EXCLUDE_FILE_PATTERNS = [/\.stories\.tsx$/, /\.test\.tsx$/, /\.test\.ts$/, /\.spec\.tsx$/, /\.spec\.ts$/];

function isExempt(file) {
  const normalized = file.replaceAll("\\", "/");
  if (EXCLUDE_FILES.has(normalized)) return true;
  if (EXCLUDE_FILE_PATTERNS.some((pattern) => pattern.test(normalized))) return true;
  return EXCLUDE_DIRS.some(
    (dir) => normalized.startsWith(`${dir}/`) || normalized.includes(`/${dir}/`),
  );
}

async function* iterFiles() {
  for (const pattern of INCLUDE_PATTERNS) {
    for await (const file of glob(pattern, { cwd: ROOT })) {
      if (!isExempt(file)) yield file;
    }
  }
}

async function main() {
  let violations = 0;
  for await (const file of iterFiles()) {
    const absolute = resolve(ROOT, file);
    const contents = await readFile(absolute, "utf8");
    const lines = contents.split(/\r?\n/);
    lines.forEach((line, index) => {
      // Skip pure-comment lines (best-effort heuristic).
      const trimmed = line.trimStart();
      if (trimmed.startsWith("//") || trimmed.startsWith("*")) return;
      for (const rule of RULES) {
        rule.pattern.lastIndex = 0;
        const match = rule.pattern.exec(line);
        if (match) {
          violations += 1;
          process.stdout.write(
            `${relative(process.cwd(), absolute)}:${index + 1} ${rule.name}: "${match[0]}" — ${rule.description}\n`,
          );
        }
      }
    });
  }
  if (violations > 0) {
    process.stdout.write(`\naudit:tokens — ${violations} violation(s)\n`);
    process.exit(1);
  }
  process.stdout.write("audit:tokens — clean.\n");
}

main().catch((err) => {
  process.stderr.write(`audit:tokens crashed: ${err.stack ?? err}\n`);
  process.exit(2);
});
