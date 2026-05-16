"use client";

import * as React from "react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { t } from "@/lib/i18n";
import type { StringKey } from "@/lib/i18n";

export interface DestructiveConfirmationProps {
  readonly titleKey?: StringKey;
  readonly descriptionKey?: StringKey;
  readonly confirmLabelKey?: StringKey;
  readonly cancelLabelKey?: StringKey;
  readonly onConfirm: () => Promise<void> | void;
}

/**
 * Returns a stable callback that opens a destructive-confirmation dialog
 * before invoking `onConfirm` (T078 / FR-017).
 */
export function useDestructiveConfirm({
  titleKey = "dialog.destructive.defaultTitle",
  descriptionKey = "dialog.destructive.defaultDescription",
  confirmLabelKey = "dialog.destructive.confirmLabel",
  cancelLabelKey = "dialog.destructive.cancelLabel",
  onConfirm,
}: DestructiveConfirmationProps) {
  const [open, setOpen] = React.useState(false);
  const [pending, setPending] = React.useState(false);

  const trigger = React.useCallback(() => setOpen(true), []);

  const dialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t(titleKey)}</DialogTitle>
          <DialogDescription>{t(descriptionKey)}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button intent="ghost" onClick={() => setOpen(false)}>
            {t(cancelLabelKey)}
          </Button>
          <Button
            intent="destructive"
            disabled={pending}
            onClick={async () => {
              setPending(true);
              try {
                await onConfirm();
                setOpen(false);
              } finally {
                setPending(false);
              }
            }}
          >
            {t(confirmLabelKey)}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  return { trigger, dialog };
}
