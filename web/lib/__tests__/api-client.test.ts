import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { apiGet } from "@/lib/api-client";
import "@/lib/observability/register-adapters";

describe("apiGet", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    process.env.NEXT_PUBLIC_API_BASE_URL = "http://localhost:8080";
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("attaches Authorization bearer token and traceparent header", async () => {
    const seen: { url: string; init: RequestInit | undefined } = {
      url: "",
      init: undefined,
    };
    globalThis.fetch = vi.fn(async (input: Request | string | URL, init?: RequestInit) => {
      seen.url = typeof input === "string" ? input : (input as Request).url ?? String(input);
      seen.init = init;
      return new Response(JSON.stringify({ pong: true }), {
        status: 200,
        headers: { "content-type": "application/json" },
      });
    }) as typeof fetch;

    const result = await apiGet<{ pong: boolean }>("/whoami", {
      accessToken: "test-token",
    });

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.data.pong).toBe(true);
    }

    const headers = new Headers(seen.init?.headers ?? {});
    expect(headers.get("authorization")).toBe("Bearer test-token");
    expect(headers.get("traceparent")).toMatch(
      /^00-[0-9a-f]{32}-[0-9a-f]{16}-(00|01)$/,
    );
    expect(seen.url).toBe("http://localhost:8080/whoami");
  });

  it("surfaces a 401 response as an 'unauthenticated' error", async () => {
    globalThis.fetch = vi.fn(async () =>
      new Response("", {
        status: 401,
        headers: { "www-authenticate": "Bearer realm=\"busterminal\"" },
      }),
    ) as typeof fetch;

    const result = await apiGet<unknown>("/whoami");
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toBe("unauthenticated");
      expect(result.traceparent).toMatch(/^00-[0-9a-f]{32}-[0-9a-f]{16}-(00|01)$/);
    }
  });

  it("surfaces network failures as 'network-error'", async () => {
    globalThis.fetch = vi.fn(async () => {
      throw new TypeError("Failed to fetch");
    }) as typeof fetch;

    const result = await apiGet<unknown>("/whoami");
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toBe("network-error");
    }
  });
});
