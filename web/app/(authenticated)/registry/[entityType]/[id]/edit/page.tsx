"use client";

/**
 * Spec 006 / T100 + Spec 009 / T112. Edit-form route.
 *
 * Dispatches on the id format:
 *   - `pe_*` → published-entity curated-metadata editor (Spec 009).
 *   - Anything else → Spec 006 typed forms (Namespace, Queue, ...).
 */

import { useQuery } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import type { Route } from "next";

import { NamespaceForm } from "@/components/registry/forms/namespace-form";
import { PublishedEntityEditForm } from "@/components/registry/forms/published-entity-edit-form";
import { QueueForm } from "@/components/registry/forms/queue-form";
import { RuleForm } from "@/components/registry/forms/rule-form";
import { SubscriptionForm } from "@/components/registry/forms/subscription-form";
import { TopicForm } from "@/components/registry/forms/topic-form";
import { RegistryEmptyState } from "@/components/registry/registry-empty-state";
import { useAcquireToken } from "@/hooks/use-acquire-token";
import { DiscoveryApiError, getEntityDetail } from "@/lib/discovery/api";
import { getEntity } from "@/lib/registry/api";
import { registryQueryKeys } from "@/lib/registry/query-keys";

export default function RegistryEditEntityPage() {
  const params = useParams();
  const rawId = params.id;
  const id =
    typeof rawId === "string"
      ? rawId
      : Array.isArray(rawId) && rawId.length > 0
        ? (rawId[0] ?? "")
        : "";

  if (id.startsWith("pe_")) {
    return <PublishedEntityEdit id={id} />;
  }
  return <RegistryEntityEdit id={id} />;
}

function PublishedEntityEdit({ id }: { id: string }) {
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
    return <p className="text-sm text-foreground-muted">Loading…</p>;
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
  if (detailQuery.isError || !detailQuery.data) {
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

  return <PublishedEntityEditForm entity={detailQuery.data.entity} etag={detailQuery.data.etag} />;
}

function RegistryEntityEdit({ id }: { id: string }) {
  const router = useRouter();
  const params = useParams();
  const rawEntityType = params.entityType;
  const entityTypeParam =
    typeof rawEntityType === "string"
      ? rawEntityType
      : Array.isArray(rawEntityType) && rawEntityType.length > 0
        ? (rawEntityType[0] ?? "")
        : "";

  const detailQuery = useQuery({
    queryKey: registryQueryKeys.entities.detail(id),
    queryFn: () => getEntity(id),
    enabled: !!id,
  });

  if (detailQuery.isPending) {
    return <p className="text-sm text-foreground-muted">Loading…</p>;
  }
  if (detailQuery.isError || !detailQuery.data) {
    return (
      <RegistryEmptyState
        variant="no-data"
        title="Entity not found"
        description={`No registry entity with id ${id}.`}
      />
    );
  }

  const { entity, etag } = detailQuery.data;
  const onSaved = () => {
    router.push(`/registry/${entity.entityType}/${entity.id}` as Route);
  };
  const onCancel = () => router.back();

  const commonProps = {
    mode: "edit" as const,
    id: entity.id,
    persistedEntity: entity,
    persistedEtag: etag,
    onSaved,
    onCancel,
  };

  if (entityTypeParam.toLowerCase() !== entity.entityType.toLowerCase()) {
    return (
      <RegistryEmptyState
        variant="no-data"
        title="Entity type mismatch"
        description={`URL type '${entityTypeParam}' doesn't match stored type '${entity.entityType}'.`}
      />
    );
  }

  switch (entity.entityType) {
    case "Namespace":
      return <NamespaceForm {...commonProps} />;
    case "Queue":
      return <QueueForm {...commonProps} />;
    case "Topic":
      return <TopicForm {...commonProps} />;
    case "Subscription":
      return <SubscriptionForm {...commonProps} />;
    case "Rule":
      return <RuleForm {...commonProps} />;
  }
}
