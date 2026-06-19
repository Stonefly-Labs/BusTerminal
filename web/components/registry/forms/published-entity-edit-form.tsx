"use client";

/**
 * Spec 009 / T112 / US4.
 *
 * Edit form for a published entity's curated metadata. Wraps
 * `updateEntityMetadata` with optimistic-update + 412/409 conflict-modal
 * handling, surfaces the `<ServiceAssociationEditor>` trigger, and renders
 * an Archive button gated by `canEditEntityMetadata`. Azure-sourced fields
 * are intentionally read-only on the detail page — there is no input for
 * them here.
 *
 * The form uses RHF + Zod for validation. Server-side validation runs the
 * same shape via the FluentValidation pipeline; mismatches surface as a
 * problem-detail toast/inline message.
 */

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import type { Route } from "next";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";

import { ServiceAssociationEditor } from "@/components/discovery/service-association-editor";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useAcquireToken } from "@/hooks/use-acquire-token";
import { useOwnedServices } from "@/hooks/use-owned-services";
import { useRoles } from "@/hooks/use-roles";
import { useToast } from "@/hooks/use-toast";
import {
  archiveEntity,
  DiscoveryApiError,
  updateEntityMetadata,
} from "@/lib/discovery/api";
import { canEditEntityMetadata } from "@/lib/discovery/permissions";
import {
  updateEntityMetadataRequestSchema,
  type PublishedEntity,
  type UpdateEntityMetadataRequest,
} from "@/lib/discovery/schemas";

interface PublishedEntityEditFormProps {
  readonly entity: PublishedEntity;
  readonly etag: string;
}

const TAGS_DELIMITER = ",";

interface FormShape {
  description: string;
  businessPurpose: string;
  tagsCsv: string;
  operationalNotes: string;
  primaryContact: string;
  escalationPath: string;
}

