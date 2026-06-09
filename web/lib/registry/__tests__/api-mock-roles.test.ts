import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import "@/lib/observability/register-adapters";

import { createEntity, listEntities, updateEntity } from "../api";
import type { RegistryEntityCreateRequest, RegistryEntityUpdateRequest } from "../schemas";
import { E2E_MOCK_ROLES_HEADER, E2E_PERSONA_SESSION_KEY } from "@/tests/auth/personas";

// Spec 007 regression — `lib/registry/api.ts` predates `lib/api-client.ts`
// and has its own thin `httpFetch` wrapper. When the playwright fixture
// seeds a persona via sessionStorage, every outbound CRUD call must
// attach `X-Mock-Roles` so the backend's MockAuthenticationHandler
// synthesises a principal with the expected roles. Without the header,
// MutateDomain operations (create/update/delete) come back as 403 and
// the create-namespace E2E never navigates off the form.

describe("registry/api X-Mock-Roles wiring", () => {
  const originalFetch = globalThis.fetch;
  const originalAuthMode = process.env.NEXT_PUBLIC_AUTH_MODE;

  beforeEach(() => {
    process.env.NEXT_PUBLIC_AUTH_MODE = "mock";
    window.sessionStorage.clear();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    process.env.NEXT_PUBLIC_AUTH_MODE = originalAuthMode;
    window.sessionStorage.clear();
    vi.restoreAllMocks();
  });

  function stubFetch(responseBody: unknown, status = 200) {
    const seen: { url: string; init: RequestInit | undefined } = { url: "", init: undefined };
    globalThis.fetch = vi.fn(async (input: Request | string | URL, init?: RequestInit) => {
      seen.url = typeof input === "string" ? input : (input as Request).url ?? String(input);
      seen.init = init;
      return new Response(JSON.stringify(responseBody), {
        status,
        headers: { "content-type": "application/json", etag: "etag-1" },
      });
    }) as typeof fetch;
    return seen;
  }

  const minimalEntityPayload: RegistryEntityCreateRequest = {
    id: "11111111-1111-1111-1111-111111111111",
    entityType: "Namespace",
    name: "orders",
    environment: "dev",
    status: "Active",
    source: "Manual",
    tags: [],
  };

  // CRUD tests only care that the header lands on the outbound request.
  // We swallow the post-call Zod validation since our stub body is not a
  // fully-shaped entity — adding response fixtures here would shadow the
  // single behaviour under test (header wiring) with maintenance noise.
  async function suppress<T>(p: Promise<T>): Promise<void> {
    try {
      await p;
    } catch {
      // Intentional — schema/validation paths are out of scope here.
    }
  }

  it("attaches X-Mock-Roles on createEntity when an operator persona is seeded", async () => {
    window.sessionStorage.setItem(E2E_PERSONA_SESSION_KEY, "operator");
    const seen = stubFetch({});

    await suppress(createEntity(minimalEntityPayload));

    const headers = new Headers(seen.init?.headers ?? {});
    expect(headers.get(E2E_MOCK_ROLES_HEADER)).toBe("BusTerminal.Operator");
  });

  it("attaches X-Mock-Roles on updateEntity", async () => {
    window.sessionStorage.setItem(E2E_PERSONA_SESSION_KEY, "admin");
    const seen = stubFetch({});

    const updateBody = minimalEntityPayload as unknown as RegistryEntityUpdateRequest;
    await suppress(updateEntity(minimalEntityPayload.id, updateBody, "etag-0"));

    const headers = new Headers(seen.init?.headers ?? {});
    expect(headers.get(E2E_MOCK_ROLES_HEADER)).toBe("BusTerminal.Admin");
  });

  it("attaches X-Mock-Roles on read paths too (listEntities)", async () => {
    window.sessionStorage.setItem(E2E_PERSONA_SESSION_KEY, "reader");
    const seen = stubFetch({ items: [], continuationToken: null });

    await listEntities({ environment: "dev" });

    const headers = new Headers(seen.init?.headers ?? {});
    expect(headers.get(E2E_MOCK_ROLES_HEADER)).toBe("BusTerminal.Reader");
  });

  it("omits X-Mock-Roles when no persona is seeded", async () => {
    const seen = stubFetch({ items: [], continuationToken: null });

    await listEntities({ environment: "dev" });

    const headers = new Headers(seen.init?.headers ?? {});
    expect(headers.get(E2E_MOCK_ROLES_HEADER)).toBeNull();
  });

  it("omits X-Mock-Roles when NEXT_PUBLIC_AUTH_MODE is not mock", async () => {
    process.env.NEXT_PUBLIC_AUTH_MODE = "msal";
    window.sessionStorage.setItem(E2E_PERSONA_SESSION_KEY, "operator");
    const seen = stubFetch({ items: [], continuationToken: null });

    await listEntities({ environment: "dev" });

    const headers = new Headers(seen.init?.headers ?? {});
    expect(headers.get(E2E_MOCK_ROLES_HEADER)).toBeNull();
  });
});
