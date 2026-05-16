#!/usr/bin/env node
/**
 * audit-strings (T037 / SC-012).
 *
 * Scans primitive and composite source for raw user-facing strings inside
 * JSX text nodes. Components MUST source copy through `t(key)` from
 * `@/lib/i18n` so a future translation spec can swap the implementation.
 *
 * Heuristic — flags JSX text nodes longer than three characters that
 * contain at least one alphabetic word and are NOT entirely whitespace,
 * punctuation, or an interpolation expression. Aria/role attribute literals
 * are also flagged when they look like prose copy.
 */

import { readFile } from "node:fs/promises";
import { resolve, relative } from "node:path";
import { glob } from "node:fs/promises";

const ROOT = resolve(import.meta.dirname, "..");

const INCLUDE_PATTERNS = [
  "components/**/*.{tsx}",
  "app/**/*.{tsx}",
];

const EXCLUDE_DIRS = [
  "node_modules",
  ".next",
  "lib",
  "stories",
  "app/_showcase",
];

// Storybook stories and Vitest specs are demo / verification content. Their
// hardcoded labels exist to exercise primitives, not to ship to users.
const EXCLUDE_FILE_PATTERNS = [/\.stories\.tsx$/, /\.test\.tsx$/, /\.spec\.tsx$/];

function isExempt(file) {
  const normalized = file.replaceAll("\\", "/");
  if (EXCLUDE_FILE_PATTERNS.some((pattern) => pattern.test(normalized))) return true;
  return EXCLUDE_DIRS.some((dir) => normalized.startsWith(`${dir}/`) || normalized.includes(`/${dir}/`));
}

const JSX_TEXT = />[\s\S]{3,}?</g;
const ARIA_LABEL = /aria-label\s*=\s*"([^"]+)"/g;
const PLACEHOLDER_ATTR = /placeholder\s*=\s*"([^"]+)"/g;

function looksLikeProse(value) {
  const trimmed = value.replace(/\{[^}]*\}/g, "").trim();
  if (trimmed.length < 3) return false;
  if (!/[a-z]/i.test(trimmed)) return false;
  // Acceptable single tokens (camelCase identifiers, kebab labels, etc.)
  if (/^[a-z][a-z0-9-]*$/i.test(trimmed)) return false;
  // Accept pure punctuation / glyphs.
  if (/^[^\w\s]+$/.test(trimmed)) return false;
  return true;
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
    let inT = false;
    let tParenDepth = 0;
    lines.forEach((line, index) => {
      // Skip pure-comment lines (best-effort).
      const trimmed = line.trimStart();
      if (trimmed.startsWith("//") || trimmed.startsWith("*")) return;
      // Skip lines that already invoke `t(`.
      if (/\bt\(/.test(line) && !/\bt\([\)]/.test(line)) inT = true;
      tParenDepth = inT ? tParenDepth + (line.split("(").length - 1) - (line.split(")").length - 1) : 0;
      if (inT && tParenDepth <= 0) inT = false;
      if (inT) return;

      JSX_TEXT.lastIndex = 0;
      let match;
      while ((match = JSX_TEXT.exec(line))) {
        const text = match[0].slice(1, -1);
        if (looksLikeProse(text)) {
          violations += 1;
          process.stdout.write(
            `${relative(process.cwd(), absolute)}:${index + 1} jsx-text: "${text.trim().slice(0, 60)}" — wrap in t(key).\n`,
          );
        }
      }
      ARIA_LABEL.lastIndex = 0;
      while ((match = ARIA_LABEL.exec(line))) {
        const text = match[1];
        if (text && looksLikeProse(text)) {
          violations += 1;
          process.stdout.write(
            `${relative(process.cwd(), absolute)}:${index + 1} aria-label: "${text}" — wrap in t(key).\n`,
          );
        }
      }
      PLACEHOLDER_ATTR.lastIndex = 0;
      while ((match = PLACEHOLDER_ATTR.exec(line))) {
        const text = match[1];
        if (text && looksLikeProse(text)) {
          violations += 1;
          process.stdout.write(
            `${relative(process.cwd(), absolute)}:${index + 1} placeholder: "${text}" — wrap in t(key).\n`,
          );
        }
      }
    });
  }
  if (violations > 0) {
    process.stdout.write(`\naudit:strings — ${violations} violation(s)\n`);
    process.exit(1);
  }
  process.stdout.write("audit:strings — clean.\n");
}

main().catch((err) => {
  process.stderr.write(`audit:strings crashed: ${err.stack ?? err}\n`);
  process.exit(2);
});
