"use client";

/**
 * Spec 006 / T102 / FR-030. Delete-confirmation modal. Clearly communicates
 * the block-with-children policy so the operator knows ahead of time that
 * non-leaf entities can't be deleted.
 */

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import type { HasChildrenResponse, RegistryEntity } from "@/lib/registry/types";

interface RegistryDeleteConfirmationProps {
  readonly open: boolean;
  readonly entity: RegistryEntity | null;
  readonly hasChildrenResponse: HasChildrenResponse | null;
  readonly busy: boolean;
  readonly onConfirm: () => void;
  readonly onCancel: () => void;
}

export function RegistryDeleteConfirmation({
  open,
  entity,
  hasChildrenResponse,
  busy,
  onConfirm,
  onCancel,
}: RegistryDeleteConfirmationProps) {
  if (!entity) return null;

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onCancel()}>
      <DialogContent data-testid="registry-delete-confirmation">
        <DialogHeader>
          <DialogTitle>Delete {entity.entityType.toLowerCase()}?</DialogTitle>
          <DialogDescription>
            You&apos;re about to delete <strong className="font-mono">{entity.name}</strong> in
            environment <strong>{entity.environment}</strong>. This action cannot be undone.
          </DialogDescription>
        </DialogHeader>

        <Alert intent="warning">
          <AlertTitle>Deletes are blocked when children exist</AlertTitle>
          <AlertDescription>
            BusTerminal will reject the delete if this entity has registered children. Remove
            them first or change their parent.
          </AlertDescription>
        </Alert>

        {hasChildrenResponse ? (
          <Alert intent="error" data-testid="delete-blocked-by-children">
            <AlertTitle>Cannot delete — this entity has children</AlertTitle>
            <AlertDescription>
              {hasChildrenResponse.totalChildren} child
              {hasChildrenResponse.totalChildren === 1 ? "" : "ren"}:{" "}
              {Object.entries(hasChildrenResponse.childrenByType)
                .map(([type, count]) => `${count} × ${type}`)
                .join(", ")}
              .
            </AlertDescription>
          </Alert>
        ) : null}

        <DialogFooter className="gap-2">
          <Button intent="secondary" onClick={onCancel} disabled={busy}>
            Cancel
          </Button>
          <Button
            intent="destructive"
            onClick={onConfirm}
            disabled={busy || hasChildrenResponse !== null}
            data-testid="delete-confirm"
          >
            {busy ? "Deleting…" : "Delete"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
