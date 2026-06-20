"use client";

/**
 * Spec 006 / T111 + Spec 009 / T077. /registry/search route. Reads search
 * state from the URL (`?q=…&entityType=…&environment=…&status=…&page=…
 * &pageSize=…&lifecycleStatus=…&associatedServiceId=…&associationRole=…`)
 * so the surface is shareable and back/forward-friendly.
 *
 * Spec 009 adds three new filter components: <LifecycleFilter>,
 * <ServiceAssociationFilter>, plus passing the new filter state into the
 * existing search query so namespace administrators and operators can
 * narrow to discovered-entity subsets without leaving the page.
 */

import { useRouter, useSearchParams, usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";

import { LifecycleFilter } from "@/components/registry/filters/lifecycle-filter";
import { ServiceAssociationFilter } from "@/components/registry/filters/service-association-filter";
import { RegistrySearchFilters } from "@/components/registry/registry-search-filters";
import { RegistrySearchInput } from "@/components/registry/registry-search-input";
import {
  RegistrySearchResultsTable,
  type RegistrySearchResultRow,
} from "@/components/registry/registry-search-results-table";
import {
  listEnvironments,
  resolveApiOptions,
  searchEntities,
  RegistryApiError,
} from "@/lib/registry/api";
import { registryQueryKeys } from "@/lib/registry/query-keys";
import type { RegistryEntityType } from "@/lib/registry/types";
import { useAcquireToken } from "@/hooks/use-acquire-token";

const DEFAULT_PAGE_SIZE = 25;

export default function RegistrySearchPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const q = searchParams.get("q") ?? "";
  const entityType = (searchParams.get("entityType") ?? undefined) as RegistryEntityType | undefined;
  const environment = searchParams.get("environment") ?? undefined;
  const status = searchParams.get("status") ?? undefined;
  const page = Math.max(1, Number(searchParams.get("page") ?? "1"));
  const pageSize = Math.max(1, Number(searchParams.get("pageSize") ?? DEFAULT_PAGE_SIZE));

  const getToken = useAcquireToken();

  const environmentsQuery = useQuery({
    queryKey: registryQueryKeys.environments.list(),
    // The registry API client never acquires its own token — resolve one here
    // so the call is authenticated under real Entra auth.
    queryFn: async () => listEnvironments(await resolveApiOptions(undefined, getToken)),
    staleTime: 60_000,
  });

  const searchQuery = useQuery({
    queryKey: registryQueryKeys.search.query(q, { entityType, environment, status, page, pageSize }),
    queryFn: async () =>
      searchEntities(
        {
          query: q,
          ...(entityType ? { entityType } : {}),
          ...(environment ? { environment } : {}),
          ...(status ? { status } : {}),
          top: pageSize,
          skip: (page - 1) * pageSize,
        },
        await resolveApiOptions(undefined, getToken),
      ),
    enabled: q.length > 0,
  });

  const setUrl = (patch: Record<string, string | undefined>) => {
    const next = new URLSearchParams(searchParams.toString());
    for (const [key, value] of Object.entries(patch)) {
      if (value === undefined || value === "") next.delete(key);
      else next.set(key, value);
    }
    router.replace(`${pathname}?${next.toString()}` as never);
  };

  const state: Parameters<typeof RegistrySearchResultsTable>[0]["state"] =
    q.length === 0
      ? "idle"
      : searchQuery.isPending
        ? "loading"
        : searchQuery.error instanceof RegistryApiError && searchQuery.error.status === 503
          ? "unavailable"
          : searchQuery.isError
            ? "error"
            : "loaded";

  const rows: RegistrySearchResultRow[] = (searchQuery.data?.hits ?? []).map((hit) => ({
    id: hit.id,
    entityType: hit.entityType,
    name: hit.name,
    fullyQualifiedName: hit.fullyQualifiedName ?? null,
    environment: hit.environment ?? null,
    status: hit.status ?? null,
    owner: hit.owner ?? null,
    namespaceName: hit.namespaceName ?? null,
    score: hit.score ?? null,
  }));

  return (
    <div data-testid="registry-search-page" className="flex flex-col gap-4">
      <div>
        <h1 className="text-2xl font-semibold text-foreground-default">Search the registry</h1>
        <p className="mt-1 text-sm text-foreground-muted">
          Full-text search across names, descriptions, owners, tags and metadata.
        </p>
      </div>

      <RegistrySearchInput
        value={q}
        onChange={(next) => setUrl({ q: next || undefined, page: undefined })}
        autoFocus
      />

      <RegistrySearchFilters environments={environmentsQuery.data ?? []} />

      <div className="flex flex-col gap-2 border-t border-border-default pt-3">
        <LifecycleFilter />
        <ServiceAssociationFilter />
      </div>

      <RegistrySearchResultsTable
        results={rows}
        state={state}
        errorMessage={searchQuery.error instanceof Error ? searchQuery.error.message : undefined}
        totalCount={searchQuery.data?.totalCount ?? null}
        page={page}
        pageSize={pageSize}
        onPageChange={(next) => setUrl({ page: String(next) })}
      />
    </div>
  );
}
