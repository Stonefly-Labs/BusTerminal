"use client";

/**
 * Spec 006 / T095. Queue form — child of Namespace. Surfaces the
 * Deprecated-parent warning via ChildEntityForm + ParentPicker.
 */

import type { RegistryEntity } from "@/lib/registry/types";

import { ChildEntityForm } from "./child-entity-form";

interface QueueFormProps {
  readonly mode: "create" | "edit";
  readonly id: string;
  readonly defaultEnvironment?: string | undefined;
  readonly persistedEntity?: RegistryEntity | undefined;
  readonly persistedEtag?: string | undefined;
  readonly onSaved?: ((entity: RegistryEntity) => void) | undefined;
  readonly onCancel?: (() => void) | undefined;
}

export function QueueForm(props: QueueFormProps) {
  return <ChildEntityForm {...props} entityType="Queue" parentType="Namespace" />;
}
