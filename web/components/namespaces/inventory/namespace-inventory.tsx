"use client";

/**
 * Spec 008 / T109 / US2. Client Component that drives the inventory page —
 * reads URL params, hits the typed namespace API via TanStack Query, and
 * composes `<NamespaceInventoryFilters>` + `<NamespaceInventoryTable>`.
 *
 * Page split:
 *   - `web/app/(authenticated)/namespaces/page.tsx` is a thin RSC shell that
 *     mounts this component (so SSR + auth gates apply).
 *   - This component owns data fetching, URL state, error + loading states.
 */

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import { useHasRole } from "@/hooks/use-has-role";
import * as NamespacesApi from "@/lib/namespaces/api";
import { namespaceKeys, type NamespaceInventoryFilter } from "@/lib/namespaces/query-keys";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

import { NamespaceInventoryFilters } from "./namespace-inventory-filters";
import { NamespaceInventoryTable } from "./namespace-inventory-table";

export function NamespaceInventory() {
  const searchParams = useSearchParams();
  const isAdmin = useHasRole("BusTerminal.NamespaceAdministrator");
  const getToken = useAcquireToken();

  const filter: NamespaceInventoryFilter & {
    pageSize?: number;
    continuationToken?: string;
    sort?: string;
  } = {};
  const set = (key: keyof typeof filter, value: string | boolean | number | undefined) => {
    if (value === undefined || value === "") return;
    (filter as Record<string, unknown>)[key] = value;
  };
  set("environment", searchParams.get("environment") ?? undefined);
  set("lifecycleStatus", searchParams.get("lifecycleStatus") ?? undefined);
  set("validationStatus", searchParams.get("validationStatus") ?? undefined);
  set("tagKey", searchParams.get("tagKey") ?? undefined);
  set("tagValue", searchParams.get("tagValue") ?? undefined);
  set("q", searchParams.get("q") ?? undefined);
  const includeArchived = searchParams.get("includeArchived");
  if (includeArchived === "true") set("includeArchived", true);
  const continuationToken = searchParams.get("continuationToken");
  if (continuationToken) set("continuationToken", continuationToken);
  const sort = searchParams.get("sort");
  // Sort is passed via URL straight to the server through a side-channel
  // (the api client doesn't strictly type it yet — added below).

  const query = useQuery({
    queryKey: namespaceKeys.inventory.list({ ...filter, sort } as never),
    queryFn: async () => {
      const token = await getToken();
      return NamespacesApi.listInventory(
        { ...filter, ...(sort ? { sort } : {}) } as never,
        token ? { accessToken: token } : {},
      );
    },
  });

  return (
    <div className="flex flex-col gap-4 p-6">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-foreground-default">Namespace inventory</h1>
          <p className="mt-1 text-sm text-foreground-muted">
            Onboarded Azure Service Bus namespaces with ownership, validation, and lifecycle context.
          </p>
        </div>
        {isAdmin ? (
          <Button asChild intent="primary">
            <Link href={"/namespaces/onboard" as never}>Onboard a namespace</Link>
          </Button>
        ) : null}
      </header>

      <NamespaceInventoryFilters />

      {query.isLoading ? (
        <Card>
          <CardContent className="p-6 text-sm text-foreground-muted">Loading inventory…</CardContent>
        </Card>
      ) : query.isError ? (
        <Card>
          <CardContent className="p-6">
            <p className="text-sm text-error-foreground">
              Failed to load the namespace inventory. Try again, and reach out to the platform team if the
              error persists.
            </p>
          </CardContent>
        </Card>
      ) : (
        <NamespaceInventoryTable
          items={query.data?.items ?? []}
          continuationToken={query.data?.continuationToken}
        />
      )}
    </div>
  );
}
