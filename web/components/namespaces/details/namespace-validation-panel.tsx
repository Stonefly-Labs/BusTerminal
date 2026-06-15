"use client";

/**
 * Spec 008 / T112 / US2. Validation panel — renders the latest ValidationRun:
 * aggregate status badge, per-check breakdown, drift warning, and the
 * "Re-run validation" button. The re-run wiring lands in T140 (Phase 5); this
 * panel currently surfaces the button as disabled with a tooltip if the
 * caller hasn't passed `onReRun`.
 */

import { CheckCircle2, CircleAlert, CircleSlash, RotateCw } from "lucide-react";

import { useHasRole } from "@/hooks/use-has-role";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/design-system/cn";
import type { ValidationRun } from "@/lib/namespaces/types";

import { ValidationStatusBadge } from "../inventory/validation-status-badge";

interface NamespaceValidationPanelProps {
  readonly run: ValidationRun | null | undefined;
  readonly onReRun?: () => void | undefined;
  readonly isReRunning?: boolean | undefined;
}

export function NamespaceValidationPanel({ run, onReRun, isReRunning }: NamespaceValidationPanelProps) {
  const isAdmin = useHasRole("BusTerminal.NamespaceAdministrator");

  if (!run) {
    return (
      <Card data-testid="namespace-validation-panel">
        <CardHeader>
          <CardTitle>Validation</CardTitle>
        </CardHeader>
        <CardContent className="text-sm text-foreground-muted">
          No validation run has been recorded for this namespace yet.
        </CardContent>
      </Card>
    );
  }

  return (
    <Card data-testid="namespace-validation-panel">
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <CardTitle>Validation</CardTitle>
        <div className="flex items-center gap-2">
          <ValidationStatusBadge status={run.aggregateStatus} />
          {isAdmin ? (
            <Button
              type="button"
              intent="secondary"
              size="sm"
              onClick={onReRun}
              disabled={!onReRun || isReRunning}
            >
              <RotateCw className={cn("me-1 size-4", isReRunning && "animate-spin")} aria-hidden="true" />
              Re-run
            </Button>
          ) : null}
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="grid grid-cols-1 gap-2 text-xs text-foreground-muted sm:grid-cols-3">
          <div>
            Executed:{" "}
            <span className="text-foreground-default">{new Date(run.executedAtUtc).toLocaleString()}</span>
          </div>
          <div>
            By:{" "}
            <span className="text-foreground-default">{run.executedByDisplayNameSnapshot ?? "—"}</span>
          </div>
          <div>
            Duration:{" "}
            <span className="text-foreground-default">{run.totalDurationMs} ms</span>
          </div>
        </div>

        <ul className="flex flex-col gap-2">
          {run.checkResults.map((check) => (
            <li
              key={check.name}
              className="flex items-start gap-3 rounded border border-border-default px-3 py-2"
            >
              <CheckIcon outcome={check.outcome} />
              <div className="flex-1">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium text-foreground-default">{check.name}</span>
                  <Badge intent={check.outcome === "Pass" ? "success" : check.outcome === "Skipped" ? "outline" : "error"}>
                    {check.outcome}
                  </Badge>
                  {check.reasonCategory !== "Ok" ? (
                    <Badge intent="outline">{check.reasonCategory}</Badge>
                  ) : null}
                </div>
                <p className="mt-1 text-xs text-foreground-muted">{check.reason}</p>
              </div>
              <span className="text-xs text-foreground-muted">{check.durationMs} ms</span>
            </li>
          ))}
        </ul>

        {run.driftDetected ? (
          <div className="rounded border border-warning-foreground/40 bg-warning-surface/30 p-3">
            <div className="flex items-center gap-2 text-sm font-medium text-warning-foreground">
              <CircleAlert className="size-4" aria-hidden="true" />
              Drift detected
            </div>
            <ul className="mt-2 flex flex-col gap-1 text-xs text-foreground-default">
              {run.driftFields.map((field) => (
                <li key={field.field} className="font-mono">
                  <span className="font-semibold">{field.field}:</span> persisted{" "}
                  <code>{field.persistedValue}</code> → observed <code>{field.observedValue}</code>
                </li>
              ))}
            </ul>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function CheckIcon({ outcome }: { outcome: "Pass" | "Fail" | "Skipped" }) {
  if (outcome === "Pass") return <CheckCircle2 className="mt-0.5 size-4 text-success-foreground" aria-hidden="true" />;
  if (outcome === "Skipped") return <CircleSlash className="mt-0.5 size-4 text-foreground-muted" aria-hidden="true" />;
  return <CircleAlert className="mt-0.5 size-4 text-error-foreground" aria-hidden="true" />;
}
