/**
 * Spec 006 / T060. Cross-language schema parity guard.
 *
 * Asserts the Zod schemas in `web/lib/registry/schemas.ts` agree with the
 * canonical JSON schemas in
 * `specs/006-service-bus-registry-core/contracts/`. Drift here means a wire
 * shape has fragmented across the API contract, the FluentValidation rules,
 * and the Zod schemas — a class of bug that only surfaces in production.
 *
 * Comparison scope is intentionally narrow — required-field set + enum
 * values. Full structural equality would force every additive Zod refinement
 * (like `.min()`) to be mirrored in the JSON schema, which is not the goal
 * (the JSON schema is the floor; Zod refines on top).
 *
 * Runs as part of `pnpm test` (vitest auto-discovers this file).
 */

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { z } from "zod";

import {
  auditEventSchema,
  conflictResponseSchema,
  registryEntitySchema,
} from "../schemas";

const contractsDir = resolve(
  process.cwd(),
  "../specs/006-service-bus-registry-core/contracts",
);

function loadCanonical(filename: string): {
  required?: string[];
  properties?: Record<string, { enum?: string[] }>;
} {
  const path = resolve(contractsDir, filename);
  return JSON.parse(readFileSync(path, "utf8")) as never;
}

function toJson(schema: z.ZodTypeAny): {
  required?: string[];
  properties?: Record<string, { enum?: string[] }>;
} {
  // Zod 4: z.toJSONSchema produces a Draft 2020-12 envelope.
  return z.toJSONSchema(schema) as never;
}

describe("shared-schema parity (T060)", () => {
  describe("registry-entity", () => {
    const canonical = loadCanonical("registry-entity.schema.json");
    const zod = toJson(registryEntitySchema);

    it("declares every canonical-required property", () => {
      const zodKeys = new Set(Object.keys(zod.properties ?? {}));
      const missing = (canonical.required ?? []).filter((k) => !zodKeys.has(k));
      expect(missing, `Zod schema missing canonical-required properties: ${missing.join(", ")}`).toEqual([]);
    });

    it("entityType enum is identical to the canonical set", () => {
      const zodEntityType = new Set(zod.properties?.entityType?.enum ?? []);
      const canonicalEntityType = new Set(canonical.properties?.entityType?.enum ?? []);
      expect(zodEntityType).toEqual(canonicalEntityType);
    });

    it("status enum is identical to the canonical set", () => {
      const zodStatus = new Set(zod.properties?.status?.enum ?? []);
      const canonicalStatus = new Set(canonical.properties?.status?.enum ?? []);
      expect(zodStatus).toEqual(canonicalStatus);
    });

    it("source enum is identical to the canonical set", () => {
      const zodSource = new Set(zod.properties?.source?.enum ?? []);
      const canonicalSource = new Set(canonical.properties?.source?.enum ?? []);
      expect(zodSource).toEqual(canonicalSource);
    });
  });

  describe("conflict-response", () => {
    const canonical = loadCanonical("conflict-response.schema.json");
    const zod = toJson(conflictResponseSchema);

    it("declares every canonical-required property", () => {
      const zodKeys = new Set(Object.keys(zod.properties ?? {}));
      const missing = (canonical.required ?? []).filter((k) => !zodKeys.has(k));
      expect(missing).toEqual([]);
    });
  });

  describe("audit-event", () => {
    const canonical = loadCanonical("audit-event.schema.json");
    const zod = toJson(auditEventSchema);

    it("declares every canonical-required property", () => {
      const zodKeys = new Set(Object.keys(zod.properties ?? {}));
      const missing = (canonical.required ?? []).filter((k) => !zodKeys.has(k));
      expect(missing).toEqual([]);
    });

    it("entityType enum is identical to the canonical set", () => {
      const zodEntityType = new Set(zod.properties?.entityType?.enum ?? []);
      const canonicalEntityType = new Set(canonical.properties?.entityType?.enum ?? []);
      expect(zodEntityType).toEqual(canonicalEntityType);
    });

    it("eventType enum is identical to the canonical set", () => {
      const zodEventType = new Set(zod.properties?.eventType?.enum ?? []);
      const canonicalEventType = new Set(canonical.properties?.eventType?.enum ?? []);
      expect(zodEventType).toEqual(canonicalEventType);
    });
  });
});
