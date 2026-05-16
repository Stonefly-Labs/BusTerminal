/**
 * No-op observability adapter (FR-040 default).
 *
 * Default adapter when `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` is absent.
 * It records every captured event to an in-memory ring buffer keyed by event
 * kind so SC-013 (frontend telemetry observable locally without a backend)
 * and SC-016 (Web Vitals captured on every load) can be verified in dev.
 *
 * In dev, the debug pipeline is reachable through `window.__bt_obs_debug`.
 */

import type {
  ObservabilityAdapter,
  ObservabilityEvent,
} from "./adapter";

const RING_BUFFER_LIMIT = 50;

export interface NoopDebugPipeline {
  byKind(kind: ObservabilityEvent["kind"]): readonly ObservabilityEvent[];
  all(): readonly ObservabilityEvent[];
  reset(): void;
}

declare global {
  // Augments `globalThis.__bt_obs_debug` for dev-time inspection (SC-013).
  // prettier-ignore
  var __bt_obs_debug: NoopDebugPipeline | undefined;
}

function createPipeline(): NoopDebugPipeline & { record(event: ObservabilityEvent): void } {
  const buffers: Record<ObservabilityEvent["kind"], ObservabilityEvent[]> = {
    error: [],
    "web-vital": [],
    "route-change": [],
    custom: [],
  };
  return {
    record(event) {
      const bucket = buffers[event.kind];
      bucket.push(event);
      if (bucket.length > RING_BUFFER_LIMIT) bucket.shift();
    },
    byKind(kind) {
      return buffers[kind].slice();
    },
    all() {
      return Object.values(buffers).flat();
    },
    reset() {
      buffers.error.length = 0;
      buffers["web-vital"].length = 0;
      buffers["route-change"].length = 0;
      buffers.custom.length = 0;
    },
  };
}

export function createNoopAdapter(): ObservabilityAdapter {
  const pipeline = createPipeline();
  if (typeof globalThis !== "undefined") {
    globalThis.__bt_obs_debug = pipeline;
  }
  return {
    name: "noop",
    isActive: false,
    async init() {
      // Nothing to initialize.
    },
    capture(event) {
      pipeline.record(event);
    },
    async flush() {
      // Nothing to flush.
    },
  };
}
