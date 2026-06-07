"use client";

/**
 * Spec 006 / T097. Subscription form — child of Topic.
 */

import type { RegistryEntity } from "@/lib/registry/types";

import { ChildEntityForm } from "./child-entity-form";

interface SubscriptionFormProps {
  readonly mode: "create" | "edit";
  readonly id: string;
  readonly defaultEnvironment?: string | undefined;
  readonly persistedEntity?: RegistryEntity | undefined;
  readonly persistedEtag?: string | undefined;
  readonly onSaved?: ((entity: RegistryEntity) => void) | undefined;
  readonly onCancel?: (() => void) | undefined;
}

export function SubscriptionForm(props: SubscriptionFormProps) {
  return <ChildEntityForm {...props} entityType="Subscription" parentType="Topic" />;
}
