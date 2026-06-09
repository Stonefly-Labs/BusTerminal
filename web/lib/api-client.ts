/**
 * Typed API client for the BusTerminal backend.
 *
 * - Acquires the API access token from MSAL via `acquireTokenSilent` when the
 *   caller does not pass one explicitly. Falls back to `acquireTokenRedirect`
 *   when interaction is required.
 * - Propagates a W3C `traceparent` header on every request (constitution-bound
 *   trace-context requirement).
 * - On a 401 response (after an MSAL-acquired token), retries the call once
 *   with `forceRefresh: true` to dislodge a stale silent-cache entry.
 *
 * Callers may pass an explicit `accessToken` to bypass MSAL entirely — useful
 * for tests and for any short-lived callers that hold a token directly. When
 * an explicit token is supplied, the 401 retry path is skipped.
 */

import { InteractionRequiredAuthError } from "@azure/msal-browser";

import { msalReady, pca } from "@/lib/auth/msal-instance";
import { API_SCOPE_REQUEST } from "@/lib/auth/scopes";
import { httpFetch, type HttpFetchOptions } from "@/lib/http/client";
import { generateTraceparent } from "@/lib/telemetry/trace-context";
import {
  E2E_MOCK_ROLES_HEADER,
  E2E_PERSONA_SESSION_KEY,
  isPersona,
  PERSONA_CONFIGS,
} from "@/tests/auth/personas";

const TRACE_HEADER_NAME = "traceparent";
const MOCK_AUTH_MODE = "mock";

export type ApiResult<T> =
  | { ok: true; data: T; traceparent: string }
  | { ok: false; error: ApiError; traceparent: string };

export type ApiError =
  | "unauthenticated"
  | "forbidden"
  | "server-error"
  | "network-error"
  | "invalid-response";

export interface ApiCallOptions extends Omit<HttpFetchOptions, "headers"> {
  readonly accessToken?: string;
  readonly headers?: Record<string, string>;
  readonly init?: RequestInit;
}

function readApiBaseUrl(): string {
  const fromEnv = process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!fromEnv || fromEnv.trim().length === 0) {
    return "http://localhost:8080";
  }
  return fromEnv.replace(/\/$/, "");
}

function buildUrl(path: string): string {
  if (path.startsWith("http://") || path.startsWith("https://")) {
    return path;
  }
  const base = readApiBaseUrl();
  const suffix = path.startsWith("/") ? path : `/${path}`;
  return `${base}${suffix}`;
}

/**
 * In mock-auth mode the backend's `MockAuthenticationHandler` reads the
 * `X-Mock-Roles` header (comma-separated `BusTerminal.*` role values) to
 * synthesise the request principal. We resolve the active persona from
 * sessionStorage on every call so a re-seeded persona is picked up
 * without a SPA restart. Returns `null` when not in mock mode, when no
 * persona is seeded, or when running server-side (the api-client is
 * client-only in practice; the guard is defensive).
 */
function resolveMockRolesHeaderValue(): string | null {
  if (process.env.NEXT_PUBLIC_AUTH_MODE !== MOCK_AUTH_MODE) {
    return null;
  }
  if (typeof window === "undefined") {
    return null;
  }
  const raw = window.sessionStorage.getItem(E2E_PERSONA_SESSION_KEY);
  if (!isPersona(raw)) {
    return null;
  }
  const roles = PERSONA_CONFIGS[raw].expectedRoleAssignments;
  // Empty role list is intentional for the `none` persona — the backend
  // mock handler reads no header as "no roles", which is the right
  // semantics. We still send the header (empty value) so the request
  // path is identical across personas.
  return roles.join(",");
}

async function acquireAccessToken(forceRefresh: boolean): Promise<string | null> {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    await msalReady;
  } catch {
    return null;
  }
  const account = pca.getActiveAccount() ?? pca.getAllAccounts()[0] ?? null;
  if (!account) {
    return null;
  }
  try {
    const response = await pca.acquireTokenSilent({
      scopes: [...API_SCOPE_REQUEST.scopes],
      account,
      forceRefresh,
    });
    return response.accessToken;
  } catch (err) {
    if (err instanceof InteractionRequiredAuthError) {
      await pca.acquireTokenRedirect({ scopes: [...API_SCOPE_REQUEST.scopes] });
      return null;
    }
    return null;
  }
}

export async function apiGet<T>(
  path: string,
  options: ApiCallOptions = {},
): Promise<ApiResult<T>> {
  return apiCall<T>(path, "GET", options);
}

export async function apiCall<T>(
  path: string,
  method: string,
  options: ApiCallOptions = {},
): Promise<ApiResult<T>> {
  return performCall<T>(path, method, options, false);
}

async function performCall<T>(
  path: string,
  method: string,
  options: ApiCallOptions,
  hasRetried: boolean,
): Promise<ApiResult<T>> {
  const { accessToken: explicitToken, headers: userHeaders, init, ...rest } = options;
  const usingExplicitToken = typeof explicitToken === "string";
  const token = usingExplicitToken ? explicitToken : await acquireAccessToken(false);

  const headers: Record<string, string> = {
    accept: "application/json",
    ...(userHeaders ?? {}),
  };
  if (token) {
    headers.authorization = `Bearer ${token}`;
  }

  const mockRoles = resolveMockRolesHeaderValue();
  if (mockRoles !== null) {
    headers[E2E_MOCK_ROLES_HEADER] = mockRoles;
  }

  const traceparent = headers[TRACE_HEADER_NAME] ?? generateTraceparent();
  headers[TRACE_HEADER_NAME] = traceparent;

  try {
    const response = await httpFetch(buildUrl(path), {
      ...rest,
      ...(init ?? {}),
      method,
      headers,
    });

    if (response.status === 401) {
      if (!hasRetried && !usingExplicitToken) {
        const refreshed = await acquireAccessToken(true);
        if (refreshed) {
          return performCall<T>(path, method, options, true);
        }
      }
      return { ok: false, error: "unauthenticated", traceparent };
    }
    if (response.status === 403) {
      return { ok: false, error: "forbidden", traceparent };
    }
    if (response.status >= 500) {
      return { ok: false, error: "server-error", traceparent };
    }
    if (!response.ok) {
      return { ok: false, error: "server-error", traceparent };
    }

    try {
      const data = (await response.json()) as T;
      return { ok: true, data, traceparent };
    } catch {
      return { ok: false, error: "invalid-response", traceparent };
    }
  } catch {
    return { ok: false, error: "network-error", traceparent };
  }
}

export { readApiBaseUrl };
