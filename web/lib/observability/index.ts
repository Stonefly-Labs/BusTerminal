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
} from "./adapter";
export { createNoopAdapter } from "./noop-adapter";
export { createAppInsightsAdapter } from "./app-insights-adapter";
export { startWebVitalsCapture } from "./web-vitals";
export { RouteChangeReporter } from "./route-change";
export { ErrorBoundary, type ErrorBoundaryState } from "./error-boundary";
