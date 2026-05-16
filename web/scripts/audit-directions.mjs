#!/usr/bin/env node
/**
 * audit-directions (T038 / SC-012).
 *
 * Scans source for physical-direction Tailwind utilities that break RTL
 * support. Components MUST use logical-property utilities (`ms-*`, `me-*`,
 * `ps-*`, `pe-*`, `start-*`, `end-*`, `text-start`, `text-end`).
 *
 * Style files under `web/styles/**` and `web/app/globals.css` are exempt
 * because the token system itself defines neutral physical values when
 * required.
 */

import { readFile } from "node:fs/promises";
import { resolve, relative } from "node:path";
import { glob } from "node:fs/promises";

const ROOT = resolve(import.meta.dirname, "..");

const INCLUDE_PATTERNS = [
  "components/**/*.{ts,tsx}",
  "app/**/*.{ts,tsx}",
  "lib/**/*.{ts,tsx}",
];

const EXCLUDE_DIRS = ["node_modules", ".next", "styles"];

// Storybook stories and Vitest specs are demo content; physical-direction
// utilities are still discouraged but excluded from the gate so primitive
// authors can showcase legacy patterns when explicitly demonstrating RTL.
const EXCLUDE_FILE_PATTERNS = [/\.stories\.tsx$/, /\.test\.tsx$/, /\.test\.ts$/, /\.spec\.tsx$/, /\.spec\.ts$/];

function isExempt(file) {
  if (file.endsWith("app/globals.css")) return true;
  const normalized = file.replaceAll("\\", "/");
  if (EXCLUDE_FILE_PATTERNS.some((pattern) => pattern.test(normalized))) return true;
  return EXCLUDE_DIRS.some((dir) => normalized.startsWith(`${dir}/`) || normalized.includes(`/${dir}/`));
}

const PHYSICAL_PATTERNS = [
  { name: "ml-*", pattern: /\bml-[\w./[\]\-]+/g },
  { name: "mr-*", pattern: /\bmr-[\w./[\]\-]+/g },
  { name: "pl-*", pattern: /\bpl-[\w./[\]\-]+/g },
  { name: "pr-*", pattern: /\bpr-[\w./[\]\-]+/g },
  { name: "left-*", pattern: /\bleft-[\w./[\]\-]+/g },
  { name: "right-*", pattern: /\bright-[\w./[\]\-]+/g },
  { name: "text-left", pattern: /\btext-left\b/g },
  { name: "text-right", pattern: /\btext-right\b/g },
  { name: "border-l", pattern: /\bborder-l(?:-[\w./[\]\-]+)?/g },
  { name: "border-r", pattern: /\bborder-r(?:-[\w./[\]\-]+)?/g },
];

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
      const trimmed = line.trimStart();
      if (trimmed.startsWith("//") || trimmed.startsWith("*")) return;
      for (const rule of PHYSICAL_PATTERNS) {
        rule.pattern.lastIndex = 0;
        const match = rule.pattern.exec(line);
        if (match) {
          violations += 1;
          process.stdout.write(
            `${relative(process.cwd(), absolute)}:${index + 1} physical-direction: "${match[0]}" — use the logical-property equivalent (ms-/me-/ps-/pe-/start-/end-/text-start/text-end/border-s/border-e).\n`,
          );
        }
      }
    });
  }
  if (violations > 0) {
    process.stdout.write(`\naudit:directions — ${violations} violation(s)\n`);
    process.exit(1);
  }
  process.stdout.write("audit:directions — clean.\n");
}

main().catch((err) => {
  process.stderr.write(`audit:directions crashed: ${err.stack ?? err}\n`);
  process.exit(2);
});
