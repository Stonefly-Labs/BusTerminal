#!/usr/bin/env node
/**
 * audit-svg-hygiene (T039 / FR-002a / SC-017).
 *
 * Scans every committed SVG under `web/brand/**` and `web/public/brand/**`
 * for embedded raster content. Plain SVG only — no `<image>` tags, no
 * base64 `data:` URIs, no external `xlink:href` raster references.
 */

import { readFile } from "node:fs/promises";
import { resolve, relative } from "node:path";
import { glob } from "node:fs/promises";

const ROOT = resolve(import.meta.dirname, "..");

const INCLUDE_PATTERNS = ["brand/**/*.svg", "public/brand/**/*.svg"];

const VIOLATIONS = [
  {
    name: "image-tag",
    pattern: /<image\b/i,
    description: "Embedded <image> tag — plain SVG must use vector primitives only.",
  },
  {
    name: "base64-data-uri",
    pattern: /data:[^"']*;base64,/i,
    description: "Base64 data: URI — embedded raster bytes are not permitted.",
  },
  {
    name: "raster-href",
    pattern: /(?:href|xlink:href)=["'][^"']+\.(?:png|jpe?g|gif|webp|bmp|tiff?)["']/i,
    description: "External raster reference — replace with a vector path.",
  },
];

async function main() {
  let violations = 0;
  for (const pattern of INCLUDE_PATTERNS) {
    for await (const file of glob(pattern, { cwd: ROOT })) {
      const absolute = resolve(ROOT, file);
      const contents = await readFile(absolute, "utf8");
      for (const rule of VIOLATIONS) {
        const match = rule.pattern.exec(contents);
        if (match) {
          const offset = match.index;
          const line = contents.slice(0, offset).split(/\r?\n/).length;
          violations += 1;
          process.stdout.write(
            `${relative(process.cwd(), absolute)}:${line} ${rule.name} — ${rule.description}\n`,
          );
        }
      }
    }
  }
  if (violations > 0) {
    process.stdout.write(`\naudit:svg-hygiene — ${violations} violation(s)\n`);
    process.exit(1);
  }
  process.stdout.write("audit:svg-hygiene — clean.\n");
}

main().catch((err) => {
  process.stderr.write(`audit:svg-hygiene crashed: ${err.stack ?? err}\n`);
  process.exit(2);
});
