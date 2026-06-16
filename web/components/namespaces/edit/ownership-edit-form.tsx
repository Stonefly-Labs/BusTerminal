"use client";

/**
 * Spec 008 / T135 / US3. Ownership edit form — full-block replace.
 *
 * Reuses `<EntraPrincipalPicker>` per role; submits the entire OwnershipBlock
 * (PrimaryOwner + secondary owners + technical stewards + support contacts).
 */

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import { EntraPrincipalPicker, type PickedPrincipal } from "@/components/namespaces/shared/entra-principal-picker";
import * as NamespacesApi from "@/lib/namespaces/api";
import { namespaceKeys } from "@/lib/namespaces/query-keys";
import type {
  NamespaceDetailsResponse,
  OwnershipAssignment,
  OwnershipBlock,
} from "@/lib/namespaces/schemas";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export interface OwnershipEditFormProps {
  readonly namespace: NamespaceDetailsResponse;
  readonly etag: string;
  readonly onSuccess?: () => void;
}

interface RoleState {
  readonly primaryOwner: PickedPrincipal | null;
  readonly secondaryOwners: PickedPrincipal[];
  readonly technicalStewards: PickedPrincipal[];
  readonly supportContacts: PickedPrincipal[];
}

function toPicked(a: OwnershipAssignment): PickedPrincipal {
  return {
    objectId: a.objectId,
    principalType: a.principalType,
    displayName: a.displayNameSnapshot,
  };
}

function seed(ownership: OwnershipBlock | null | undefined): RoleState {
  if (!ownership) {
    return {
      primaryOwner: null,
      secondaryOwners: [],
      technicalStewards: [],
      supportContacts: [],
    };
  }
  return {
    primaryOwner: toPicked(ownership.primaryOwner),
    secondaryOwners: ownership.secondaryOwners.map(toPicked),
    technicalStewards: ownership.technicalStewards.map(toPicked),
    supportContacts: ownership.supportContacts.map(toPicked),
  };
}

export function OwnershipEditForm({ namespace, etag, onSuccess }: OwnershipEditFormProps) {
  const getToken = useAcquireToken();
  const queryClient = useQueryClient();
  const [state, setState] = useState<RoleState>(() => seed(namespace.ownership));
  const [conflict, setConflict] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async () => {
      if (!state.primaryOwner || !state.primaryOwner.objectId) {
        throw new Error("Primary owner is required.");
      }
      const now = new Date().toISOString();
      const assignedBy = "00000000-0000-0000-0000-000000000000";
      const body = {
        id: namespace.id,
        ownership: {
          primaryOwner: composeAssignment(state.primaryOwner, "PrimaryOwner", now, assignedBy),
          secondaryOwners: state.secondaryOwners
            .filter((p) => p.objectId)
            .map((p) => composeAssignment(p, "SecondaryOwner", now, assignedBy)),
          technicalStewards: state.technicalStewards
            .filter((p) => p.objectId)
            .map((p) => composeAssignment(p, "TechnicalSteward", now, assignedBy)),
          supportContacts: state.supportContacts
            .filter((p) => p.objectId)
            .map((p) => composeAssignment(p, "SupportContact", now, assignedBy)),
        },
      };
      const token = await getToken();
      return NamespacesApi.updateOwnership(body, etag, token ? { accessToken: token } : {});
    },
    onSuccess: () => {
      setConflict(false);
      setValidationError(null);
      queryClient.invalidateQueries({ queryKey: namespaceKeys.details(namespace.id) });
      onSuccess?.();
    },
    onError: (error: unknown) => {
      if (error instanceof NamespacesApi.NamespacesApiError) {
        if (error.status === 409) setConflict(true);
        else setValidationError(`Update failed (${error.status}).`);
      } else if (error instanceof Error) {
        setValidationError(error.message);
      } else {
        setValidationError("Update failed.");
      }
    },
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Ownership</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {conflict ? (
          <div
            className="rounded border border-warning-foreground/40 bg-warning-surface/30 p-3 text-sm text-warning-foreground"
            role="alert"
          >
            This namespace was updated by another user since you opened the form. Reload to see the
            latest ownership block, then re-apply your changes.
          </div>
        ) : null}
        {validationError ? (
          <div className="text-sm text-error-foreground" role="alert">
            {validationError}
          </div>
        ) : null}

        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Primary owner *</CardTitle>
          </CardHeader>
          <CardContent>
            <EntraPrincipalPicker
              label="Primary owner"
              required
              value={state.primaryOwner}
              onChange={(v) => setState((s) => ({ ...s, primaryOwner: v }))}
              testIdPrefix="ownership-edit-primary"
            />
          </CardContent>
        </Card>

        <RoleList
          title="Secondary owners"
          items={state.secondaryOwners}
          onChange={(items) => setState((s) => ({ ...s, secondaryOwners: items }))}
          testIdPrefix="ownership-edit-secondary"
        />
        <RoleList
          title="Technical stewards"
          items={state.technicalStewards}
          onChange={(items) => setState((s) => ({ ...s, technicalStewards: items }))}
          testIdPrefix="ownership-edit-stewards"
        />
        <RoleList
          title="Support contacts"
          items={state.supportContacts}
          onChange={(items) => setState((s) => ({ ...s, supportContacts: items }))}
          testIdPrefix="ownership-edit-support"
        />

        <div className="flex justify-end gap-2">
          <Button
            type="button"
            onClick={() => mutation.mutate()}
            disabled={!state.primaryOwner || !state.primaryOwner.objectId || mutation.isPending}
            data-testid="ownership-edit-submit"
          >
            {mutation.isPending ? "Saving…" : "Save ownership"}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function composeAssignment(
  picked: PickedPrincipal,
  role: OwnershipAssignment["role"],
  assignedAtUtc: string,
  assignedBy: string,
): OwnershipAssignment {
  return {
    role,
    principalType: picked.principalType,
    objectId: picked.objectId,
    displayNameSnapshot: picked.displayName,
    assignedAtUtc,
    assignedBy,
  };
}

function RoleList({
  title,
  items,
  onChange,
  testIdPrefix,
}: {
  readonly title: string;
  readonly items: PickedPrincipal[];
  readonly onChange: (items: PickedPrincipal[]) => void;
  readonly testIdPrefix: string;
}) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle className="text-sm">{title}</CardTitle>
        <Button
          type="button"
          size="sm"
          intent="outline"
          onClick={() => onChange([...items, { objectId: "", principalType: "User", displayName: "" }])}
          data-testid={`${testIdPrefix}-add`}
        >
          Add
        </Button>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {items.length === 0 ? (
          <p className="text-xs text-foreground-muted">None assigned.</p>
        ) : (
          items.map((item, index) => (
            <div key={index} className="flex items-start gap-3">
              <div className="flex-1">
                <EntraPrincipalPicker
                  label={`Slot ${index + 1}`}
                  value={item.objectId ? item : null}
                  onChange={(v) => {
                    const next = items.slice();
                    if (v === null) {
                      next.splice(index, 1);
                    } else {
                      next[index] = v;
                    }
                    onChange(next);
                  }}
                  testIdPrefix={`${testIdPrefix}-${index}`}
                />
              </div>
              <Button
                type="button"
                size="sm"
                intent="ghost"
                onClick={() => onChange(items.filter((_, i) => i !== index))}
                data-testid={`${testIdPrefix}-${index}-remove`}
              >
                Remove
              </Button>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}
