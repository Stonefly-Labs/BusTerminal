"use client";

/**
 * Spec 006 / T100. Edit-form route. Pre-fetches the entity, then renders the
 * matching form with `persistedEtag` hidden field. Mutations route through
 * the form's conflict-modal hookup on 409.
 */

import { useQuery } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import type { Route } from "next";

import { NamespaceForm } from "@/components/registry/forms/namespace-form";
import { QueueForm } from "@/components/registry/forms/queue-form";
import { TopicForm } from "@/components/registry/forms/topic-form";
import { SubscriptionForm } from "@/components/registry/forms/subscription-form";
import { RuleForm } from "@/components/registry/forms/rule-form";
import { RegistryEmptyState } from "@/components/registry/registry-empty-state";
import { getEntity } from "@/lib/registry/api";
import { registryQueryKeys } from "@/lib/registry/query-keys";

export default function RegistryEditEntityPage() {
  const params = useParams();
  const router = useRouter();

  const rawId = params.id;
  const id =
    typeof rawId === "string"
      ? rawId
      : Array.isArray(rawId) && rawId.length > 0
        ? (rawId[0] ?? "")
        : "";
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
