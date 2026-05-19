/**
 * Slice 002 telemetry entry-point for W3C Trace Context generation.
 *
 * Slice 001 already ships the byte-level primitives in
 * `web/lib/http/trace-context.ts`. This module re-exports them and adds the
 * higher-level helpers spec'd in tasks T025: `generateTraceparent()` (single
 * call producing a header value) and `getOrCreateActiveTraceContext()` (used
 * by `api-client` to attach correlation IDs to outbound fetches even when
 * no observability adapter is active — FR-022).
 */

import {
  newTraceContext,
  serializeTraceparent,
  type TraceContext,
} from "@/lib/http/trace-context";

export {
  parseTraceparent,
  serializeTraceparent,
  newTraceContext,
  type TraceContext,
  type TraceId,
  type SpanId,
  type TraceFlags,
} from "@/lib/http/trace-context";

let activeContext: TraceContext | null = null;

export function generateTraceparent(): string {
  return serializeTraceparent(newTraceContext());
}

export function getOrCreateActiveTraceContext(): TraceContext {
  if (activeContext) return activeContext;
  activeContext = newTraceContext();
  return activeContext;
}

export function __resetActiveTraceContextForTests(): void {
  activeContext = null;
}
