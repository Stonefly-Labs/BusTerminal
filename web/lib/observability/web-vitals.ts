/**
 * Web Vitals capture (FR-037 / SC-016).
 *
 * Registers the LCP / INP / CLS / TTFB / FCP collectors from the
 * `web-vitals` library and forwards every measurement through the active
 * observability adapter. Capture runs on every page load regardless of
 * adapter configuration — the no-op adapter records the measurements into
 * the in-memory debug pipeline so SC-016 is verifiable in dev.
 *
 * The W3C trace context attached to each event is freshly minted so each
 * Web Vital correlates to its own span — they are reported asynchronously
 * relative to user actions, so reusing a request-scoped trace would be
 * misleading.
 */

import { onCLS, onFCP, onINP, onLCP, onTTFB, type Metric } from "web-vitals";

import { getAdapter, type WebVitalEventAttributes } from "./adapter";
import { newTraceContext } from "@/lib/http/trace-context";

function mapNavigationType(value: Metric["navigationType"]): WebVitalEventAttributes["navigationType"] {
  switch (value) {
    case "back-forward":
    case "back-forward-cache":
    case "navigate":
    case "prerender":
    case "reload":
    case "restore":
      return value;
    default:
      return "navigate";
  }
}

function handle(metric: Metric, name: WebVitalEventAttributes["name"]): void {
  const adapter = getAdapter();
  adapter.capture({
    kind: "web-vital",
    trace: newTraceContext(),
    attributes: {
      name,
      value: metric.value,
      id: metric.id,
      navigationType: mapNavigationType(metric.navigationType),
    },
  });
}

let registered = false;

/**
 * Idempotent. Safe to call from layout-level client components on every
 * render — only the first invocation registers the listeners.
 */
export function startWebVitalsCapture(): void {
  if (registered) return;
  registered = true;
  onLCP((metric) => handle(metric, "LCP"));
  onINP((metric) => handle(metric, "INP"));
  onCLS((metric) => handle(metric, "CLS"));
  onTTFB((metric) => handle(metric, "TTFB"));
  onFCP((metric) => handle(metric, "FCP"));
}
