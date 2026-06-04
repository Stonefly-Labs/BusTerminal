"use client";

/**
 * Spec 006 / T099. Create-form route. Dispatches on `entityType` to the
 * matching form component.
 */

import { useMemo } from "react";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import type { Route } from "next";

import { NamespaceForm } from "@/components/registry/forms/namespace-form";
import { QueueForm } from "@/components/registry/forms/queue-form";
import { TopicForm } from "@/components/registry/forms/topic-form";
import { SubscriptionForm } from "@/components/registry/forms/subscription-form";
import { RuleForm } from "@/components/registry/forms/rule-form";
import { RegistryEmptyState } from "@/components/registry/registry-empty-state";
import type { RegistryEntityType } from "@/lib/registry/types";

export default function RegistryNewEntityPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();

  const rawType = typeof params.entityType === "string" ? params.entityType : "";
  const entityType = useMemo<RegistryEntityType | null>(() => {
    const candidates: RegistryEntityType[] = ["Namespace", "Queue", "Topic", "Subscription", "Rule"];
    return candidates.find((t) => t.toLowerCase() === rawType.toLowerCase()) ?? null;
  }, [rawType]);

  if (!entityType) {
    return (
      <RegistryEmptyState
        variant="no-data"
        title="Unknown entity type"
        description={`'${rawType}' is not a supported registry entity type.`}
      />
    );
  }

  const defaultEnvironment = searchParams.get("environment") ?? undefined;
  const id = useMemo(() => globalThis.crypto.randomUUID(), []);
  const onSaved = (e: { id: string; entityType: RegistryEntityType }) => {
    router.push(`/registry/${e.entityType}/${e.id}` as Route);
  };
  const onCancel = () => router.back();

  const commonProps = {
    mode: "create" as const,
    id,
    ...(defaultEnvironment ? { defaultEnvironment } : {}),
    onSaved,
    onCancel,
  };
  switch (entityType) {
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
