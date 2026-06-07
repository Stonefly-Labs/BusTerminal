"use client";

/**
 * Spec 006 / T057. TanStack Query provider for the registry slice.
 *
 * Mounted from `web/app/providers.tsx` so every Client Component below the
 * authenticated layout participates in the same QueryClient instance.
 * Configuration choices follow research §6:
 *
 *   - Entity queries: 60s staleTime — registry data doesn't change quickly
 *     under normal operation, and the audit panel + form mutations
 *     invalidate explicitly when the operator's own actions update state.
 *   - Audit queries: 10s staleTime — the audit panel refreshes more
 *     aggressively so the operator sees their own event immediately after
 *     a save (quickstart §7).
 *   - Suspense disabled by default (Next.js 16 RSC handles initial loads;
 *     hooks fall back to standard isPending state).
 *   - Query-error events are forwarded to the existing observability adapter
 *     so error rates roll up alongside the rest of the frontend signals
 *     (FR-040 / SC-013).
 */

import {
  QueryCache,
  QueryClient,
  QueryClientProvider,
} from "@tanstack/react-query";
import { useState, type ReactNode } from "react";

import { getAdapter } from "@/lib/observability/adapter";
import { newTraceContext } from "@/lib/http/trace-context";

const SIXTY_SECONDS = 60_000;
const TEN_SECONDS = 10_000;

function makeQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: SIXTY_SECONDS,
        refetchOnWindowFocus: false,
        retry: (failureCount, error) => {
          const status = (error as { status?: number } | null)?.status;
          if (status && status >= 400 && status < 500 && status !== 408) return false;
          return failureCount < 2;
        },
      },
      mutations: {
        retry: false,
      },
    },
    queryCache: new QueryCache({
      onError: (_error, query) => {
        const adapter = getAdapter();
        adapter.capture({
          kind: "custom",
          trace: newTraceContext(),
          attributes: {
            name: "registry.query.error",
            correlationIds: query.queryKey.map(String),
          },
        });
      },
    }),
  });
}

// Audit panel queries should override the default 60s staleTime so the
// operator sees their own audit event after a save (quickstart §7). Callers
// of useQuery on audit keys pass `staleTime: AUDIT_STALE_TIME_MS`.
export const REGISTRY_AUDIT_STALE_TIME_MS = TEN_SECONDS;

interface RegistryQueryProviderProps {
  readonly children: ReactNode;
}

export function RegistryQueryProvider({ children }: RegistryQueryProviderProps) {
  // useState guarantees one QueryClient per browser tab — important for
  // Next.js App Router where the providers component remounts on theme/locale
  // changes but the cache should persist.
  const [client] = useState(() => makeQueryClient());
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
