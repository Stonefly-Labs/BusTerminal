"use client";

/**
 * Spec 008 / T087 — Step 2: Business metadata.
 *
 * Display name (defaults from the parsed namespace name), description,
 * environment, business unit, product/application, cost center, notes.
 */

import { useEffect } from "react";
import { useFormContext } from "react-hook-form";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import type { StepProps, WizardFormValues } from "./types";

const ARM_NAMESPACE_NAME_PATTERN = /\/namespaces\/([^/]+)$/;

export function Step2Metadata({ goNext, goBack }: StepProps) {
  const { register, formState, watch, setValue, getValues } = useFormContext<WizardFormValues>();
  const armId = watch("azureResourceId");

  // Default display name to the parsed namespace name once.
  useEffect(() => {
    if (!armId) return;
    const current = getValues("displayName");
    if (current) return;
    const m = armId.match(ARM_NAMESPACE_NAME_PATTERN);
    if (m && m[1]) setValue("displayName", m[1]);
  }, [armId, getValues, setValue]);

  const displayName = watch("displayName");
  const environment = watch("environment");
  const canAdvance = Boolean(displayName?.trim()) && Boolean(environment?.trim());

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h2 className="text-base font-semibold text-foreground-default">Business metadata</h2>
        <p className="text-sm text-foreground-muted">
          These fields appear in inventory + details. Required fields are marked.
        </p>
      </div>
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Field label="Display name *" error={formState.errors.displayName?.message}>
          <Input
            id="wizard-display-name"
            autoComplete="off"
            aria-invalid={formState.errors.displayName ? true : undefined}
            {...register("displayName")}
            data-testid="wizard-step-2-display-name"
          />
        </Field>
        <Field label="Environment *" error={formState.errors.environment?.message}>
          <Input
            id="wizard-environment"
            autoComplete="off"
            placeholder="dev, test, prod"
            aria-invalid={formState.errors.environment ? true : undefined}
            {...register("environment")}
            data-testid="wizard-step-2-environment"
          />
        </Field>
        <Field label="Business unit" error={formState.errors.businessUnit?.message}>
          <Input id="wizard-business-unit" autoComplete="off" {...register("businessUnit")} />
        </Field>
        <Field label="Product / application" error={formState.errors.productOrApplication?.message}>
          <Input id="wizard-product" autoComplete="off" {...register("productOrApplication")} />
        </Field>
        <Field label="Cost center" error={formState.errors.costCenter?.message}>
          <Input id="wizard-cost-center" autoComplete="off" {...register("costCenter")} />
        </Field>
        <Field label="Description" error={formState.errors.description?.message} className="md:col-span-2">
          <Textarea id="wizard-description" rows={3} {...register("description")} />
        </Field>
        <Field label="Notes" error={formState.errors.notes?.message} className="md:col-span-2">
          <Textarea id="wizard-notes" rows={3} {...register("notes")} />
        </Field>
      </div>
      <div className="flex justify-between">
        <Button type="button" intent="outline" onClick={goBack}>
          Back
        </Button>
        <Button
          type="button"
          onClick={goNext}
          disabled={!canAdvance}
          data-testid="wizard-step-2-next"
        >
          Continue
        </Button>
      </div>
    </div>
  );
}

function Field({
  label,
  error,
  className,
  children,
}: {
  readonly label: string;
  readonly error?: unknown;
  readonly className?: string;
  readonly children: React.ReactNode;
}) {
  return (
    <div className={`flex flex-col gap-1.5 ${className ?? ""}`}>
      <Label>{label}</Label>
      {children}
      {error && typeof error === "string" ? (
        <p className="text-xs text-error-foreground" role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}
