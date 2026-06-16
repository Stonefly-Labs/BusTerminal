"use client";

/**
 * Spec 008 / T137 / US3. Lifecycle transition button group.
 *
 * Renders one button per permitted action for the current status; clicking a
 * button opens the LifecycleActionDialog with that action pre-selected.
 */

import { useState } from "react";
import { Archive, CircleOff, CirclePlay, Undo2 } from "lucide-react";

import {
  permittedActionsFor,
} from "@/lib/namespaces/lifecycle";
import type { LifecycleAction, LifecycleStatus } from "@/lib/namespaces/schemas";

import { Button } from "@/components/ui/button";

import { LifecycleActionDialog } from "./lifecycle-action-dialog";

export interface LifecycleTransitionButtonsProps {
  readonly namespaceId: string;
  readonly currentStatus: LifecycleStatus;
  readonly etag: string;
  readonly onSuccess?: () => void;
}

const ACTION_LABELS: Record<LifecycleAction, string> = {
  disable: "Disable",
  enable: "Enable",
  archive: "Archive",
  restore: "Restore",
};

function iconFor(action: LifecycleAction) {
  switch (action) {
    case "disable":
      return CircleOff;
    case "enable":
      return CirclePlay;
    case "archive":
      return Archive;
    case "restore":
      return Undo2;
  }
}

export function LifecycleTransitionButtons({
  namespaceId,
  currentStatus,
  etag,
  onSuccess,
}: LifecycleTransitionButtonsProps) {
  const [selectedAction, setSelectedAction] = useState<LifecycleAction | null>(null);
  const actions = permittedActionsFor(currentStatus);

  if (actions.length === 0) {
    return (
      <p className="text-sm text-foreground-muted">
        No lifecycle transitions available from the {currentStatus} state.
      </p>
    );
  }

  return (
    <>
      <div className="flex flex-wrap gap-2">
        {actions.map((action) => {
          const Icon = iconFor(action);
          return (
            <Button
              key={action}
              type="button"
              intent="outline"
              onClick={() => setSelectedAction(action)}
              data-testid={`lifecycle-action-${action}`}
            >
              <Icon className="me-1 size-4" aria-hidden="true" />
              {ACTION_LABELS[action]}
            </Button>
          );
        })}
      </div>
      <LifecycleActionDialog
        namespaceId={namespaceId}
        currentStatus={currentStatus}
        etag={etag}
        action={selectedAction}
        onClose={() => setSelectedAction(null)}
        onSuccess={onSuccess}
      />
    </>
  );
}
