"use client";

/**
 * Spec 006. Shared submit/state hook for entity create + edit forms. Handles
 * the API call (create or update), surfaces a ConflictResponse for the
 * conflict modal, and exposes the lifecycle state used by EntityFormShell.
 */

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";

import {
  createEntity,
  updateEntity,
  type RegistryApiOptions,
  type RegistryUpdateResult,
  type RegistryUpdateConflict,
} from "@/lib/registry/api";
import { registryQueryKeys } from "@/lib/registry/query-keys";
import type {
  ConflictResponse,
  RegistryEntity,
  RegistryEntityCreateRequest,
  RegistryEntityType,
  RegistryEntityUpdateRequest,
} from "@/lib/registry/types";

import type { EntityFormState } from "./entity-form-shell";
import type { EntityFormValues } from "./entity-form-fields";

interface UseEntityFormArgs {
  readonly mode: "create" | "edit";
  readonly entityType: RegistryEntityType;
  readonly id: string;
  readonly persistedEtag?: string | undefined;
  readonly persistedEntity?: RegistryEntity | undefined;
  readonly parentId?: string | undefined;
  readonly onSaved?: ((entity: RegistryEntity) => void) | undefined;
  readonly apiOptions?: RegistryApiOptions | undefined;
}

interface UseEntityFormResult {
  readonly state: EntityFormState;
  readonly errorMessage: string | undefined;
  readonly successMessage: string | undefined;
  readonly conflict: ConflictResponse | null;
  readonly submit: (values: EntityFormValues, opts?: { forceOverwrite?: boolean }) => Promise<void>;
  readonly clearConflict: () => void;
}

export function useEntityForm({
  mode,
  entityType,
  id,
  persistedEtag,
  persistedEntity,
  parentId,
  onSaved,
  apiOptions,
}: UseEntityFormArgs): UseEntityFormResult {
  const queryClient = useQueryClient();
  const [state, setState] = useState<EntityFormState>("idle");
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);
  const [successMessage, setSuccessMessage] = useState<string | undefined>(undefined);
  const [conflict, setConflict] = useState<ConflictResponse | null>(null);

  const submit = async (
    values: EntityFormValues,
    opts: { forceOverwrite?: boolean } = {},
  ): Promise<void> => {
    setState("submitting");
    setErrorMessage(undefined);

    const payload = {
      id,
      entityType,
      name: values.name,
      environment: values.environment,
      status: persistedEntity?.status ?? "Active",
      source: "Manual" as const,
      ...(values.owner ? { owner: values.owner } : {}),
      ...(values.description ? { description: values.description } : {}),
      ...(values.azureResourceId ? { azureResourceId: values.azureResourceId } : {}),
      tags: values.tags,
      ...(parentId ? { parentId } : {}),
    };

    try {
      if (mode === "create") {
        const result = await createEntity(payload as RegistryEntityCreateRequest, apiOptions);
        await queryClient.invalidateQueries({ queryKey: registryQueryKeys.entities.all });
        // T125 / FR-033 — the new entity's Created audit event should be visible
        // immediately on its detail page (quickstart §7 expectation).
        await queryClient.invalidateQueries({
          queryKey: registryQueryKeys.audit.forEntity(result.entity.id),
        });
        setState("saved");
        setSuccessMessage(`${entityType} '${result.entity.name}' created.`);
        onSaved?.(result.entity);
      } else {
        if (!persistedEtag) {
          throw new Error("Cannot update — missing ETag from initial load.");
        }
        const updateBody = {
          ...(payload as RegistryEntityUpdateRequest),
          ...(opts.forceOverwrite ? { _overwriteAcknowledged: true } : {}),
        } as RegistryEntityUpdateRequest & { _overwriteAcknowledged?: boolean };
        const result: RegistryUpdateResult | RegistryUpdateConflict = await updateEntity(
          id,
          updateBody,
          persistedEtag,
          apiOptions,
        );
        if (!result.ok) {
          setConflict(result.conflict);
          setState("error");
          setErrorMessage(
            "The entity was modified by another writer. Review the changes and choose how to proceed.",
          );
          return;
        }
        await queryClient.invalidateQueries({ queryKey: registryQueryKeys.entities.detail(id) });
        await queryClient.invalidateQueries({ queryKey: registryQueryKeys.entities.all });
        // T125 / FR-033 — the audit panel must reflect the new Updated /
        // StatusChanged event without a manual refresh (quickstart §7).
        await queryClient.invalidateQueries({
          queryKey: registryQueryKeys.audit.forEntity(id),
        });
        setState("saved");
        setSuccessMessage(`${entityType} '${result.entity.name}' updated.`);
        onSaved?.(result.entity);
      }
    } catch (err) {
      setState("error");
      setErrorMessage(err instanceof Error ? err.message : "Unknown error.");
    }
  };

  const clearConflict = (): void => setConflict(null);

  return { state, errorMessage, successMessage, conflict, submit, clearConflict };
}
