/**
 * Typed fetch wrapper (FR-039 / SC-014).
 *
 * Every UI-originated HTTP request flows through this helper so:
 *   - `traceparent` is always attached (regardless of which adapter is
 *     active), enabling backend correlation in Azure Monitor.
 *   - An inbound `tracestate` is forwarded unchanged when supplied.
 *   - The active adapter is notified via a `custom` event for visibility.
 *
 * Feature code MUST consume `httpFetch` (or `httpJson` for JSON endpoints)
 * instead of calling `globalThis.fetch` directly. A future lint rule will
 * enforce this once contracted API surfaces land in spec 002.
 */

import { getAdapter, type CustomEventAttributes } from "@/lib/observability/adapter";
import {
  newTraceContext,
  serializeTraceparent,
  type TraceContext,
} from "./trace-context";

export interface HttpFetchOptions extends Omit<RequestInit, "headers"> {
  readonly headers?: HeadersInit;
  /**
   * Pass an existing trace context to attach an outbound request to an
   * in-flight UI operation. Defaults to a fresh trace context for each call.
   */
  readonly trace?: TraceContext;
  /**
   * Forward the supplied `tracestate` header along with `traceparent`.
   */
  readonly tracestate?: string;
  /**
   * Optional logical operation name forwarded to the observability adapter
   * as a `custom` event. Defaults to the request method + URL pathname.
   */
  readonly operation?: string;
  /**
   * Correlation IDs forwarded to the observability adapter on the emitted
   * `custom` event. PII is structurally rejected by the adapter contract.
   */
  readonly correlationIds?: readonly string[];
}

function operationName(input: RequestInfo | URL, init?: RequestInit, override?: string): string {
  if (override) return override;
  const method = init?.method ?? "GET";
  if (typeof input === "string") return `${method} ${input}`;
  if (input instanceof URL) return `${method} ${input.pathname}`;
  return `${method} ${input.url}`;
}

/**
 * Issue a fetch with W3C Trace Context propagation. Returns the raw Response
 * — callers handle status codes, JSON parsing, etc.
 */
export async function httpFetch(
  input: RequestInfo | URL,
  options: HttpFetchOptions = {},
): Promise<Response> {
  const { trace, tracestate, operation, correlationIds, headers, ...rest } = options;
  const ctx = trace ?? newTraceContext();
  const merged = new Headers(headers);
  merged.set("traceparent", serializeTraceparent(ctx));
  if (tracestate) {
    merged.set("tracestate", tracestate);
  }

  const adapter = getAdapter();
  const customAttributes: CustomEventAttributes = correlationIds
    ? {
        name: `http.${operationName(input, rest as RequestInit, operation)}`,
        correlationIds,
      }
    : { name: `http.${operationName(input, rest as RequestInit, operation)}` };
  adapter.capture({
    kind: "custom",
    trace: ctx,
    attributes: customAttributes,
  });

  return fetch(input, { ...rest, headers: merged });
}

/**
 * Issue a JSON request and return the parsed body. Throws on non-2xx
 * responses with the response status attached so callers can branch on it.
 */
export async function httpJson<T>(
  input: RequestInfo | URL,
  options: HttpFetchOptions = {},
): Promise<T> {
  const headers = new Headers(options.headers);
  if (!headers.has("accept")) headers.set("accept", "application/json");
  if (
    options.body !== undefined &&
    options.body !== null &&
    !headers.has("content-type") &&
    typeof options.body === "string"
  ) {
    headers.set("content-type", "application/json");
  }
  const response = await httpFetch(input, { ...options, headers });
  if (!response.ok) {
    const error = new Error(
      `HTTP ${response.status} ${response.statusText} from ${operationName(input, options as RequestInit, options.operation)}`,
    ) as Error & { status?: number };
    error.status = response.status;
    throw error;
  }
  return (await response.json()) as T;
}
