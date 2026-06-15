"use client";

/**
 * Spec 008 / T090 — Step 5: Review & Register.
 *
 * Read-only summary of every wizard input + the latest validation run.
 * Register button is enabled iff the latest run is Healthy or Degraded
 * (FR-023a). On 201 we route to the new namespace's details page.
 */

import { useFormContext } from "react-hook-form";
import { useRouter } from "next/navigation";
import { useMutation } from "@tanstack/react-query";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import * as NamespacesApi from "@/lib/namespaces/api";
import type { OwnershipBlock } from "@/lib/namespaces/schemas";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { PickedPrincipal } from "@/components/namespaces/shared/entra-principal-picker";
import type { StepProps, WizardFormValues } from "./types";

export interface Step5ReviewProps extends StepProps {
  readonly onRegistered: (id: string) => void;
}

export function Step5Review({ goBack, validationRun, onRegistered }: Step5ReviewProps) {
  const { getValues } = useFormContext<WizardFormValues>();
  const getToken = useAcquireToken();
  const router = useRouter();

  const canRegister = validationRun !== null
    && (validationRun.aggregateStatus === "Healthy"
      || validationRun.aggregateStatus === "Degraded");

  const registerMutation = useMutation({
    mutationFn: async () => {
      if (!validationRun) throw new Error("No validation run");
      const values = getValues();
      const ownership = composeOwnership(values, getValues("namespaceId"));
      const token = await getToken();
      const now = new Date().toISOString();
      const ns = await NamespacesApi.register(
        {
          id: values.namespaceId,
          azureResourceId: values.azureResourceId,
          displayName: values.displayName,
          environment: values.environment,
          description: values.description || null,
          businessUnit: values.businessUnit || null,
          productOrApplication: values.productOrApplication || null,
          costCenter: values.costCenter || null,
          notes: values.notes || null,
          tags: [],
          ownership,
          validationRunId: values.validationRunId,
        },
        token ? { accessToken: token } : {},
      );
      // Surface the assignedAtUtc on payload — backend echoes its own value;
      // we don't reuse the local timestamp after this point.
      void now;
      return ns;
    },
    onSuccess: (ns) => {
      onRegistered(ns.id);
      router.push(`/namespaces/${ns.id}` as never);
    },
  });

  const values = getValues();

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h2 className="text-base font-semibold text-foreground-default">Review &amp; register</h2>
        <p className="text-sm text-foreground-muted">
          Confirm the captured details below, then register. After registration the namespace appears
          in inventory immediately.
        </p>
      </div>
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Identification</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-1 gap-2 text-sm md:grid-cols-2">
          <SummaryRow label="ARM resource id" value={values.azureResourceId} mono />
          <SummaryRow label="Display name" value={values.displayName} />
          <SummaryRow label="Environment" value={values.environment} />
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Business metadata</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-1 gap-2 text-sm md:grid-cols-2">
          <SummaryRow label="Business unit" value={values.businessUnit} />
          <SummaryRow label="Product / application" value={values.productOrApplication} />
          <SummaryRow label="Cost center" value={values.costCenter} />
          <SummaryRow label="Description" value={values.description} />
          <SummaryRow label="Notes" value={values.notes} />
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Ownership</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2 text-sm">
          <SummaryRow label="Primary owner" value={values.primaryOwner?.displayName ?? ""} />
          <SummaryRow label="Secondary owners" value={joinPrincipals(values.secondaryOwners)} />
          <SummaryRow label="Technical stewards" value={joinPrincipals(values.technicalStewards)} />
          <SummaryRow label="Support contacts" value={joinPrincipals(values.supportContacts)} />
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="text-sm">Latest validation</CardTitle>
          {validationRun ? (
            <Badge
              intent={
                validationRun.aggregateStatus === "Healthy" ? "success"
                : validationRun.aggregateStatus === "Degraded" ? "warning"
                : "error"
              }
            >
              {validationRun.aggregateStatus}
            </Badge>
          ) : null}
        </CardHeader>
        <CardContent>
          {validationRun ? (
            <p className="text-xs text-foreground-muted">
              Run at {new Date(validationRun.executedAtUtc).toLocaleString()} —{" "}
              {validationRun.checkResults.filter((c) => c.outcome === "Pass").length}/5 checks Pass
            </p>
          ) : (
            <p className="text-xs text-foreground-muted">Run validation in step 4 first.</p>
          )}
        </CardContent>
      </Card>
      {registerMutation.isError ? (
        <p className="text-sm text-error-foreground" role="alert">
          Registration failed. Re-run validation if needed and try again.
        </p>
      ) : null}
      <div className="flex justify-between">
        <Button type="button" intent="outline" onClick={goBack}>
          Back
        </Button>
        <Button
          type="button"
          onClick={() => registerMutation.mutate()}
          disabled={!canRegister || registerMutation.isPending}
          data-testid="wizard-step-5-register"
        >
          {registerMutation.isPending ? "Registering…" : "Register namespace"}
        </Button>
      </div>
    </div>
  );
}

function SummaryRow({ label, value, mono }: { readonly label: string; readonly value: string; readonly mono?: boolean }) {
  return (
    <div className="flex flex-col">
      <span className="text-xs uppercase tracking-wide text-foreground-muted">{label}</span>
      <span className={mono ? "font-mono text-xs" : "text-sm text-foreground-default"}>
        {value || <em className="text-foreground-muted">Not provided</em>}
      </span>
    </div>
  );
}

function joinPrincipals(items: ReadonlyArray<PickedPrincipal>): string {
  const named = items.filter((p) => p.displayName).map((p) => p.displayName);
  return named.join(", ");
}

function composeOwnership(values: WizardFormValues, assignedBy: string): OwnershipBlock {
  const now = new Date().toISOString();
  const assigner = assignedBy && assignedBy.length === 36 ? assignedBy : "00000000-0000-0000-0000-000000000000";
  if (!values.primaryOwner) {
    throw new Error("primaryOwner is required");
  }
  return {
    primaryOwner: {
      role: "PrimaryOwner",
      principalType: values.primaryOwner.principalType,
      objectId: values.primaryOwner.objectId,
      displayNameSnapshot: values.primaryOwner.displayName,
      assignedAtUtc: now,
      assignedBy: assigner,
    },
    secondaryOwners: values.secondaryOwners
      .filter((p) => p.objectId)
      .map((p) => ({
        role: "SecondaryOwner" as const,
        principalType: p.principalType,
        objectId: p.objectId,
        displayNameSnapshot: p.displayName,
        assignedAtUtc: now,
        assignedBy: assigner,
      })),
    technicalStewards: values.technicalStewards
      .filter((p) => p.objectId)
      .map((p) => ({
        role: "TechnicalSteward" as const,
        principalType: p.principalType,
        objectId: p.objectId,
        displayNameSnapshot: p.displayName,
        assignedAtUtc: now,
        assignedBy: assigner,
      })),
    supportContacts: values.supportContacts
      .filter((p) => p.objectId)
      .map((p) => ({
        role: "SupportContact" as const,
        principalType: p.principalType,
        objectId: p.objectId,
        displayNameSnapshot: p.displayName,
        assignedAtUtc: now,
        assignedBy: assigner,
      })),
  };
}
