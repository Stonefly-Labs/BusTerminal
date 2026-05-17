/**
 * Slice 002 telemetry entry-point for the Application Insights adapter.
 *
 * The adapter itself lives in `web/lib/observability/app-insights-adapter.ts`
 * from slice 001. This module re-exports it and adds the route-change
 * tracking hook (`usePageView`) per task T026.
 */

"use client";

import { useEffect, useRef } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { getAdapter } from "@/lib/observability/adapter";
import { getOrCreateActiveTraceContext } from "@/lib/telemetry/trace-context";

export { createAppInsightsAdapter } from "@/lib/observability/app-insights-adapter";

export function usePageView(): void {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const previousRoute = useRef<string | null>(null);
  const lastChangeAt = useRef<number>(performance.now());

  useEffect(() => {
    const query = searchParams?.toString();
    const toRoute = query ? `${pathname}?${query}` : pathname ?? "";
    const fromRoute = previousRoute.current ?? toRoute;
    const now = performance.now();
    const durationMs = Math.max(0, Math.round(now - lastChangeAt.current));

    const adapter = getAdapter();
    adapter.capture({
      kind: "route-change",
      trace: getOrCreateActiveTraceContext(),
      attributes: {
        fromRoute,
        toRoute,
        durationMs,
      },
    });

    previousRoute.current = toRoute;
    lastChangeAt.current = now;
  }, [pathname, searchParams]);
}
