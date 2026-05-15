/**
 * Application Insights observability adapter (FR-040 / SC-013 – SC-016).
 *
 * Activated only when `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` is set
 * at module load. The `@microsoft/applicationinsights-web` SDK is
 * dynamic-imported inside `init()` so the no-op path doesn't pay the JS
 * cost (Research R4 / FR-035e).
 *
 * Errors inside the adapter itself MUST NOT throw — they are buffered and
 * surfaced through the no-op debug pipeline (and, if init succeeded, the
 * AI client) as `category: 'observability-self'` error events.
 */

import type { ObservabilityAdapter, ObservabilityEvent } from "./adapter";

interface ApplicationInsightsLike {
  loadAppInsights(): void;
  trackException(payload: {
    exception: Error;
    properties?: Record<string, string | number | boolean | undefined>;
  }): void;
  trackMetric(payload: {
    name: string;
    average: number;
    properties?: Record<string, string | number | boolean | undefined>;
  }): void;
  trackEvent(payload: {
    name: string;
    properties?: Record<string, string | number | boolean | undefined>;
  }): void;
  flush(): void;
}

export function createAppInsightsAdapter(connectionString: string): ObservabilityAdapter {
  let client: ApplicationInsightsLike | null = null;
  let initPromise: Promise<void> | null = null;
  const pending: ObservabilityEvent[] = [];

  function selfError(message: string): void {
    console.warn(`[observability] ${message}`);
  }

  function flushPending(): void {
    if (!client) return;
    while (pending.length > 0) {
      const event = pending.shift();
      if (event) deliver(event);
    }
  }

  function deliver(event: ObservabilityEvent): void {
    if (!client) {
      pending.push(event);
      return;
    }
    const baseProperties = {
      "trace.id": event.trace.traceId,
      "trace.span": event.trace.spanId,
      "trace.flags": event.trace.traceFlags,
    } as const;
    try {
      switch (event.kind) {
        case "error":
          client.trackException({
            exception: new Error(event.attributes.message),
            properties: {
              ...baseProperties,
              category: event.attributes.category,
              componentStack: event.attributes.componentStack,
              route: event.attributes.route,
            },
          });
          return;
        case "web-vital":
          client.trackMetric({
            name: `webvital.${event.attributes.name.toLowerCase()}`,
            average: event.attributes.value,
            properties: {
              ...baseProperties,
              "webvital.id": event.attributes.id,
              "webvital.navigationType": event.attributes.navigationType,
            },
          });
          return;
        case "route-change":
          client.trackEvent({
            name: "route-change",
            properties: {
              ...baseProperties,
              "route.from": event.attributes.fromRoute,
              "route.to": event.attributes.toRoute,
              "route.durationMs": event.attributes.durationMs,
            },
          });
          return;
        case "custom":
          client.trackEvent({
            name: event.attributes.name,
            properties: {
              ...baseProperties,
              "correlation.ids": event.attributes.correlationIds?.join(","),
            },
          });
          return;
      }
    } catch (cause) {
      selfError(`failed to forward ${event.kind} event: ${(cause as Error).message}`);
    }
  }

  return {
    name: "application-insights",
    isActive: true,
    async init() {
      if (initPromise) return initPromise;
      initPromise = (async () => {
        try {
          const mod = (await import(
            /* webpackChunkName: "app-insights-sdk" */
            "@microsoft/applicationinsights-web"
          )) as unknown as {
            ApplicationInsights: new (config: { config: { connectionString: string } }) => ApplicationInsightsLike;
          };
          const instance = new mod.ApplicationInsights({
            config: { connectionString },
          });
          instance.loadAppInsights();
          client = instance;
          flushPending();
        } catch (cause) {
          selfError(`failed to load applicationinsights-web: ${(cause as Error).message}`);
        }
      })();
      return initPromise;
    },
    capture(event) {
      deliver(event);
    },
    async flush() {
      if (!client) return;
      try {
        client.flush();
      } catch (cause) {
        selfError(`flush failed: ${(cause as Error).message}`);
      }
    },
  };
}
