/**
 * Typed API client for the BusTerminal backend.
 *
 * Wraps `httpFetch` (which already injects W3C `traceparent` and emits a
 * custom observability event) and adds:
 *   - `Authorization: Bearer <session.accessToken>` from the active Auth.js session
 *   - 401 handling that surfaces a discriminated-union result so callers do
 *     not have to throw/catch
 *   - JSON parsing into a typed `data` field
 *
 * Server-component callers should pass an explicit session (fetched via
 * `auth()` in the calling component) so we don't reach for `auth()` at module
 * import time and accidentally couple `lib/auth.ts` to non-server code paths.
 */

import { httpFetch, type HttpFetchOptions } from "@/lib/http/client";
import { generateTraceparent } from "@/lib/telemetry/trace-context";

const TRACE_HEADER_NAME = "traceparent";

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
  const { accessToken, headers: userHeaders, init, ...rest } = options;
  const headers: Record<string, string> = {
    accept: "application/json",
    ...(userHeaders ?? {}),
  };

  if (accessToken) {
    headers.authorization = `Bearer ${accessToken}`;
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