export function PublishedEntityEditForm({ entity, etag }: PublishedEntityEditFormProps) {
  const router = useRouter();
  const getToken = useAcquireToken();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const ownedServices = useOwnedServices();
  const roles = useRoles();
  const [currentEtag, setCurrentEtag] = useState(etag);
  const [conflictBanner, setConflictBanner] = useState<string | null>(null);

  const canEdit = useMemo(
    () =>
      canEditEntityMetadata(
        entity,
        { roles: roles as ReadonlySet<string> },
        ownedServices.data,
      ),
    [entity, roles, ownedServices.data],
  );

  const form = useForm<FormShape>({
    defaultValues: {
      description: entity.description ?? "",
      businessPurpose: entity.businessPurpose ?? "",
      tagsCsv: (entity.tags ?? []).join(", "),
      operationalNotes: entity.operationalNotes ?? "",
      primaryContact: entity.contactInformation?.primaryContact ?? "",
      escalationPath: entity.contactInformation?.escalationPath ?? "",
    },
  });

  const updateMutation = useMutation({
    mutationFn: async (body: UpdateEntityMetadataRequest) => {
      const validated = updateEntityMetadataRequestSchema.parse(body);
      const token = await getToken();
      return updateEntityMetadata(entity.id, currentEtag, validated, token ? { accessToken: token } : {});
    },
    onSuccess: (result) => {
      if (!result.ok) {
        if (result.conflict.status === 412) {
          setConflictBanner(
            "Someone else updated this entity. Refresh to pull the latest version, then re-apply your changes.",
          );
          return;
        }
        toast.error("Update failed", {
          description: `Server rejected the request (${result.conflict.status}).`,
        });
        return;
      }
      setCurrentEtag(result.etag);
      setConflictBanner(null);
      queryClient.setQueryData(["discovery", "entity", entity.id], { entity: result.entity, etag: result.etag });
      toast.success("Saved", { description: "Entity metadata updated." });
    },
    onError: (err) => {
      const message = err instanceof DiscoveryApiError ? `Server rejected the request (${err.status}).` : "Unknown error.";
      toast.error("Update failed", { description: message });
    },
  });

  const archiveMutation = useMutation({
    mutationFn: async () => {
      const token = await getToken();
      return archiveEntity(entity.id, currentEtag, token ? { accessToken: token } : {});
    },
    onSuccess: (result) => {
      if (!result.ok) {
        if (result.conflict.status === 412) {
          setConflictBanner(
            "The entity changed in the background. Refresh and try again.",
          );
          return;
        }
        toast.error("Archive failed", {
          description: `Server rejected the request (${result.conflict.status}).`,
        });
        return;
      }
      setCurrentEtag(result.etag);
      queryClient.setQueryData(["discovery", "entity", entity.id], { entity: result.entity, etag: result.etag });
      toast.success("Archived", { description: "Entity moved to Archived." });
      router.push(`/registry/${result.entity.entityType}/${result.entity.id}` as Route);
    },
  });

  if (!canEdit) {
    return (
      <Alert intent="warning" data-testid="published-entity-edit-denied">
        <AlertDescription>
          You are not authorized to edit this entity. Editing requires Platform Admin,
          Namespace Administrator, or Service Owner standing on an Owner-role association.
        </AlertDescription>
      </Alert>
    );
  }

  const onSubmit = form.handleSubmit(async (data) => {
    const body: UpdateEntityMetadataRequest = {
      description: data.description === "" ? null : data.description,
      businessPurpose: data.businessPurpose === "" ? null : data.businessPurpose,
      tags: data.tagsCsv
        .split(TAGS_DELIMITER)
        .map((t) => t.trim())
        .filter((t) => t.length > 0),
      operationalNotes: data.operationalNotes === "" ? null : data.operationalNotes,
      contactInformation:
        data.primaryContact || data.escalationPath
          ? {
              primaryContact: data.primaryContact || undefined,
              escalationPath: data.escalationPath || undefined,
            }
          : null,
    };
    await updateMutation.mutateAsync(body);
  });

  return (
    <div className="flex flex-col gap-4" data-testid="published-entity-edit">
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground-default">{entity.name}</h1>
          <p className="text-sm text-foreground-muted">
            {entity.entityType} · {entity.environment} · namespace {entity.namespaceId}
          </p>
        </div>
        <div className="flex gap-2">
          <ServiceAssociationEditor
            entityId={entity.id}
            initialAssociations={entity.serviceAssociations}
            etag={currentEtag}
            onMutated={(newEtag) => setCurrentEtag(newEtag)}
          />
          <Button
            intent="destructive"
            onClick={() => archiveMutation.mutate()}
            disabled={archiveMutation.isPending || entity.lifecycleStatus === "Archived"}
            data-testid="archive-entity-button"
          >
            {entity.lifecycleStatus === "Archived" ? "Archived" : "Archive"}
          </Button>
        </div>
      </header>

      {conflictBanner ? (
        <Alert intent="error" data-testid="conflict-banner">
          <AlertDescription>{conflictBanner}</AlertDescription>
        </Alert>
      ) : null}

      <form onSubmit={onSubmit} className="flex flex-col gap-4" data-testid="published-entity-edit-form">
        <div className="flex flex-col gap-1">
          <Label htmlFor="description">Description</Label>
          <Textarea id="description" rows={3} {...form.register("description")} />
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="businessPurpose">Business purpose</Label>
          <Textarea id="businessPurpose" rows={2} {...form.register("businessPurpose")} />
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="tagsCsv">Tags (comma-separated)</Label>
          <Input id="tagsCsv" {...form.register("tagsCsv")} />
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="primaryContact">Primary contact</Label>
          <Input id="primaryContact" {...form.register("primaryContact")} />
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="escalationPath">Escalation path</Label>
          <Input id="escalationPath" {...form.register("escalationPath")} />
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="operationalNotes">Operational notes</Label>
          <Textarea id="operationalNotes" rows={3} {...form.register("operationalNotes")} />
        </div>
        <div className="flex justify-end gap-2">
          <Button
            type="button"
            intent="ghost"
            onClick={() => router.back()}
            data-testid="cancel-button"
          >
            Cancel
          </Button>
          <Button type="submit" disabled={updateMutation.isPending} data-testid="save-button">
            {updateMutation.isPending ? "Saving…" : "Save"}
          </Button>
        </div>
      </form>
    </div>
  );
}
