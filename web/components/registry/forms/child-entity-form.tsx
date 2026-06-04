"use client";

/**
 * Spec 006 / T095-T098. Shared scaffold for child entity forms (Queue, Topic,
 * Subscription, Rule). Each per-type form passes its expected parent type;
 * this component handles the picker + the Deprecated-parent warning + the
 * submit pipeline.
 */

import { useState } from "react";
import { useForm, FormProvider } from "react-hook-form";

import type { RegistryEntity, RegistryEntityType } from "@/lib/registry/types";

import { EntityFormFields, type EntityFormValues } from "./shared/entity-form-fields";
import { EntityFormShell } from "./shared/entity-form-shell";
import { ParentPicker } from "./shared/parent-picker";
import { useEntityForm } from "./shared/use-entity-form";
import { RegistryConflictModal } from "../registry-conflict-modal";

interface ChildEntityFormProps {
  readonly mode: "create" | "edit";
  readonly entityType: Exclude<RegistryEntityType, "Namespace">;
  readonly parentType: RegistryEntityType;
  readonly id: string;
  readonly defaultEnvironment?: string | undefined;
  readonly persistedEntity?: RegistryEntity | undefined;
  readonly persistedEtag?: string | undefined;
  readonly onSaved?: ((entity: RegistryEntity) => void) | undefined;
  readonly onCancel?: (() => void) | undefined;
}

export function ChildEntityForm({
  mode,
  entityType,
  parentType,
  id,
  defaultEnvironment,
  persistedEntity,
  persistedEtag,
  onSaved,
  onCancel,
}: ChildEntityFormProps) {
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

  const [parentId, setParentId] = useState<string | undefined>(
    persistedEntity?.parentId ?? undefined,
  );

  const form = useEntityForm({
    mode,
    entityType,
    id,
    persistedEtag,
    persistedEntity,
    parentId,
    onSaved,
  });

  const environment = methods.watch("environment");

  return (
    <FormProvider {...methods}>
      <EntityFormShell
        title={mode === "create" ? `Register ${entityType}` : `Edit ${entityType}`}
        state={form.state}
        errorMessage={form.errorMessage}
        successMessage={form.successMessage}
        canSubmit={!!parentId && !!environment}
        onSubmit={methods.handleSubmit((values) => form.submit(values))}
        onCancel={onCancel}
      >
        {environment ? (
          <ParentPicker
            parentType={parentType}
            environment={environment}
            value={parentId}
            onChange={(id) => setParentId(id)}
          />
        ) : (
          <p className="text-sm text-foreground-muted">
            Enter an environment first to see the available parents.
          </p>
        )}
        <EntityFormFields entityType={entityType} environmentReadOnly={mode === "edit"} />
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
