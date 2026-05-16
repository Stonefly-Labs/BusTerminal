/**
 * Observability adapter contract + selector (FR-040).
 *
 * Implements the adapter portion of
 * `specs/001-brand-system-and-design-foundation/contracts/observability-adapter.ts`.
 *
 * Feature code depends on the `ObservabilityAdapter` interface — never on a
 * specific implementation. The selector chooses between the no-op adapter
 * (default) and the Application Insights adapter based on the presence of
 * `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` at module load.
 *
 * PII hygiene (FR-041) is structurally enforced: every sanctioned event
 * shape below has a fixed, audit-ready attribute set with no free-form
 * string record. Trying to attach unsanctioned fields fails type-checking.
 */

import type { TraceContext } from "@/lib/http/trace-context";

// -----------------------------------------------------------------------------
// Sanctioned event shapes
// -----------------------------------------------------------------------------

export interface ErrorEventAttributes {
  readonly message: string;
  readonly category:
    | "render"
    | "unhandled-promise"
    | "route-load"
    | "data-fetch"
    | "observability-self";
  readonly componentStack?: string;
  readonly route?: string;
}

export type WebVitalName = "LCP" | "INP" | "CLS" | "TTFB" | "FCP";

export interface WebVitalEventAttributes {
  readonly name: WebVitalName;
  readonly value: number;
  readonly id: string;
  readonly navigationType:
    | "navigate"
    | "reload"
    | "back-forward"
    | "back-forward-cache"
    | "prerender"
    | "restore";
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
  | { kind: "error"; trace: TraceContext; attributes: ErrorEventAttributes }
  | { kind: "web-vital"; trace: TraceContext; attributes: WebVitalEventAttributes }
  | { kind: "route-change"; trace: TraceContext; attributes: RouteChangeEventAttributes }
  | { kind: "custom"; trace: TraceContext; attributes: CustomEventAttributes };

export type AdapterName = "noop" | "application-insights";

// -----------------------------------------------------------------------------
// Adapter interface
// -----------------------------------------------------------------------------

export interface ObservabilityAdapter {
  readonly name: AdapterName;
  readonly isActive: boolean;
  init(): Promise<void>;
  capture(event: ObservabilityEvent): void;
  flush(): Promise<void>;
}

// -----------------------------------------------------------------------------
// Selector
// -----------------------------------------------------------------------------

const APP_INSIGHTS_ENV_KEY = "NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING";

function readEnv(name: string): string | undefined {
  if (typeof process !== "undefined" && process.env) {
    return process.env[name];
  }
  return undefined;
}

let activeAdapter: ObservabilityAdapter | null = null;
let adapterFactories: {
  createNoopAdapter: () => ObservabilityAdapter;
  createAppInsightsAdapter: (connectionString: string) => ObservabilityAdapter;
} | null = null;

/**
 * Registers the concrete adapter factories. Called once at module load by
 * `./register-adapters.ts` so the selector stays free of import-time
 * dependencies on either adapter (the AI adapter dynamic-imports the SDK
 * inside its own `init()`).
 */
export function registerAdapters(factories: {
  createNoopAdapter: () => ObservabilityAdapter;
  createAppInsightsAdapter: (connectionString: string) => ObservabilityAdapter;
}): void {
  adapterFactories = factories;
}

/**
 * Returns the active adapter. Selection happens once per browser session.
 * The no-op adapter is the default; the Application Insights adapter is
 * activated when `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` is set. The
 * AI SDK itself is dynamic-imported inside the AI adapter's `init()`, so
 * the no-op path never pays the JS cost (FR-035e / FR-040).
 */
export function getAdapter(): ObservabilityAdapter {
  if (activeAdapter) return activeAdapter;
  if (!adapterFactories) {
    throw new Error(
      "Observability adapters not registered. Import `@/lib/observability/register-adapters` from the app entry.",
    );
  }
  const connectionString = readEnv(APP_INSIGHTS_ENV_KEY);
  if (connectionString && connectionString.trim().length > 0) {
    activeAdapter = adapterFactories.createAppInsightsAdapter(connectionString);
    return activeAdapter;
  }
  activeAdapter = adapterFactories.createNoopAdapter();
  return activeAdapter;
}

/**
 * Test-only — resets the cached adapter so a different env-var configuration
 * can be selected in a unit test. NOT for production use.
 */
export function __resetAdapterForTests(): void {
  activeAdapter = null;
}
