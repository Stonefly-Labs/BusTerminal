/**
 * Spec 006 / T087 + Spec 009 / T078. Detail page (Client Component — token
 * acquisition happens through MSAL on the client, and the App Router RSC
 * fetch path doesn't have access to the user's bearer).
 *
 * Dispatches on the id format:
 *   - `pe_*` → published-entity detail (Spec 009) — fetches `getEntityDetail`
 *     and renders the discovery-info + Azure-metadata panels alongside the
 *     registry-curated metadata card.
 *   - Anything else → Spec 006 registry detail shell (Namespace, App, etc.).
 */

"use client";

import { useQuery } from "@tanstack/react-query";
import { useParams } from "next/navigation";

import { EntityAzureMetadata } from "@/components/discovery/entity-azure-metadata";
import { EntityDiscoveryInfo } from "@/components/discovery/entity-discovery-info";
import { RegistryAuditPanel } from "@/components/registry/registry-audit-panel";
import { RegistryDetailShell } from "@/components/registry/registry-detail-shell";
import { RegistryEmptyState } from "@/components/registry/registry-empty-state";
import { RegistryRelationshipsPanel } from "@/components/registry/registry-relationships-panel";
import { RegistryUnauthorizedState } from "@/components/registry/registry-unauthorized-state";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useAcquireToken } from "@/hooks/use-acquire-token";
import { getEntityDetail, DiscoveryApiError } from "@/lib/discovery/api";
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

  const isPublishedEntity = id.startsWith("pe_");

  return isPublishedEntity ? (
    <PublishedEntityDetail id={id} />
  ) : (
    <RegistryEntityDetail id={id} />
  );
}

function PublishedEntityDetail({ id }: { id: string }) {
  const getToken = useAcquireToken();
  const detailQuery = useQuery({
    queryKey: ["discovery", "entity", id] as const,
    queryFn: async () => {
      const token = await getToken();
      return getEntityDetail(id, token ? { accessToken: token } : {});
    },
    enabled: !!id,
  });

  if (detailQuery.isPending) {
    return (
      <p data-testid="entity-detail-loading" className="text-sm text-foreground-muted">
        Loading…
      </p>
    );
  }

  if (detailQuery.error instanceof DiscoveryApiError && detailQuery.error.status === 401) {
    return <RegistryUnauthorizedState />;
  }

  if (detailQuery.error instanceof DiscoveryApiError && detailQuery.error.status === 404) {
    return (
      <RegistryEmptyState
        variant="no-data"
        title="Entity not found"
        description={`No published entity with id ${id}.`}
      />
    );
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
        description={`No published entity with id ${id}.`}
      />
    );
  }

  const { entity } = detailQuery.data;
  return (
    <div data-testid="published-entity-detail" className="flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-foreground-default">{entity.name}</h1>
        <p className="text-sm text-foreground-muted">
          {entity.entityType} · {entity.environment} · namespace {entity.namespaceId}
        </p>
      </header>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <EntityDiscoveryInfo entity={entity} />
        <RegistryMetadataCard entity={entity} />
      </div>

      <EntityAzureMetadata entity={entity} />
    </div>
  );
}

function RegistryMetadataCard({
  entity,
}: {
  entity: {
    readonly description?: string | null | undefined;
    readonly businessPurpose?: string | null | undefined;
    readonly tags?: readonly string[] | undefined;
    readonly operationalNotes?: string | null | undefined;
  };
}) {
  return (
    <Card data-testid="entity-registry-metadata">
      <CardHeader>
        <CardTitle>Registry metadata</CardTitle>
        <p className="text-xs text-foreground-muted">
          Curated by service owners and namespace administrators.
        </p>
      </CardHeader>
      <CardContent className="flex flex-col gap-3 text-sm">
        <Field label="Description" value={entity.description} />
        <Field label="Business purpose" value={entity.businessPurpose} />
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-foreground-subtle">Tags</p>
          {entity.tags && entity.tags.length > 0 ? (
            <ul className="mt-1 flex flex-wrap gap-1" data-testid="entity-tags">
              {entity.tags.map((t) => (
                <li
                  key={t}
                  className="rounded-full border border-border-default px-2 py-0.5 text-xs"
                >
                  {t}
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-foreground-muted">—</p>
          )}
        </div>
        <Field label="Operational notes" value={entity.operationalNotes} />
      </CardContent>
    </Card>
  );
}

function Field({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <div className="flex flex-col gap-0.5">
      <p className="text-xs font-medium uppercase tracking-wide text-foreground-subtle">{label}</p>
      <p className="text-foreground-default">{value ?? "—"}</p>
    </div>
  );
}

function RegistryEntityDetail({ id }: { id: string }) {
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

  const entity = detailQuery.data.entity;
  return (
    <RegistryDetailShell
      entity={entity}
      relationshipsSlot={<RegistryRelationshipsPanel entity={entity} />}
      auditSlot={<RegistryAuditPanel entityId={entity.id} />}
    />
  );
}
