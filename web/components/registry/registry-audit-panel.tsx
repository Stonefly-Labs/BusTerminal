"use client";

/**
 * Spec 006 / T124 [US3] / FR-033. Audit panel for the entity detail page.
 *
 * Queries `GET /api/registry/{id}/audit` (newest-first; default limit 50).
 * Each event renders the actor, UTC timestamp, and change summary. For
 * `Updated` and `StatusChanged` events the change-summary opens a popover
 * showing the field-level diff (before / after) per quickstart §7 step 4.
 *
 * Append-only at the API layer (FR-034) — the UI exposes no edit/delete
 * actions on individual events.
 */

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";

import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/design-system/cn";
import { listAuditForEntity, RegistryApiError } from "@/lib/registry/api";
import { registryQueryKeys } from "@/lib/registry/query-keys";
import type { AuditEvent, AuditFieldChange } from "@/lib/registry/types";

interface RegistryAuditPanelProps {
  readonly entityId: string;
  readonly limit?: number;
  readonly className?: string;
}

const DIFFABLE_EVENT_TYPES = new Set(["Updated", "StatusChanged"]);

export function RegistryAuditPanel({
  entityId,
  limit = 50,
  className,
}: RegistryAuditPanelProps) {
  const auditQuery = useQuery({
    queryKey: registryQueryKeys.audit.forEntity(entityId, limit),
    queryFn: () => listAuditForEntity(entityId, limit),
    enabled: !!entityId,
  });

  if (auditQuery.isPending) {
    return (
      <p
        data-testid="registry-audit-panel"
        data-variant="loading"
        className={cn("text-sm text-foreground-muted", className)}
      >
        Loading history…
      </p>
    );
  }

  if (auditQuery.isError) {
    const message =
      auditQuery.error instanceof RegistryApiError || auditQuery.error instanceof Error
        ? auditQuery.error.message
        : "Could not load audit events.";
    return (
      <p
        data-testid="registry-audit-panel"
        data-variant="error"
        role="alert"
        className={cn("text-sm text-foreground-default", className)}
      >
        {message}
      </p>
    );
  }

  const events = auditQuery.data ?? [];
  if (events.length === 0) {
    return (
      <p
        data-testid="registry-audit-panel"
        data-variant="empty"
        className={cn("text-sm text-foreground-muted", className)}
      >
        No audit events for this entity yet.
      </p>
    );
  }

  return (
    <ol
      data-testid="registry-audit-panel"
      data-variant="loaded"
      className={cn("flex flex-col gap-3", className)}
    >
      {events.map((event) => (
        <AuditEventRow key={event.id} event={event} />
      ))}
    </ol>
  );
}

function AuditEventRow({ event }: { event: AuditEvent }) {
  const hasDiff =
    DIFFABLE_EVENT_TYPES.has(event.eventType) &&
    !!event.fieldChanges &&
    event.fieldChanges.length > 0;

  const formattedTimestamp = useMemo(() => formatTimestamp(event.timestamp), [event.timestamp]);
  const [popoverOpen, setPopoverOpen] = useState(false);

  const eventTypeLabel = (
    <span
      data-event-type={event.eventType}
      className="rounded-full border border-border-default bg-surface-muted px-2 py-0.5 text-[11px] font-medium uppercase tracking-wide text-foreground-muted"
    >
      {event.eventType}
    </span>
  );

  const summary = (
    <p className="text-sm text-foreground-default">{event.changeSummary}</p>
  );

  return (
    <li
      data-testid="registry-audit-event"
      data-event-id={event.id}
      data-event-type={event.eventType}
      data-has-diff={hasDiff ? "true" : "false"}
      className="rounded-md border border-border-default bg-surface-muted p-3"
    >
      <div className="flex flex-wrap items-center gap-2 text-xs text-foreground-muted">
        {eventTypeLabel}
        <span>
          {event.actor.displayName || event.actor.principalId || "Unknown actor"}
        </span>
        <span aria-hidden="true">•</span>
        <time dateTime={event.timestamp} className="font-mono">
          {formattedTimestamp}
        </time>
        {event.wasForceOverwrite ? (
          <span className="rounded-full border border-border-default px-2 py-0.5 text-[10px] uppercase tracking-wide text-foreground-default">
            Force overwrite
          </span>
        ) : null}
      </div>
      <div className="mt-1">
        {hasDiff ? (
          <Popover open={popoverOpen} onOpenChange={setPopoverOpen}>
            <PopoverTrigger asChild>
              <button
                type="button"
                data-testid="registry-audit-event-trigger"
                className="text-start text-sm text-foreground-default underline-offset-2 hover:underline focus-visible:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)"
              >
                {event.changeSummary}
              </button>
            </PopoverTrigger>
            <PopoverContent
              align="start"
              data-testid="registry-audit-field-diff"
              className="w-96"
            >
              <FieldDiffList changes={event.fieldChanges ?? []} />
            </PopoverContent>
          </Popover>
        ) : (
          summary
        )}
      </div>
    </li>
  );
}

function FieldDiffList({ changes }: { changes: readonly AuditFieldChange[] }) {
  return (
    <dl className="flex flex-col gap-2 text-xs">
      <p className="text-[11px] font-semibold uppercase tracking-wide text-foreground-subtle">
        Field changes
      </p>
      {changes.map((change, idx) => (
        <div
          key={`${change.field}-${idx}`}
          data-testid="registry-audit-field-change"
          data-field={change.field}
          className="rounded border border-border-default bg-surface-canvas p-2"
        >
          <dt className="font-mono text-[11px] text-foreground-muted">{change.field}</dt>
          <dd className="mt-1 grid grid-cols-[auto_1fr] gap-x-2 gap-y-0.5">
            <span className="text-foreground-subtle">before</span>
            <code className="break-all font-mono text-foreground-default">
              {renderValue(change.before)}
            </code>
            <span className="text-foreground-subtle">after</span>
            <code className="break-all font-mono text-foreground-default">
              {renderValue(change.after)}
            </code>
          </dd>
        </div>
      ))}
    </dl>
  );
}

function renderValue(value: unknown): string {
  if (value === null || value === undefined) return "—";
  if (typeof value === "string") return value === "" ? "(empty)" : value;
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

function formatTimestamp(iso: string): string {
  // Render in a stable UTC form so the panel matches App Insights timestamps
  // (data-model §5). Avoid locale-specific formatting which would diverge
  // from the audit document on disk.
  try {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return iso;
    return date.toISOString().replace("T", " ").replace(/\.\d{3}Z$/, "Z");
  } catch {
    return iso;
  }
}
