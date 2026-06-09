/**
 * Persona definitions for the spec-007 Playwright auth fixture.
 *
 * The persona enum is the sole interface between an E2E spec and the
 * fixture. Each persona maps 1:1 to a synthetic mock identity. The
 * fixture writes the active persona name into sessionStorage; the SPA's
 * mock MSAL instance (`web/lib/auth/msal-mock.ts`) reads that and
 * synthesizes a signed-in `AccountInfo`. The api-client adds an
 * `X-Mock-Roles` header derived from the persona's
 * `expectedRoleAssignments`; the backend's existing
 * `MockAuthenticationHandler` reads that header and synthesizes the
 * matching `ClaimsPrincipal`. No real Entra round-trip occurs.
 *
 * Adding a new persona (e.g. `developer`) requires only:
 *  - Adding the literal to the `Persona` union below.
 *  - Adding a `PERSONA_CONFIGS` entry with a stable `mockAccount.oid` and
 *    the persona's role set.
 *  - Updating the JSON-schema enum at
 *    `specs/007-playwright-auth-fixture/contracts/persona-config.schema.json`.
 *
 * No infrastructure or tenant configuration is required.
 */

export type Persona = "reader" | "operator" | "admin" | "none";

export const PERSONA_NAMES: readonly Persona[] = ["reader", "operator", "admin", "none"] as const;

/**
 * BusTerminal app roles that can appear on a persona's access-token `roles`
 * claim. Matches the spec-003 role catalog. `Developer` is intentionally
 * unused in v1 — none of the suspended specs exercise `DeveloperTooling`-
 * gated UI.
 */
export type RoleClaim =
  | "BusTerminal.Reader"
  | "BusTerminal.Developer"
  | "BusTerminal.Operator"
  | "BusTerminal.Admin";

/**
 * Synthetic identity attached to a persona in mock mode. Used to populate
 * the mock MSAL `AccountInfo` and to surface a recognisable name in logs
 * and telemetry. None of these values are validated against any real
 * directory — they exist purely to give the SPA something to render.
 *
 * `oid` is a stable per-persona GUID (NOT a real Entra object ID) so that
 * trace correlation across runs is deterministic.
 */
export interface MockAccount {
  readonly oid: string;
  readonly upn: string;
  readonly displayName: string;
}

/**
 * Per-persona configuration. Mirrors the JSON Schema at
 * `specs/007-playwright-auth-fixture/contracts/persona-config.schema.json`
 * — when adding fields here, also extend the schema and the Zod validator
 * in `__tests__/personas.config.test.ts`.
 */
export interface PersonaConfig {
  readonly persona: Persona;
  readonly mockAccount: MockAccount;
  readonly expectedRoleAssignments: readonly RoleClaim[];
  readonly storageStatePath: `tests/.auth/${Persona}.json`;
}

export const PERSONA_CONFIGS: Readonly<Record<Persona, PersonaConfig>> = {
  reader: {
    persona: "reader",
    mockAccount: {
      oid: "11111111-1111-1111-1111-111111111101",
      upn: "e2e-reader@mock.busterminal.dev",
      displayName: "E2E Reader",
    },
    expectedRoleAssignments: ["BusTerminal.Reader"] as const,
    storageStatePath: "tests/.auth/reader.json",
  },
  operator: {
    persona: "operator",
    mockAccount: {
      oid: "11111111-1111-1111-1111-111111111102",
      upn: "e2e-operator@mock.busterminal.dev",
      displayName: "E2E Operator",
    },
    expectedRoleAssignments: ["BusTerminal.Operator"] as const,
    storageStatePath: "tests/.auth/operator.json",
  },
  // Provisioned but currently unused — no v1 test consumer. Kept here so
  // an admin-scoped spec authored later can opt in via a single line.
  admin: {
    persona: "admin",
    mockAccount: {
      oid: "11111111-1111-1111-1111-111111111103",
      upn: "e2e-admin@mock.busterminal.dev",
      displayName: "E2E Admin",
    },
    expectedRoleAssignments: ["BusTerminal.Admin"] as const,
    storageStatePath: "tests/.auth/admin.json",
  },
  none: {
    persona: "none",
    mockAccount: {
      oid: "11111111-1111-1111-1111-111111111104",
      upn: "e2e-none@mock.busterminal.dev",
      displayName: "E2E No-Role",
    },
    // Authenticated, role-empty. Used by no-access-experience and
    // unauthorized-state specs.
    expectedRoleAssignments: [] as const,
    storageStatePath: "tests/.auth/none.json",
  },
} as const;

export function getPersonaConfig(persona: Persona): PersonaConfig {
  return PERSONA_CONFIGS[persona];
}

export function isPersona(value: unknown): value is Persona {
  return typeof value === "string" && (PERSONA_NAMES as readonly string[]).includes(value);
}

/**
 * The sessionStorage key the Playwright fixture writes (via
 * `addInitScript`) to tell the mock MSAL instance which persona is
 * active. Centralised here so both the fixture and the SPA's mock PCA
 * stay in sync.
 */
export const E2E_PERSONA_SESSION_KEY = "bt.e2e.persona";

/**
 * The opaque header the api-client emits on every request in mock mode.
 * The backend `MockAuthenticationHandler` reads it as a comma-separated
 * list of role claim values.
 */
export const E2E_MOCK_ROLES_HEADER = "X-Mock-Roles";
