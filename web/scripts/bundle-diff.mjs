#!/usr/bin/env node
/**
 * bundle-diff (T040 / FR-035e / SC-020).
 *
 * Compares the latest `@next/bundle-analyzer` JSON output against the
 * committed baseline at `web/docs/performance-budget.json`. Posts a PR
 * comment with red/green status against the 180 KB soft target and the
 * 200 KB hard alert threshold. Fails the PR check when:
 *
 *   - First Load JS grew by > +10% relative to the recorded baseline, OR
 *   - First Load JS crossed the 200 KB hard alert threshold.
 *
 * Inputs are read by path so the script runs the same way locally and in CI.
 */

import { readFile, writeFile } from "node:fs/promises";
import { resolve, relative } from "node:path";

const ROOT = resolve(import.meta.dirname, "..");
const ANALYZE_JSON = resolve(ROOT, ".next/analyze/__bundle_analysis.json");
const BUDGET_JSON = resolve(ROOT, "docs/performance-budget.json");

const SOFT_TARGET_KB = Number(process.env.BUNDLE_SOFT_TARGET_KB ?? "180");
const HARD_ALERT_KB = Number(process.env.BUNDLE_HARD_ALERT_KB ?? "200");
const REGRESSION_PCT = 10; // % growth that fails CI

function bytesToKb(bytes) {
  return Math.round((bytes / 1024) * 10) / 10;
}

async function readJson(path) {
  try {
    const buf = await readFile(path, "utf8");
    return JSON.parse(buf);
  } catch {
    return null;
  }
}

async function main() {
  const analyze = await readJson(ANALYZE_JSON);
  if (!analyze) {
    process.stdout.write(
      `bundle-diff: no analyze output found at ${relative(process.cwd(), ANALYZE_JSON)} — run \`pnpm analyze\` first.\n`,
    );
    process.exit(2);
  }

  // `@next/bundle-analyzer` produces a top-level array of pages; the
  // First Load JS metric we track is the root authenticated route. The
  // structure has changed across Next major versions; tolerate either.
  const rootEntry =
    analyze.find?.((entry) => entry.route === "/") ??
    analyze.pages?.["/"] ??
    null;
  if (!rootEntry) {
    process.stdout.write("bundle-diff: could not locate the `/` route in analyzer output.\n");
    process.exit(2);
  }
  const firstLoadKb =
    typeof rootEntry.firstLoadJs === "number"
      ? bytesToKb(rootEntry.firstLoadJs)
      : Number(rootEntry.firstLoad ?? rootEntry.size ?? 0);

  const budget = await readJson(BUDGET_JSON);
  const baselineKb = budget?.firstLoadJsKb ?? null;

  const lines = [];
  lines.push(`First Load JS @ \`/\` — ${firstLoadKb.toFixed(1)} KB gzipped`);
  if (baselineKb != null) {
    const deltaKb = firstLoadKb - baselineKb;
    const deltaPct = (deltaKb / baselineKb) * 100;
    lines.push(
      `Baseline: ${baselineKb.toFixed(1)} KB · Δ ${deltaKb >= 0 ? "+" : ""}${deltaKb.toFixed(1)} KB (${deltaPct >= 0 ? "+" : ""}${deltaPct.toFixed(1)}%)`,
    );
  } else {
    lines.push("Baseline: (not yet recorded — first run)");
  }
  lines.push(`Soft target: ${SOFT_TARGET_KB} KB · Hard alert: ${HARD_ALERT_KB} KB`);

  let failed = false;
  if (firstLoadKb >= HARD_ALERT_KB) {
    lines.push(`🚨 Hard alert: First Load JS ≥ ${HARD_ALERT_KB} KB — blocks merge.`);
    failed = true;
  } else if (firstLoadKb >= SOFT_TARGET_KB) {
    lines.push(`⚠️  Above soft target ${SOFT_TARGET_KB} KB — requires reviewer acknowledgement.`);
  } else {
    lines.push(`✅ Within soft target.`);
  }
  if (baselineKb != null) {
    const deltaPct = ((firstLoadKb - baselineKb) / baselineKb) * 100;
    if (deltaPct > REGRESSION_PCT) {
      lines.push(`🚨 Regression: +${deltaPct.toFixed(1)}% vs baseline (> ${REGRESSION_PCT}% threshold).`);
      failed = true;
    }
  }

  const report = lines.join("\n");
  process.stdout.write(`${report}\n`);

  // Surface the report to GitHub Actions when running in CI.
  const githubOutput = process.env.GITHUB_STEP_SUMMARY;
  if (githubOutput) {
    await writeFile(githubOutput, `\n### Bundle diff\n\n${report}\n`, { flag: "a" });
  }

  process.exit(failed ? 1 : 0);
}

main().catch((err) => {
  process.stderr.write(`bundle-diff crashed: ${err.stack ?? err}\n`);
  process.exit(2);
});
