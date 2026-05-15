#!/usr/bin/env node
/**
 * audit-review-records (T040 / FR-002b).
 *
 * Enforces the `REVIEW.md` gate described in
 * `specs/001-brand-system-and-design-foundation/contracts/brand-asset-review.md`:
 *
 *   1. Every committed SVG under `web/brand/**` whose neighboring directory
 *      does NOT contain a `placeholder.flag` MUST have a `REVIEW.md` in the
 *      same directory.
 *   2. Required headings present.
 *   3. Every checkbox under `## Checks performed` is `- [x]`.
 *   4. Every checkbox under `## Render verification` is `- [x]`.
 *   5. Exactly one decision item is checked, and it is "Approved for commit".
 *   6. `**Reviewer signature**` line present, handle is not the literal
 *      `<GitHub handle>`.
 */

import { readdir, readFile, stat } from "node:fs/promises";
import { resolve, relative, join } from "node:path";

const ROOT = resolve(import.meta.dirname, "..");
const BRAND_DIR = resolve(ROOT, "brand");

async function listAssetDirs(root) {
  const dirs = [];
  async function walk(dir) {
    const entries = await readdir(dir, { withFileTypes: true });
    let hasSvg = false;
    for (const entry of entries) {
      if (entry.isDirectory()) {
        await walk(join(dir, entry.name));
      } else if (entry.name.endsWith(".svg")) {
        hasSvg = true;
      }
    }
    if (hasSvg) dirs.push(dir);
  }
  try {
    await stat(root);
  } catch {
    return dirs;
  }
  await walk(root);
  return dirs;
}

function extractSection(markdown, heading) {
  const escaped = heading.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const re = new RegExp(`^##\\s+${escaped}\\s*$([\\s\\S]*?)(?=^##\\s|\\Z)`, "im");
  const match = re.exec(markdown);
  return match?.[1] ?? null;
}

function reviewIsClean(file, markdown) {
  const errors = [];

  for (const required of [
    "Checks performed",
    "Render verification",
    "Decision",
    "Signature",
  ]) {
    if (!new RegExp(`^##\\s+${required}\\s*$`, "im").test(markdown)) {
      errors.push(`missing required heading "## ${required}"`);
    }
  }

  const checks = extractSection(markdown, "Checks performed");
  if (checks) {
    const unchecked = checks.match(/- \[ \]/g);
    if (unchecked && unchecked.length > 0) {
      errors.push(`${unchecked.length} unchecked item(s) under "Checks performed"`);
    }
  }

  const render = extractSection(markdown, "Render verification");
  if (render) {
    const unchecked = render.match(/- \[ \]/g);
    if (unchecked && unchecked.length > 0) {
      errors.push(`${unchecked.length} unchecked item(s) under "Render verification"`);
    }
  }

  const decision = extractSection(markdown, "Decision");
  if (decision) {
    const approved = /-\s*\[\s*[xX]\s*\]\s*\*\*Approved for commit\*\*/.test(decision);
    const reworked = /-\s*\[\s*[xX]\s*\]\s*\*\*Rework required\*\*/.test(decision);
    if (!approved) errors.push(`"Approved for commit" not checked`);
    if (reworked) errors.push(`"Rework required" checked — asset is not ready for commit`);
  }

  if (!/\*\*Reviewer signature\*\*:\s*[^<\s][^\n]+\n?/.test(markdown)) {
    errors.push(`"**Reviewer signature**:" line missing or contains the placeholder "<GitHub handle>"`);
  }

  return errors;
}

async function main() {
  let violations = 0;
  const dirs = await listAssetDirs(BRAND_DIR);
  for (const dir of dirs) {
    const placeholderFlag = join(dir, "placeholder.flag");
    const reviewPath = join(dir, "REVIEW.md");
    let isPlaceholder = false;
    try {
      await stat(placeholderFlag);
      isPlaceholder = true;
    } catch {
      isPlaceholder = false;
    }
    if (isPlaceholder) continue;

    let markdown;
    try {
      markdown = await readFile(reviewPath, "utf8");
    } catch {
      violations += 1;
      process.stdout.write(
        `${relative(process.cwd(), dir)} — missing REVIEW.md (per contracts/brand-asset-review.md).\n`,
      );
      continue;
    }
    const errors = reviewIsClean(reviewPath, markdown);
    for (const err of errors) {
      violations += 1;
      process.stdout.write(`${relative(process.cwd(), reviewPath)} — ${err}\n`);
    }
  }
  if (violations > 0) {
    process.stdout.write(`\naudit:review-records — ${violations} violation(s)\n`);
    process.exit(1);
  }
  process.stdout.write("audit:review-records — clean.\n");
}

main().catch((err) => {
  process.stderr.write(`audit:review-records crashed: ${err.stack ?? err}\n`);
  process.exit(2);
});
