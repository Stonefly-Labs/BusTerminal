"use client";

/**
 * Spec 008 / T089 — Step 4: Validation.
 *
 * Triggers `POST /api/namespaces/_validate` with the pre-allocated
 * `proposedNamespaceId` (research §18). Renders per-check progress as the
 * run resolves, then an aggregate status badge + remediation hints for
 * failures (especially `ReaderRoleMissing`, which re-embeds the step-1
 * grant guidance).
 *
 * Re-runs validation only on validation-relevant field change per FR-003.
 */

import { useEffect, useMemo, useState } from "react";
import { useFormContext } from "react-hook-form";
import { useMutation } from "@tanstack/react-query";
import { AlertTriangle, CheckCircle2, Loader2, XCircle } from "lucide-react";

import { useAcquireToken } from "@/hooks/use-acquire-token";
import * as NamespacesApi from "@/lib/namespaces/api";
import type {
  ValidationCheckResult,
  ValidationFailureCategory,
  ValidationRun,
} from "@/lib/namespaces/schemas";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { GrantReaderGuidance } from "@/components/namespaces/wizard/grant-reader-guidance";
import type { StepProps, WizardFormValues } from "./types";

export function Step4Validation({ goNext, goBack, setValidationRun, validationRun }: StepProps) {
  const { getValues, setValue, watch } = useFormContext<WizardFormValues>();
  const getToken = useAcquireToken();
  const armId = watch("azureResourceId");

  // Allocate the namespaceId once when we land on step 4. Re-uses it across
  // retries so all wizard ValidationRuns partition-align with the eventual
  // OnboardedNamespace (research §18).
  useEffect(() => {
    if (!getValues("namespaceId")) {
      setValue("namespaceId", crypto.randomUUID());
    }
  }, [getValues, setValue]);

  const runMutation = useMutation({
    mutationFn: async () => {
      const proposedNamespaceId = getValues("namespaceId");
      const token = await getToken();
      const run = await NamespacesApi.runPreOnboardingValidation(
        {
          azureResourceId: armId,
          proposedNamespaceId,
        },
        token ? { accessToken: token } : {},
      );
      return run;
    },
    onSuccess: (run) => {
      setValue("validationRunId", run.id);
      setValidationRun(run);
    },
  });

  // Surface a stale-ARM banner if the user navigated back to step 1 and
  // mutated the ARM id after running validation.
  const armIdAtRun = validationRun?.azureResourceIdAtRun;
  const armIdIsStale = useMemo(
    () => validationRun !== null && armIdAtRun !== armId,
    [armId, armIdAtRun, validationRun],
  );

  const canAdvance = validationRun
    && !armIdIsStale
    && (validationRun.aggregateStatus === "Healthy"
      || validationRun.aggregateStatus === "Degraded");

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="text-base font-semibold text-foreground-default">Run validation</h2>
          <p className="text-sm text-foreground-muted">
            Five checks verify the namespace exists, the workload identity has Reader, and the
            management endpoint is reachable. Re-run after fixing any failures.
          </p>
        </div>
        <Button
          type="button"
          onClick={() => runMutation.mutate()}
          disabled={runMutation.isPending}
          data-testid="wizard-step-4-run"
        >
          {runMutation.isPending ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />
              Running validation
            </>
          ) : validationRun ? (
            "Re-run validation"
          ) : (
            "Run validation"
          )}
        </Button>
      </div>

      {armIdIsStale ? (
        <Card>
          <CardContent className="flex items-center gap-2 p-4 text-sm text-warning-foreground">
            <AlertTriangle className="h-4 w-4" aria-hidden="true" />
            ARM id changed since the last run — re-run validation to continue.
          </CardContent>
        </Card>
      ) : null}

      {runMutation.isError ? (
        <Card>
          <CardContent className="flex items-center gap-2 p-4 text-sm text-error-foreground">
            <XCircle className="h-4 w-4" aria-hidden="true" />
            Validation request failed. Try again.
          </CardContent>
        </Card>
      ) : null}

      {validationRun ? (
        <ValidationResultPanel run={validationRun} armResourceId={armId} />
      ) : null}

      <div className="flex justify-between">
        <Button type="button" intent="outline" onClick={goBack}>
          Back
        </Button>
        <Button
          type="button"
          onClick={goNext}
          disabled={!canAdvance}
          data-testid="wizard-step-4-next"
        >
          Continue to review
        </Button>
      </div>
    </div>
  );
}

function ValidationResultPanel({
  run,
  armResourceId,
}: {
  readonly run: ValidationRun;
  readonly armResourceId: string;
}) {
  const statusLabel = run.aggregateStatus;
  const intent: "success" | "warning" | "error" =
    statusLabel === "Healthy" ? "success"
    : statusLabel === "Degraded" ? "warning"
    : "error";

  const readerMissing = run.checkResults.some(
    (r) => r.name === "RequiredPermissions" && r.outcome === "Fail",
  );

  return (
    <Card data-testid="wizard-step-4-result">
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle className="text-sm">Validation result</CardTitle>
        <Badge intent={intent} data-testid="wizard-step-4-aggregate-status">
          {statusLabel}
        </Badge>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <ul className="flex flex-col gap-2">
          {run.checkResults.map((check) => (
            <CheckRow key={check.name} check={check} />
          ))}
        </ul>
        {readerMissing ? (
          <div className="mt-2">
            <p className="mb-2 text-xs font-medium text-foreground-default">
              Grant Reader and re-run validation:
            </p>
            <GrantReaderGuidance azureResourceId={armResourceId} />
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function CheckRow({ check }: { readonly check: ValidationCheckResult }) {
  const icon =
    check.outcome === "Pass" ? (
      <CheckCircle2 className="h-4 w-4 text-success-foreground" aria-hidden="true" />
    ) : check.outcome === "Skipped" ? (
      <AlertTriangle className="h-4 w-4 text-warning-foreground" aria-hidden="true" />
    ) : (
      <XCircle className="h-4 w-4 text-error-foreground" aria-hidden="true" />
    );
  return (
    <li className="flex items-start gap-3 rounded-md border border-border-muted p-3">
      <div className="mt-0.5">{icon}</div>
      <div className="flex flex-1 flex-col gap-0.5">
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium text-foreground-default">{check.name}</span>
          <span className="text-xs text-foreground-muted">{check.durationMs}ms</span>
        </div>
        <p className="text-xs text-foreground-muted">
          {check.reason}
          {check.reasonCategory !== "Ok" ? (
            <span className="ml-1">
              (<code className="font-mono">{remediationCategory(check.reasonCategory)}</code>)
            </span>
          ) : null}
        </p>
      </div>
    </li>
  );
}

function remediationCategory(c: ValidationFailureCategory): string {
  return c;
}
