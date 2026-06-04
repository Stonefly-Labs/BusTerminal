"use client";

/**
 * Spec 006. Shared field rendering for every registry entity form.
 * Per-type forms compose this and prepend their own parent-picker.
 */

import { useId } from "react";
import { Controller, useFormContext } from "react-hook-form";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/design-system/cn";
import type { RegistryEntityType, RegistryTag } from "@/lib/registry/types";

import { AzureResourceIdInput } from "./azure-resource-id-input";
import { RegistryTagEditor } from "../../registry-tag-editor";

export interface EntityFormValues {
  readonly name: string;
  readonly environment: string;
  readonly owner: string | undefined;
  readonly description: string | undefined;
  readonly azureResourceId: string | undefined;
  readonly tags: readonly RegistryTag[];
}

interface EntityFormFieldsProps {
  readonly entityType: RegistryEntityType;
  readonly environmentReadOnly?: boolean;
  readonly disabled?: boolean;
  readonly className?: string;
}

export function EntityFormFields({
  entityType,
  environmentReadOnly = false,
  disabled = false,
  className,
}: EntityFormFieldsProps) {
  const { register, control, formState } = useFormContext<EntityFormValues>();
  const nameId = useId();
  const envId = useId();
  const ownerId = useId();
  const descId = useId();

  return (
    <div className={cn("grid grid-cols-1 gap-4 md:grid-cols-2", className)}>
      <div className="flex flex-col gap-1">
        <Label htmlFor={nameId}>Name *</Label>
        <Input
          id={nameId}
          autoComplete="off"
          aria-invalid={formState.errors.name ? true : undefined}
          disabled={disabled}
          {...register("name")}
        />
        {formState.errors.name?.message ? (
          <p className="text-xs text-error-foreground">{String(formState.errors.name.message)}</p>
        ) : null}
      </div>

      <div className="flex flex-col gap-1">
        <Label htmlFor={envId}>Environment *</Label>
        <Input
          id={envId}
          autoComplete="off"
          disabled={disabled || environmentReadOnly}
          {...register("environment")}
        />
        {formState.errors.environment?.message ? (
          <p className="text-xs text-error-foreground">
            {String(formState.errors.environment.message)}
          </p>
        ) : null}
      </div>

      <div className="flex flex-col gap-1 md:col-span-2">
        <Label htmlFor={ownerId}>Owner</Label>
        <Input id={ownerId} autoComplete="off" disabled={disabled} {...register("owner")} />
      </div>

      <div className="flex flex-col gap-1 md:col-span-2">
        <Label htmlFor={descId}>Description</Label>
        <Textarea id={descId} rows={3} disabled={disabled} {...register("description")} />
      </div>

      <div className="md:col-span-2">
        <Controller
          control={control}
          name="azureResourceId"
          render={({ field }) => (
            <AzureResourceIdInput
              entityType={entityType}
              value={field.value ?? ""}
              onChange={(next) => field.onChange(next)}
              disabled={disabled}
            />
          )}
        />
      </div>

      <div className="md:col-span-2">
        <Controller
          control={control}
          name="tags"
          render={({ field }) => (
            <RegistryTagEditor
              value={field.value}
              onChange={(next) => field.onChange(next)}
              disabled={disabled}
            />
          )}
        />
      </div>
    </div>
  );
}
