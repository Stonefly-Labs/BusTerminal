/**
 * Schema-conformance test for `PERSONA_CONFIGS`.
 *
 * The persona-config JSON Schema at
 * `specs/007-playwright-auth-fixture/contracts/persona-config.schema.json`
 * is the binding cross-language contract for what a persona config looks
 * like. This test encodes the same shape as a Zod schema and asserts every
 * runtime `PERSONA_CONFIGS` entry conforms.
 *
 * Why Zod and not Ajv: `zod` is already in the web deps; pulling Ajv just
 * to validate four objects against a 2020-12 schema would add an
 * unnecessary dependency. The schema's invariants are simple enough that
 * the Zod encoding is a faithful translation.
 *
 * If you change the JSON Schema, change this validator too — both must
 * stay in lockstep.
 */

import { describe, expect, it } from "vitest";
import { z } from "zod";

import { PERSONA_CONFIGS, PERSONA_NAMES } from "@/tests/auth/personas";

const personaSchema = z.enum(["reader", "operator", "admin", "none"]);
const roleSchema = z.enum([
  "BusTerminal.Reader",
  "BusTerminal.Developer",
  "BusTerminal.Operator",
  "BusTerminal.Admin",
]);

const mockAccountSchema = z
  .object({
    oid: z.string().regex(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/),
    upn: z.string().regex(/^e2e-(reader|operator|admin|none)@mock\.busterminal\.dev$/),
    displayName: z.string().regex(/^E2E (Reader|Operator|Admin|No-Role)$/),
  })
  .strict();

const personaConfigSchema = z
  .object({
    persona: personaSchema,
    mockAccount: mockAccountSchema,
    expectedRoleAssignments: z.array(roleSchema),
    storageStatePath: z.string().regex(/^tests\/\.auth\/(reader|operator|admin|none)\.json$/),
  })
  .strict();

describe("PERSONA_CONFIGS", () => {
  it("covers every persona name in the enum", () => {
    const configKeys = Object.keys(PERSONA_CONFIGS).sort();
    const enumNames = [...PERSONA_NAMES].sort();
    expect(configKeys).toEqual(enumNames);
  });

  it.each(PERSONA_NAMES)("config for %s conforms to the JSON-schema contract", (persona) => {
    const config = PERSONA_CONFIGS[persona];
    const parsed = personaConfigSchema.safeParse(config);
    expect(parsed.success, parsed.error?.message).toBe(true);
  });

  it("ties persona keys to their own persona field", () => {
    for (const persona of PERSONA_NAMES) {
      expect(PERSONA_CONFIGS[persona].persona).toBe(persona);
    }
  });

  it("ties mockAccount UPN/displayName to the persona name", () => {
    expect(PERSONA_CONFIGS.reader.mockAccount.upn).toBe("e2e-reader@mock.busterminal.dev");
    expect(PERSONA_CONFIGS.reader.mockAccount.displayName).toBe("E2E Reader");
    expect(PERSONA_CONFIGS.operator.mockAccount.upn).toBe("e2e-operator@mock.busterminal.dev");
    expect(PERSONA_CONFIGS.operator.mockAccount.displayName).toBe("E2E Operator");
    expect(PERSONA_CONFIGS.admin.mockAccount.upn).toBe("e2e-admin@mock.busterminal.dev");
    expect(PERSONA_CONFIGS.admin.mockAccount.displayName).toBe("E2E Admin");
    expect(PERSONA_CONFIGS.none.mockAccount.upn).toBe("e2e-none@mock.busterminal.dev");
    expect(PERSONA_CONFIGS.none.mockAccount.displayName).toBe("E2E No-Role");
  });

  it("gives every persona a unique stable OID", () => {
    const oids = PERSONA_NAMES.map((p) => PERSONA_CONFIGS[p].mockAccount.oid);
    expect(new Set(oids).size).toBe(oids.length);
  });

  it("ties storageState paths to the persona name", () => {
    for (const persona of PERSONA_NAMES) {
      expect(PERSONA_CONFIGS[persona].storageStatePath).toBe(`tests/.auth/${persona}.json`);
    }
  });

  it("assigns the 'none' persona zero expected roles", () => {
    expect(PERSONA_CONFIGS.none.expectedRoleAssignments).toEqual([]);
  });

  it("assigns each role-bearing persona exactly its eponymous BusTerminal role", () => {
    expect(PERSONA_CONFIGS.reader.expectedRoleAssignments).toEqual(["BusTerminal.Reader"]);
    expect(PERSONA_CONFIGS.operator.expectedRoleAssignments).toEqual(["BusTerminal.Operator"]);
    expect(PERSONA_CONFIGS.admin.expectedRoleAssignments).toEqual(["BusTerminal.Admin"]);
  });
});
