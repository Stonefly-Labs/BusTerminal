"use client";

/**
 * Spec 008 / T088 — Step 3: Ownership.
 *
 * Required Primary Owner + optional Secondary Owners, Technical Stewards,
 * Support Contacts (zero or more, picker-driven).
 */

import { useFormContext } from "react-hook-form";

import { EntraPrincipalPicker, type PickedPrincipal } from "@/components/namespaces/shared/entra-principal-picker";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { StepProps, WizardFormValues } from "./types";

export function Step3Ownership({ goNext, goBack }: StepProps) {
  const { watch, setValue } = useFormContext<WizardFormValues>();
  const primaryOwner = watch("primaryOwner");
  const secondaryOwners = watch("secondaryOwners");
  const technicalStewards = watch("technicalStewards");
  const supportContacts = watch("supportContacts");

  const canAdvance = primaryOwner !== null;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h2 className="text-base font-semibold text-foreground-default">Ownership</h2>
        <p className="text-sm text-foreground-muted">
          Assign Entra users or groups to each ownership role. Primary Owner is required.
        </p>
      </div>
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Primary owner *</CardTitle>
        </CardHeader>
        <CardContent>
          <EntraPrincipalPicker
            label="Primary owner"
            required
            value={primaryOwner}
            onChange={(v) => setValue("primaryOwner", v, { shouldDirty: true })}
            testIdPrefix="wizard-step-3-primary-owner"
          />
        </CardContent>
      </Card>
      <RoleList
        title="Secondary owners"
        items={secondaryOwners}
        onChange={(items) => setValue("secondaryOwners", items, { shouldDirty: true })}
        testIdPrefix="wizard-step-3-secondary-owners"
      />
      <RoleList
        title="Technical stewards"
        items={technicalStewards}
        onChange={(items) => setValue("technicalStewards", items, { shouldDirty: true })}
        testIdPrefix="wizard-step-3-technical-stewards"
      />
      <RoleList
        title="Support contacts"
        items={supportContacts}
        onChange={(items) => setValue("supportContacts", items, { shouldDirty: true })}
        testIdPrefix="wizard-step-3-support-contacts"
      />
      <div className="flex justify-between">
        <Button type="button" intent="outline" onClick={goBack}>
          Back
        </Button>
        <Button
          type="button"
          onClick={goNext}
          disabled={!canAdvance}
          data-testid="wizard-step-3-next"
        >
          Continue
        </Button>
      </div>
    </div>
  );
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
          onClick={() => onChange([...items, ...placeholder()])}
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

function placeholder(): [PickedPrincipal] {
  return [{ objectId: "", principalType: "User", displayName: "" }];
}
