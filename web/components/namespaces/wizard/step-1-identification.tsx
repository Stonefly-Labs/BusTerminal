"use client";

/**
 * Spec 008 / T086 — Step 1: Identification.
 *
 * Composes `<AzureResourceIdInput>` + `<GrantReaderGuidance>`. Advances only
 * when the ARM id is valid (format + not duplicate per the inline probe).
 */

import { useState } from "react";
import { useFormContext } from "react-hook-form";

import { AzureResourceIdInput, type AzureResourceIdValidationState } from "@/components/namespaces/shared/azure-resource-id-input";
import { GrantReaderGuidance } from "@/components/namespaces/wizard/grant-reader-guidance";
import { Button } from "@/components/ui/button";
import type { StepProps, WizardFormValues } from "./types";

export function Step1Identification({ goNext }: StepProps) {
  const { watch } = useFormContext<WizardFormValues>();
  const armId = watch("azureResourceId");
  const [validation, setValidation] = useState<AzureResourceIdValidationState>({ state: "idle" });
  const canAdvance = validation.state === "ok" || validation.state === "warning";

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_320px]">
      <div className="flex flex-col gap-4">
        <h2 className="text-base font-semibold text-foreground-default">Identify the namespace</h2>
        <p className="text-sm text-foreground-muted">
          Paste the full ARM resource id of the Azure Service Bus namespace you want to onboard.
          We&rsquo;ll verify the format and cross-tenancy as you type.
        </p>
        <AzureResourceIdInput
          name="azureResourceId"
          onValidationStateChange={setValidation}
        />
      </div>
      <aside aria-label="Grant Reader guidance">
        <GrantReaderGuidance azureResourceId={armId} />
      </aside>
      <div className="lg:col-span-2 mt-4 flex justify-end">
        <Button
          type="button"
          onClick={goNext}
          disabled={!canAdvance}
          data-testid="wizard-step-1-next"
        >
          Continue
        </Button>
      </div>
    </div>
  );
}
