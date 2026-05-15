/**
 * Registers the concrete observability adapter factories with the selector
 * (`adapter.ts`). Import this once from the app entry (`app/providers.tsx`)
 * before any call to `getAdapter()`.
 */

import { registerAdapters } from "./adapter";
import { createNoopAdapter } from "./noop-adapter";
import { createAppInsightsAdapter } from "./app-insights-adapter";

registerAdapters({
  createNoopAdapter,
  createAppInsightsAdapter,
});
