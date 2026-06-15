"use client";

/**
 * Spec 008 / T134 / US3. Metadata edit form.
 *
 * RHF + Zod (mirror of the backend UpdateMetadataRequest). Optimistic
 * concurrency by carrying the loaded ETag through to PUT; on 409 the surface
 * shows a conflict banner and lets the operator reload-and-retry.
 */

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import * as NamespacesApi from "@/lib/namespaces/api";
import { namespaceKeys } from "@/lib/namespaces/query-keys";
import {
  updateMetadataRequestSchema,
  type NamespaceDetailsResponse,
  type UpdateMetadataRequest,
} from "@/lib/namespaces/schemas";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

export interface MetadataEditFormProps {
  readonly namespace: NamespaceDetailsResponse;
  readonly etag: string;
  readonly onSuccess?: () => void;
}

export function MetadataEditForm({ namespace, etag, onSuccess }: MetadataEditFormProps) {
  const getToken = useAcquireToken();
  const queryClient = useQueryClient();
  const [conflict, setConflict] = useState(false);

  const form = useForm<UpdateMetadataRequest>({
    resolver: zodResolver(updateMetadataRequestSchema),
    defaultValues: {
      id: namespace.id,
      displayName: namespace.displayName ?? namespace.name,
      description: namespace.description ?? undefined,
      businessUnit: namespace.businessUnit ?? undefined,
      productOrApplication: namespace.productOrApplication ?? undefined,
      costCenter: namespace.costCenter ?? undefined,
      notes: namespace.notes ?? undefined,
      tags: namespace.tags,
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: UpdateMetadataRequest) => {
      const token = await getToken();
      return NamespacesApi.updateMetadata(values, etag, token ? { accessToken: token } : {});
    },
    onSuccess: () => {
      setConflict(false);
      queryClient.invalidateQueries({ queryKey: namespaceKeys.details(namespace.id) });
      onSuccess?.();
    },
    onError: (error: unknown) => {
      if (error instanceof NamespacesApi.NamespacesApiError && error.status === 409) {
        setConflict(true);
      }
    },
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Metadata</CardTitle>
      </CardHeader>
      <CardContent>
        <form
          className="flex flex-col gap-4"
          onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
        >
          {conflict ? (
            <div
              className="rounded border border-warning-foreground/40 bg-warning-surface/30 p-3 text-sm text-warning-foreground"
              role="alert"
            >
              This namespace was updated by another user since you opened the form. Reload to see the
              latest values, then re-apply your changes.
            </div>
          ) : null}

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Field label="Display name *" error={form.formState.errors.displayName?.message}>
              <Input
                {...form.register("displayName")}
                data-testid="metadata-edit-display-name"
              />
            </Field>
            <Field label="Business unit" error={form.formState.errors.businessUnit?.message}>
              <Input {...form.register("businessUnit")} />
            </Field>
            <Field label="Product / application" error={form.formState.errors.productOrApplication?.message}>
              <Input {...form.register("productOrApplication")} />
            </Field>
            <Field label="Cost center" error={form.formState.errors.costCenter?.message}>
              <Input {...form.register("costCenter")} />
            </Field>
            <Field
              label="Description"
              className="md:col-span-2"
              error={form.formState.errors.description?.message}
            >
              <Textarea rows={3} {...form.register("description")} />
            </Field>
            <Field
              label="Notes"
              className="md:col-span-2"
              error={form.formState.errors.notes?.message}
            >
              <Textarea rows={3} {...form.register("notes")} />
            </Field>
          </div>

          <div className="flex justify-end gap-2">
            <Button
              type="submit"
              disabled={mutation.isPending}
              data-testid="metadata-edit-submit"
            >
              {mutation.isPending ? "Saving…" : "Save metadata"}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

function Field({
  label,
  error,
  className,
  children,
}: {
  readonly label: string;
  readonly error?: string | undefined;
  readonly className?: string | undefined;
  readonly children: React.ReactNode;
}) {
  return (
    <div className={`flex flex-col gap-1.5 ${className ?? ""}`}>
      <Label>{label}</Label>
      {children}
      {error ? (
        <p className="text-xs text-error-foreground" role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}
