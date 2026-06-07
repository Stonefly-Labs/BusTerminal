"use client";

/**
 * Spec 006 / T101 / FR-020. Concurrency-conflict modal. Composes the shadcn
 * Dialog primitive with the diff renderer from `lib/registry/conflict.ts`.
 * Two CTAs:
 *   - "Discard my changes and refresh" → calls onDiscard with the current
 *     server-side entity so the form can reset to it.
 *   - "Force overwrite" → calls onForceOverwrite; the parent then issues a
 *     PUT with `_overwriteAcknowledged: true` and the current ETag.
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
import { cn } from "@/lib/design-system/cn";
import type { ConflictResponse } from "@/lib/registry/types";

interface RegistryConflictModalProps {
  readonly open: boolean;
  readonly conflict: ConflictResponse | null;
  readonly onDiscard: (currentEntity: ConflictResponse["currentEntity"]) => void;
  readonly onForceOverwrite: () => void;
  readonly onClose: () => void;
}

export function RegistryConflictModal({
  open,
  conflict,
  onDiscard,
  onForceOverwrite,
  onClose,
}: RegistryConflictModalProps) {
  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent
        data-testid="registry-conflict-modal"
        className="max-w-2xl"
      >
        <DialogHeader>
          <DialogTitle>Conflict detected</DialogTitle>
          <DialogDescription>
            The entity changed since you loaded it. Choose how to proceed.
          </DialogDescription>
        </DialogHeader>

        {conflict ? (
          <div className="flex max-h-[50vh] flex-col gap-3 overflow-y-auto">
            <p className="text-sm text-foreground-muted">
              {conflict.changedFields.length} field
              {conflict.changedFields.length === 1 ? "" : "s"} changed by another writer:
            </p>
            <ul className="flex flex-col gap-2">
              {conflict.changedFields.map((change) => (
                <li
                  key={change.field}
                  className={cn(
                    "rounded-md border border-border-default bg-surface-canvas p-3 text-sm",
                  )}
                >
                  <p className="mb-1 font-mono text-xs text-foreground-muted">{change.field}</p>
                  <div className="grid grid-cols-2 gap-3 text-xs">
                    <div>
                      <p className="font-medium text-foreground-muted">Current (server)</p>
                      <pre className="mt-1 break-all font-mono">
                        {JSON.stringify(change.currentValue, null, 2)}
                      </pre>
                    </div>
                    <div>
                      <p className="font-medium text-foreground-muted">Yours (submitted)</p>
                      <pre className="mt-1 break-all font-mono">
                        {JSON.stringify(change.submittedValue, null, 2)}
                      </pre>
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        ) : null}

        <DialogFooter className="gap-2">
          <Button
            intent="secondary"
            onClick={() => conflict && onDiscard(conflict.currentEntity)}
            data-testid="conflict-discard"
          >
            Discard my changes and refresh
          </Button>
          <Button
            intent="destructive"
            onClick={onForceOverwrite}
            data-testid="conflict-force-overwrite"
          >
            Force overwrite
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
