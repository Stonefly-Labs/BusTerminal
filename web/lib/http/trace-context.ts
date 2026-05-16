/**
 * W3C Trace Context primitives (FR-039 / SC-014).
 *
 * Implements the trace-context portion of
 * `specs/001-brand-system-and-design-foundation/contracts/observability-adapter.ts`.
 *
 * `traceparent` MUST accompany every UI-originated HTTP request, regardless
 * of whether an observability adapter is configured. The `http/client.ts`
 * wrapper enforces injection; `parseTraceparent` / `serializeTraceparent`
 * provide the byte-level encoding.
 */

/** 32 hex characters per W3C Trace Context. */
export type TraceId = string & { readonly __brand: "TraceId" };

/** 16 hex characters per W3C Trace Context. */
export type SpanId = string & { readonly __brand: "SpanId" };

/** "01" = sampled, "00" = not sampled. */
export type TraceFlags = "00" | "01";

export interface TraceContext {
  readonly traceId: TraceId;
  readonly spanId: SpanId;
  readonly traceFlags: TraceFlags;
  /** Forwarded unchanged when the inbound request carried it. */
  readonly tracestate?: string;
}

const HEX_ALPHABET = "0123456789abcdef";

function randomHex(byteLength: number): string {
  if (typeof globalThis.crypto?.getRandomValues === "function") {
    const buffer = new Uint8Array(byteLength);
    globalThis.crypto.getRandomValues(buffer);
    let out = "";
    for (const byte of buffer) {
      out += HEX_ALPHABET[(byte >>> 4) & 0xf];
      out += HEX_ALPHABET[byte & 0xf];
    }
    return out;
  }
  let out = "";
  for (let i = 0; i < byteLength * 2; i += 1) {
    out += HEX_ALPHABET[Math.floor(Math.random() * 16)];
  }
  return out;
}

/**
 * Generates a fresh trace context for a new UI-originated operation. Always
 * marks the trace as sampled (`01`) so frontend events surface in the
 * backend trace if an Application Insights adapter is later activated.
 */
export function newTraceContext(): TraceContext {
  return {
    traceId: randomHex(16) as TraceId,
    spanId: randomHex(8) as SpanId,
    traceFlags: "01",
  };
}

const TRACEPARENT_PATTERN = /^00-([0-9a-f]{32})-([0-9a-f]{16})-([0-9a-f]{2})$/;

/**
 * Parse a `traceparent` header value. Returns `null` for malformed input,
 * a zero trace ID, or a zero span ID — all of which the W3C spec rejects.
 */
export function parseTraceparent(headerValue: string): TraceContext | null {
  const match = TRACEPARENT_PATTERN.exec(headerValue.trim());
  if (!match) return null;
  const [, traceId, spanId, flags] = match;
  if (!traceId || !spanId || !flags) return null;
  if (/^0+$/.test(traceId) || /^0+$/.test(spanId)) return null;
  if (flags !== "00" && flags !== "01") return null;
  return {
    traceId: traceId as TraceId,
    spanId: spanId as SpanId,
    traceFlags: flags,
  };
}

/**
 * Serialize a TraceContext into a W3C `traceparent` header value.
 * Format: `00-<traceId>-<spanId>-<traceFlags>`.
 */
export function serializeTraceparent(ctx: TraceContext): string {
  return `00-${ctx.traceId}-${ctx.spanId}-${ctx.traceFlags}`;
}
