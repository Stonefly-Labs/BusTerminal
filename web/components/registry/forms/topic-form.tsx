"use client";

/**
 * Spec 006 / T096. Topic form — child of Namespace. Same Deprecated-parent
 * behaviour as QueueForm (Story 1 AC #7).
 */

import type { RegistryEntity } from "@/lib/registry/types";

import { ChildEntityForm } from "./child-entity-form";

interface TopicFormProps {
  readonly mode: "create" | "edit";
  readonly id: string;
  readonly defaultEnvironment?: string | undefined;
  readonly persistedEntity?: RegistryEntity | undefined;
  readonly persistedEtag?: string | undefined;
  readonly onSaved?: ((entity: RegistryEntity) => void) | undefined;
  readonly onCancel?: (() => void) | undefined;
}

export function TopicForm(props: TopicFormProps) {
  return <ChildEntityForm {...props} entityType="Topic" parentType="Namespace" />;
}
