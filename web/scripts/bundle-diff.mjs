#!/usr/bin/env node
/**
 * bundle-diff (T040 / FR-035e / SC-020).
 *
 * Compares the gzipped First Load JS of the `/` route against the committed
 * baseline at `web/docs/performance-budget.json`. Posts a PR comment with
 * red/green status against the soft / hard thresholds from the env vars
 * (`BUNDLE_SOFT_TARGET_KB`, `BUNDLE_HARD_ALERT_KB`).
 *
 * Fails the PR check when:
 *
 *   - First Load JS grew by > +10% relative to the recorded baseline, OR
 *   - First Load JS crossed the hard alert threshold.
 *
 * Input source: `.next/diagnostics/route-bundle-stats.json`, produced by
 * every `next build` on Next.js ≥ 16 regardless of bundler. The older
 * `@next/bundle-analyzer` JSON output is webpack-only and is not produced
 * under Turbopack (Next 16's default builder).
 *
 * Inputs are read by path so the script runs the same way locally and in CI.
 */

import { readFile, writeFile } from "node:fs/promises";
import { gzip } from "node:zlib";
import { promisify } from "node:util";
import { resolve, relative } from "node:path";

const gzipP = promisify(gzip);

const ROOT = resolve(import.meta.dirname, "..");
const ROUTE_STATS = resolve(ROOT, ".next/diagnostics/route-bundle-stats.json");
const BUDGET_JSON = resolve(ROOT, "docs/performance-budget.json");

const SOFT_TARGET_KB = Number(process.env.BUNDLE_SOFT_TARGET_KB ?? "180");
const HARD_ALERT_KB = Number(process.env.BUNDLE_HARD_ALERT_KB ?? "200");
const REGRESSION_PCT = 10;

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

async function gzippedSize(absPath) {
  const buf = await readFile(absPath);
  const compressed = await gzipP(buf, { level: 9 });
  return compressed.length;
}

async function main() {
  const stats = await readJson(ROUTE_STATS);
  if (!stats) {
    process.stdout.write(
      `bundle-diff: no route-bundle-stats found at ${relative(process.cwd(), ROUTE_STATS)} — run \`pnpm analyze\` (or any \`next build\`) first.\n`,
    );
    process.exit(2);
  }

  const rootEntry = stats.find?.((entry) => entry.route === "/");
  if (!rootEntry) {
    process.stdout.write("bundle-diff: could not locate the `/` route in route-bundle-stats.json.\n");
    process.exit(2);
  }

  const chunks = rootEntry.firstLoadChunkPaths ?? [];
  let totalGzipBytes = 0;
  const missing = [];
  for (const rel of chunks) {
    const abs = resolve(ROOT, rel);
    try {
      totalGzipBytes += await gzippedSize(abs);
    } catch {
      missing.push(rel);
    }
  }
  if (missing.length > 0) {
    process.stdout.write(
      `bundle-diff: ${missing.length} of ${chunks.length} chunk files missing on disk — measurement may underreport.\n`,
    );
  }
  const firstLoadKb = bytesToKb(totalGzipBytes);

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
