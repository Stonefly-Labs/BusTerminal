"use client";

/**
 * Root client-side providers (T030).
 *
 * Composes:
 *   - `next-themes` ThemeProvider with the class strategy + `bt:theme` key
 *     per `contracts/theme-provider.ts`.
 *   - Observability adapter registration + initialization (FR-040 / SC-013).
 *   - Web Vitals beacon (FR-037 / SC-016).
 *   - App Router route-change span emitter (FR-038).
 *   - Top-level error boundary (FR-036 / SC-015).
 *
 * Registration is performed once on first client render so the no-op path
 * costs nothing and the AI adapter dynamic-imports its SDK only when
 * `NEXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING` is set.
 */

import { Suspense, useEffect, type ReactNode } from "react";
import { ThemeProvider } from "next-themes";

import "@/lib/observability/register-adapters";
import { ErrorBoundary } from "@/lib/observability/error-boundary";
import { RouteChangeReporter } from "@/lib/observability/route-change";
import { startWebVitalsCapture } from "@/lib/observability/web-vitals";
import { getAdapter } from "@/lib/observability/adapter";
import { THEME_STORAGE_KEY } from "@/lib/theme-provider-constants";

function ObservabilityBootstrap(): null {
  useEffect(() => {
    const adapter = getAdapter();
    void adapter.init();
    startWebVitalsCapture();
    const handlePageHide = (): void => {
      void adapter.flush();
    };
    window.addEventListener("pagehide", handlePageHide);
    return () => window.removeEventListener("pagehide", handlePageHide);
  }, []);
  return null;
}

interface ProvidersProps {
  readonly children: ReactNode;
}

export function Providers({ children }: ProvidersProps) {
  return (
    <ThemeProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      storageKey={THEME_STORAGE_KEY}
      disableTransitionOnChange
    >
      <ErrorBoundary>
        <ObservabilityBootstrap />
        <Suspense fallback={null}>
          <RouteChangeReporter />
        </Suspense>
        {children}
      </ErrorBoundary>
    </ThemeProvider>
  );
}
