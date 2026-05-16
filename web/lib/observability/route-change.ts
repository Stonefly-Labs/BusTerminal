/**
 * App Router route-change span emitter (FR-038).
 *
 * Listens to `usePathname` / `useSearchParams` transitions and emits a
 * `route-change` event with `{ fromRoute, toRoute, durationMs }` through
 * the active observability adapter.
 *
 * Duration is the time between successive path/search updates — a useful
 * proxy for perceived navigation latency that is cheap to compute on the
 * client without instrumenting the router internals.
 */

"use client";

import { useEffect, useRef } from "react";
import { usePathname, useSearchParams } from "next/navigation";

import { getAdapter } from "./adapter";
import { newTraceContext } from "@/lib/http/trace-context";

function buildRoute(pathname: string, search: string): string {
  return search ? `${pathname}?${search}` : pathname;
}

/**
 * Mounted by the root providers. Renders nothing.
 */
export function RouteChangeReporter(): null {
  const pathname = usePathname();
  const search = useSearchParams();
  const previousRouteRef = useRef<string | null>(null);
  const startedAtRef = useRef<number>(0);

  useEffect(() => {
    const now = typeof performance !== "undefined" ? performance.now() : Date.now();
    const currentRoute = buildRoute(pathname ?? "/", search?.toString() ?? "");
    const previousRoute = previousRouteRef.current;
    if (previousRoute && previousRoute !== currentRoute) {
      const adapter = getAdapter();
      adapter.capture({
        kind: "route-change",
        trace: newTraceContext(),
        attributes: {
          fromRoute: previousRoute,
          toRoute: currentRoute,
          durationMs: Math.max(0, now - startedAtRef.current),
        },
      });
    }
    previousRouteRef.current = currentRoute;
    startedAtRef.current = now;
  }, [pathname, search]);

  return null;
}
