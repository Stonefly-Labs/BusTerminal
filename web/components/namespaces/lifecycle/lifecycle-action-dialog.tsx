"use client";

/**
 * Spec 008 / T136 / US3 / FR-023. Lifecycle action dialog.
 *
 * Confirms the selected action (disable | enable | archive | restore), gates
 * the confirm button on a reason note when the action requires one, and
 * dispatches the transition mutation. Reuses the spec-001 `Dialog` primitive.
 */

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import * as NamespacesApi from "@/lib/namespaces/api";
import { namespaceKeys } from "@/lib/namespaces/query-keys";
import {
  isActionPermitted,
  requiresReason,
} from "@/lib/namespaces/lifecycle";
import type { LifecycleAction, LifecycleStatus } from "@/lib/namespaces/schemas";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

export interface LifecycleActionDialogProps {
  readonly namespaceId: string;
  readonly currentStatus: LifecycleStatus;
  readonly etag: string;
  readonly action: LifecycleAction | null;
  readonly onClose: () => void;
  readonly onSuccess?: (() => void) | undefined;
}

const ACTION_LABELS: Record<LifecycleAction, { title: string; description: string }> = {
  disable: {
    title: "Disable namespace",
    description:
      "Disabled namespaces stay registered and visible in inventory but are flagged as operationally paused.",
  },
  enable: {
    title: "Enable namespace",
    description:
      "Re-enables the namespace and automatically runs a fresh validation to verify the namespace is still reachable.",
  },
  archive: {
    title: "Archive namespace",
    description:
      "Archived namespaces are hidden from the default inventory view but retain their full history for audit.",
  },
  restore: {
    title: "Restore namespace",
    description:
      "Restores an archived namespace back to the Disabled state. From there it can be enabled when ready.",
  },
};

export function LifecycleActionDialog({
  namespaceId,
  currentStatus,
  etag,
  action,
  onClose,
  onSuccess,
}: LifecycleActionDialogProps) {
  const getToken = useAcquireToken();
  const queryClient = useQueryClient();
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  const reset = () => {
    setReason("");
    setError(null);
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const mutation = useMutation({
    mutationFn: async () => {
      if (action === null) {
        throw new Error("No action selected.");
      }
      const token = await getToken();
      return NamespacesApi.transitionLifecycle(
        {
          id: namespaceId,
          action,
          reason: requiresReason(action) ? reason.trim() : null,
        },
        etag,
        token ? { accessToken: token } : {},
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: namespaceKeys.details(namespaceId) });
      onSuccess?.();
      handleClose();
    },
    onError: (err: unknown) => {
      if (err instanceof NamespacesApi.NamespacesApiError) {
        setError(`Transition failed (${err.status}).`);
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Transition failed.");
      }
    },
  });

  if (action === null) return null;

  const permitted = isActionPermitted(currentStatus, action);
  const needsReason = requiresReason(action);
  const canConfirm = permitted && (!needsReason || reason.trim().length > 0);
  const labels = ACTION_LABELS[action];

  return (
    <Dialog open={action !== null} onOpenChange={(open) => { if (!open) handleClose(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{labels.title}</DialogTitle>
          <DialogDescription>{labels.description}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3 py-2">
          {!permitted ? (
            <div
              className="rounded border border-error-foreground/40 bg-error-surface/30 p-3 text-sm text-error-foreground"
              role="alert"
            >
              Cannot {action} a namespace in the {currentStatus} state.
            </div>
          ) : null}

          {needsReason ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="lifecycle-reason">Reason *</Label>
              <Textarea
                id="lifecycle-reason"
                rows={3}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="Why is this transition needed?"
                data-testid="lifecycle-reason"
                required
              />
            </div>
          ) : null}

          {error ? (
            <div className="text-sm text-error-foreground" role="alert">
              {error}
            </div>
          ) : null}
        </div>

        <DialogFooter>
          <DialogClose asChild>
            <Button type="button" intent="outline">
              Cancel
            </Button>
          </DialogClose>
          <Button
            type="button"
            onClick={() => mutation.mutate()}
            disabled={!canConfirm || mutation.isPending}
            data-testid="lifecycle-confirm"
          >
            {mutation.isPending ? "Applying…" : `Confirm ${action}`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
