/**
 * Spec 006 / T087. Detail page (Client Component for now — token acquisition
 * happens through MSAL on the client, and the App Router RSC fetch path
 * doesn't have access to the user's bearer). Renders the detail shell or
 * a not-found / unauthorized state.
 */

"use client";

import { useQuery } from "@tanstack/react-query";
import { useParams } from "next/navigation";

import { RegistryDetailShell } from "@/components/registry/registry-detail-shell";
import { RegistryEmptyState } from "@/components/registry/registry-empty-state";
import { RegistryUnauthorizedState } from "@/components/registry/registry-unauthorized-state";
import { getEntity, RegistryApiError } from "@/lib/registry/api";
import { registryQueryKeys } from "@/lib/registry/query-keys";

export default function RegistryDetailPage() {
  const params = useParams();
  const rawId = params.id;
  const id =
    typeof rawId === "string"
      ? rawId
      : Array.isArray(rawId) && rawId.length > 0
        ? (rawId[0] ?? "")
        : "";

  const detailQuery = useQuery({
    queryKey: registryQueryKeys.entities.detail(id),
    queryFn: () => getEntity(id),
    enabled: !!id,
  });

  if (detailQuery.isPending) {
    return (
      <p data-testid="registry-detail-loading" className="text-sm text-foreground-muted">
        Loading…
      </p>
    );
  }

  if (detailQuery.error instanceof RegistryApiError && detailQuery.error.status === 401) {
    return <RegistryUnauthorizedState />;
  }

  if (detailQuery.isError) {
    return (
      <RegistryEmptyState
        variant="unavailable"
        title="Could not load entity"
        description={
          detailQuery.error instanceof Error ? detailQuery.error.message : "Unknown error"
        }
      />
    );
  }

  if (!detailQuery.data) {
    return (
      <RegistryEmptyState
        variant="no-data"
        title="Entity not found"
        description={`No registry entity with id ${id}. It may have been deleted.`}
      />
    );
  }

  return <RegistryDetailShell entity={detailQuery.data.entity} />;
}
