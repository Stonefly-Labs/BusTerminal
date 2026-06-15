"use client";

/**
 * Spec 008 / T091 — Wizard root.
 *
 * - One `useForm<WizardFormValues>` spanning all 5 steps.
 * - sessionStorage persistence via `wizard-storage.ts` (debounced 300ms).
 * - Back-navigation preserves state automatically.
 * - State is cleared on (a) successful registration, (b) explicit cancel,
 *   (c) window beforeunload.
 */

import { useEffect, useMemo, useState } from "react";
import { FormProvider, useForm } from "react-hook-form";
import { useRouter } from "next/navigation";

import { createWizardStorage } from "@/lib/namespaces/wizard-storage";
import type { ValidationRun } from "@/lib/namespaces/schemas";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Step1Identification } from "./step-1-identification";
import { Step2Metadata } from "./step-2-metadata";
import { Step3Ownership } from "./step-3-ownership";
import { Step4Validation } from "./step-4-validation";
import { Step5Review } from "./step-5-review";
import { WizardStepper, type WizardStep } from "./wizard-stepper";
import { INITIAL_WIZARD_VALUES, type WizardFormValues } from "./types";

const STEPS: readonly WizardStep[] = [
  { id: "identification", title: "Identification", description: "ARM resource id" },
  { id: "metadata", title: "Metadata", description: "Business details" },
  { id: "ownership", title: "Ownership", description: "Entra principals" },
  { id: "validation", title: "Validation", description: "5 named checks" },
  { id: "review", title: "Review", description: "Confirm & register" },
];

const wizardStorage = createWizardStorage<WizardFormValues>();

export function NamespaceOnboardingWizard() {
  const router = useRouter();
  const [stepIndex, setStepIndex] = useState(0);
  const [validationRun, setValidationRun] = useState<ValidationRun | null>(null);

  const initialValues = useMemo<WizardFormValues>(() => {
    const persisted = wizardStorage.load();
    return persisted ?? INITIAL_WIZARD_VALUES;
  }, []);

  const form = useForm<WizardFormValues>({
    defaultValues: initialValues,
    mode: "onChange",
  });

  // Debounced persistence on any form value change.
  useEffect(() => {
    const subscription = form.watch((values) => {
      wizardStorage.save(values as WizardFormValues);
    });
    return () => subscription.unsubscribe();
  }, [form]);

  // Clear on beforeunload (per FR-002).
  useEffect(() => wizardStorage.clearOnBeforeUnload(), []);

  const goNext = () => setStepIndex((idx) => Math.min(STEPS.length - 1, idx + 1));
  const goBack = () => setStepIndex((idx) => Math.max(0, idx - 1));
  const goTo = (idx: number) => setStepIndex(idx);

  const handleCancel = () => {
    wizardStorage.clear();
    router.push("/namespaces" as never);
  };

  const handleRegistered = () => {
    wizardStorage.clear();
  };

  const stepProps = {
    goNext,
    goBack,
    setValidationRun,
    validationRun,
  };

  return (
    <FormProvider {...form}>
      <div className="flex flex-col gap-6" data-testid="namespace-onboarding-wizard">
        <header className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-foreground-default">Onboard a namespace</h1>
            <p className="text-sm text-foreground-muted">
              Capture identity, metadata, ownership, validation, and register — five steps, no
              partial persistence.
            </p>
          </div>
          <Button
            type="button"
            intent="ghost"
            onClick={handleCancel}
            data-testid="wizard-cancel"
          >
            Cancel
          </Button>
        </header>
        <WizardStepper
          steps={STEPS}
          currentIndex={stepIndex}
          onStepClick={goTo}
        />
        <Card>
          <CardContent className="p-6">
            {stepIndex === 0 && <Step1Identification {...stepProps} />}
            {stepIndex === 1 && <Step2Metadata {...stepProps} />}
            {stepIndex === 2 && <Step3Ownership {...stepProps} />}
            {stepIndex === 3 && <Step4Validation {...stepProps} />}
            {stepIndex === 4 && (
              <Step5Review {...stepProps} onRegistered={handleRegistered} />
            )}
          </CardContent>
        </Card>
      </div>
    </FormProvider>
  );
}
