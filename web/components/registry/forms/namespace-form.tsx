"use client";

/**
 * Spec 006 / T094. Namespace form (no parent picker).
 */

import { useForm, FormProvider } from "react-hook-form";

import type { RegistryEntity } from "@/lib/registry/types";

import { EntityFormFields, type EntityFormValues } from "./shared/entity-form-fields";
import { EntityFormShell } from "./shared/entity-form-shell";
import { useEntityForm } from "./shared/use-entity-form";
import { RegistryConflictModal } from "../registry-conflict-modal";

interface NamespaceFormProps {
  readonly mode: "create" | "edit";
  readonly id: string;
  readonly defaultEnvironment?: string | undefined;
  readonly persistedEntity?: RegistryEntity | undefined;
  readonly persistedEtag?: string | undefined;
  readonly onSaved?: ((entity: RegistryEntity) => void) | undefined;
  readonly onCancel?: (() => void) | undefined;
}

export function NamespaceForm({
  mode,
  id,
  defaultEnvironment,
  persistedEntity,
  persistedEtag,
  onSaved,
  onCancel,
}: NamespaceFormProps) {
  const methods = useForm<EntityFormValues>({
    defaultValues: {
      name: persistedEntity?.name ?? "",
      environment: persistedEntity?.environment ?? defaultEnvironment ?? "",
      owner: persistedEntity?.owner ?? undefined,
      description: persistedEntity?.description ?? undefined,
      azureResourceId: persistedEntity?.azureResourceId ?? undefined,
      tags: persistedEntity?.tags ?? [],
    },
  });

  const form = useEntityForm({
    mode,
    entityType: "Namespace",
    id,
    persistedEtag,
    persistedEntity,
    onSaved,
  });

  return (
    <FormProvider {...methods}>
      <EntityFormShell
        title={mode === "create" ? "Register namespace" : `Edit namespace`}
        description={
          mode === "create"
            ? "Register an Azure Service Bus namespace as the root of a messaging hierarchy."
            : undefined
        }
        state={form.state}
        errorMessage={form.errorMessage}
        successMessage={form.successMessage}
        canSubmit={methods.formState.isValid || !methods.formState.isSubmitted}
        onSubmit={methods.handleSubmit((values) => form.submit(values))}
        onCancel={onCancel}
      >
        <EntityFormFields entityType="Namespace" environmentReadOnly={mode === "edit"} />
      </EntityFormShell>

      <RegistryConflictModal
        open={!!form.conflict}
        conflict={form.conflict}
        onDiscard={() => {
          form.clearConflict();
          onSaved?.(form.conflict!.currentEntity);
        }}
        onForceOverwrite={() => {
          const values = methods.getValues();
          void form.submit(values, { forceOverwrite: true });
          form.clearConflict();
        }}
        onClose={form.clearConflict}
      />
    </FormProvider>
  );
}
