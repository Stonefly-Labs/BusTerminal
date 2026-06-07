"use client";

/**
 * Spec 006 / T098. Rule form — child of Subscription.
 */

import type { RegistryEntity } from "@/lib/registry/types";

import { ChildEntityForm } from "./child-entity-form";

interface RuleFormProps {
  readonly mode: "create" | "edit";
  readonly id: string;
  readonly defaultEnvironment?: string | undefined;
  readonly persistedEntity?: RegistryEntity | undefined;
  readonly persistedEtag?: string | undefined;
  readonly onSaved?: ((entity: RegistryEntity) => void) | undefined;
  readonly onCancel?: (() => void) | undefined;
}

export function RuleForm(props: RuleFormProps) {
  return <ChildEntityForm {...props} entityType="Rule" parentType="Subscription" />;
}
