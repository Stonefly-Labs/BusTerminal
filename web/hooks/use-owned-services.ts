/**
 * Spec 009 / T028a. Returns the set of service IDs the current user holds
 * the Service Owner role for. Used by `canEditEntityMetadata` (see
 * `web/lib/discovery/permissions.ts`) to gate edit affordances without an
 * extra round-trip per render.
 *
 * Phase 2 (foundational): the backend `/api/me/owned-services` surface is
 * scheduled for a later phase. Until then this hook returns an empty set
 * — the policy still permits Platform/Namespace Admins, who reach the edit
 * affordances via the role-only branches of R-15.
 */

"use client";

import { useQuery, type UseQueryResult } from "@tanstack/react-query";

import { httpFetch } from "@/lib/http/client";

import { useAcquireToken } from "./use-acquire-token";
import { useCurrentUser } from "./use-current-user";

const STALE_TIME_MS = 5 * 60 * 1000;

export const ownedServicesQueryKey = ["identity", "owned-services"] as const;

export interface UseOwnedServicesResult {
  readonly data: ReadonlySet<string>;
  readonly isLoading: boolean;
  readonly error: unknown;
  readonly query: UseQueryResult<ReadonlySet<string>, unknown>;
}

export function useOwnedServices(): UseOwnedServicesResult {
  const account = useCurrentUser();
  const acquireToken = useAcquireToken();

  const query = useQuery<ReadonlySet<string>>({
    queryKey: [...ownedServicesQueryKey, account?.homeAccountId ?? null] as const,
    queryFn: async () => fetchOwnedServices(await acquireToken()),
    enabled: account !== null,
    staleTime: STALE_TIME_MS,
  });

  return {
    data: query.data ?? EMPTY_SET,
    isLoading: query.isLoading,
    error: query.error,
    query,
  };
}

const EMPTY_SET: ReadonlySet<string> = new Set<string>();

async function fetchOwnedServices(token: string | null): Promise<ReadonlySet<string>> {
  const headers = new Headers({ accept: "application/json" });
  if (token) headers.set("authorization", `Bearer ${token}`);

  const response = await httpFetch("/api/me/owned-services", {
    headers,
    operation: "identity.owned-services",
  });

  // Endpoint is scheduled for a later phase — degrade to empty so the hook
  // stays usable now without breaking pages that compose it.
  if (response.status === 404) return EMPTY_SET;
  if (!response.ok) return EMPTY_SET;

  try {
    const json: unknown = await response.json();
    if (Array.isArray(json)) {
      return new Set<string>(json as readonly string[]);
    }
    if (json !== null && typeof json === "object" && "items" in json) {
      const items = (json as { items?: readonly string[] }).items ?? [];
      return new Set<string>(items);
    }
    return EMPTY_SET;
  } catch {
    return EMPTY_SET;
  }
}
