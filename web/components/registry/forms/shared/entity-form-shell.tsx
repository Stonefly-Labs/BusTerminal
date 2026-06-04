"use client";

/**
 * Spec 006 / T092 / FR-029. Shared form scaffold for create + edit forms.
 *
 * Wraps RHF + Zod with:
 *   - submit / saving / saved / error states (FR-029)
 *   - an error surface that shows server-side validation messages
 *   - a hookup for the conflict modal (the parent supplies the conflict
 *     ConflictResponse via prop; this shell just renders the modal)
 */

import type { ReactNode } from "react";
import { Loader2 } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/design-system/cn";

export type EntityFormState = "idle" | "submitting" | "saved" | "error";

interface EntityFormShellProps {
  readonly title: string;
  readonly description?: string | undefined;
  readonly state: EntityFormState;
  readonly errorMessage?: string | undefined;
  readonly successMessage?: string | undefined;
  readonly canSubmit: boolean;
  readonly onSubmit: () => void;
  readonly onCancel?: (() => void) | undefined;
  readonly footerExtras?: ReactNode | undefined;
  readonly children: ReactNode;
  readonly className?: string | undefined;
}

export function EntityFormShell({
  title,
  description,
  state,
  errorMessage,
  successMessage,
  canSubmit,
  onSubmit,
  onCancel,
  footerExtras,
  children,
  className,
}: EntityFormShellProps) {
  return (
    <form
      data-testid="entity-form-shell"
      data-state={state}
      className={cn("flex flex-col gap-6", className)}
      onSubmit={(e) => {
        e.preventDefault();
        if (canSubmit && state !== "submitting") onSubmit();
      }}
      noValidate
    >
      <div>
        <h2 className="text-xl font-semibold text-foreground-default">{title}</h2>
        {description ? (
          <p className="mt-1 text-sm text-foreground-muted">{description}</p>
        ) : null}
      </div>

      {state === "error" && errorMessage ? (
        <Alert intent="error" data-testid="entity-form-error">
          <AlertTitle>Save failed</AlertTitle>
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      ) : null}

      {state === "saved" && successMessage ? (
        <Alert intent="success" data-testid="entity-form-saved">
          <AlertTitle>Saved</AlertTitle>
          <AlertDescription>{successMessage}</AlertDescription>
        </Alert>
      ) : null}

      <div className="flex flex-col gap-4">{children}</div>

      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" intent="primary" disabled={!canSubmit || state === "submitting"}>
          {state === "submitting" ? (
            <>
              <Loader2 className="me-1 size-4 animate-spin" aria-hidden="true" />
              Saving…
            </>
          ) : (
            "Save"
          )}
        </Button>
        {onCancel ? (
          <Button type="button" intent="secondary" onClick={onCancel}>
            Cancel
          </Button>
        ) : null}
        {footerExtras}
      </div>
    </form>
  );
}
