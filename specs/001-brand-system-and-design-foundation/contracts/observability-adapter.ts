/**
 * Observability Adapter Contract
 *
 * Spec references:
 *   - FR-036 (top-level error boundary forwarding)
 *   - FR-037 (Web Vitals capture)
 *   - FR-038 (route-change span)
 *   - FR-039 (W3C Trace Context propagation — independent of adapter config)
 *   - FR-040 (adapter interface + no-op + Application Insights implementations)
 *   - FR-041 (no PII; only correlation IDs)
 *   - SC-013 / SC-014 / SC-015 / SC-016
 *
 * This file defines the contract surface. Feature code depends on the
 * interface, never on a specific adapter implementation. The no-op adapter
 * is the default; the Application Insights adapter is activated when
 * NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING is set.
 *
 * The PII hygiene rule (FR-041) is enforced at the type level: no attribute
 * shape declared below contains a free-form string-record. Feature code that
 * tries to attach unsanctioned attributes fails type-checking.
 */

// -----------------------------------------------------------------------------
// W3C Trace Context primitives
// -----------------------------------------------------------------------------

/** 32 hex characters per RFC 9110 / W3C Trace Context. */
export type TraceId = string & { readonly __brand: 'TraceId' };

/** 16 hex characters per W3C Trace Context. */
export type SpanId = string & { readonly __brand: 'SpanId' };

/** 2 hex characters; "01" = sampled, "00" = not sampled. */
export type TraceFlags = '00' | '01';

export interface TraceContext {
  readonly traceId: TraceId;
  readonly spanId: SpanId;
  readonly traceFlags: TraceFlags;
  /** Forwarded unchanged when the inbound request carried it. */
  readonly tracestate?: string;
}

// -----------------------------------------------------------------------------
// Sanctioned event shapes (PII is structurally impossible by these types)
// -----------------------------------------------------------------------------

export interface ErrorEventAttributes {
  readonly message: string;
  readonly category:
    | 'render'
    | 'unhandled-promise'
    | 'route-load'
    | 'data-fetch'
    | 'observability-self';
  readonly componentStack?: string;
  readonly route?: string;
}

export type WebVitalName = 'LCP' | 'INP' | 'CLS' | 'TTFB' | 'FCP';

export interface WebVitalEventAttributes {
  readonly name: WebVitalName;
  readonly value: number;
  readonly id: string;
  readonly navigationType:
    | 'navigate'
    | 'reload'
    | 'back-forward'
    | 'back-forward-cache'
    | 'prerender'
    | 'restore';
}

export interface RouteChangeEventAttributes {
  readonly fromRoute: string;
  readonly toRoute: string;
  readonly durationMs: number;
}

export interface CustomEventAttributes {
  readonly name: string;
  readonly correlationIds?: readonly string[];
}

export type ObservabilityEvent =
  | { kind: 'error'; trace: TraceContext; attributes: ErrorEventAttributes }
  | { kind: 'web-vital'; trace: TraceContext; attributes: WebVitalEventAttributes }
  | { kind: 'route-change'; trace: TraceContext; attributes: RouteChangeEventAttributes }
  | { kind: 'custom'; trace: TraceContext; attributes: CustomEventAttributes };

// -----------------------------------------------------------------------------
// Adapter interface (FR-040)
// -----------------------------------------------------------------------------

export interface ObservabilityAdapter {
  /** Stable, machine-readable identifier — e.g., "noop" or "application-insights". */
  readonly name: 'noop' | 'application-insights';

  /** Whether the adapter is actively forwarding to a backend. */
  readonly isActive: boolean;

  /**
   * Initialize the adapter. The no-op adapter is a no-op here; the Application
   * Insights adapter dynamically imports the AI SDK and initializes it with
   * the configured connection string.
   *
   * MUST resolve even when the adapter is not configured.
   */
  init(): Promise<void>;

  /**
   * Forward an observability event. The adapter MUST NOT throw — failures
   * inside the adapter are caught and emitted as a `category: 'observability-self'`
   * error event on the next forwarding cycle.
   */
  capture(event: ObservabilityEvent): void;

  /**
   * Flush any buffered events. Called by the page-hide handler and on shutdown.
   * MUST resolve even if no backend is configured.
   */
  flush(): Promise<void>;
}

// -----------------------------------------------------------------------------
// Selector — wired at module load
// -----------------------------------------------------------------------------

/**
 * Returns the active adapter. The selection is made once per browser session
 * based on the presence of NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING.
 *
 * Implementations:
 *   - lib/observability/noop-adapter.ts
 *   - lib/observability/app-insights-adapter.ts (dynamic-imports the AI SDK)
 */
export declare function getAdapter(): ObservabilityAdapter;

// -----------------------------------------------------------------------------
// W3C Trace Context propagation (FR-039 — independent of adapter)
// -----------------------------------------------------------------------------

/**
 * Generates a fresh trace context for a new UI-originated operation.
 * MUST be called regardless of whether the adapter is the no-op or AI variant.
 */
export declare function newTraceContext(): TraceContext;

/**
 * Parses a `traceparent` header value into a TraceContext, returning null
 * for malformed input. Used when forwarding an existing span from an
 * upstream caller (currently only relevant for SSR / RSC originated calls).
 */
export declare function parseTraceparent(headerValue: string): TraceContext | null;

/**
 * Serializes a TraceContext into a W3C `traceparent` header value.
 * Format: `00-<traceId>-<spanId>-<traceFlags>`.
 */
export declare function serializeTraceparent(ctx: TraceContext): string;
