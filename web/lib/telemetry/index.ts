/**
 * Slice 002 telemetry barrel.
 *
 * Re-exports the slice-001 observability adapter selector and the slice-002
 * trace-context helpers. Feature code should import from this module rather
 * than reaching into `@/lib/observability/*` directly so a future
 * relocation of the implementation does not ripple.
 */

export {
  getAdapter,
  registerAdapters,
  __resetAdapterForTests,
  type ObservabilityAdapter,
  type ObservabilityEvent,
  type AdapterName,
  type ErrorEventAttributes,
  type WebVitalEventAttributes,
  type WebVitalName,
  type RouteChangeEventAttributes,
  type CustomEventAttributes,
} from "@/lib/observability/adapter";

export { createNoopAdapter } from "@/lib/observability/noop-adapter";
export { createAppInsightsAdapter } from "@/lib/observability/app-insights-adapter";

export {
  generateTraceparent,
  getOrCreateActiveTraceContext,
  parseTraceparent,
  serializeTraceparent,
  newTraceContext,
  type TraceContext,
} from "./trace-context";
