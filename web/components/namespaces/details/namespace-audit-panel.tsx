/**
 * Spec 008 / T113 / US2. Audit panel — renders the most recent audit events
 * for the namespace. Each row carries actor, timestamp, event type, change
 * summary, and (for lifecycle transitions) the reason supplied by the
 * administrator.
 *
 * RSC-safe (pure presentational).
 */

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

interface AuditEventShape {
  readonly id: string;
  readonly eventType: string;
  readonly timestamp: string;
  readonly actor?:
    | { readonly displayName?: string | null | undefined; readonly principalId?: string | null | undefined }
    | null
    | undefined;
  readonly changeSummary?: string | null | undefined;
  readonly lifecycleReason?: string | null | undefined;
  readonly fieldChanges?:
    | ReadonlyArray<{
        readonly field: string;
        readonly before?: unknown;
        readonly after?: unknown;
      }>
    | null
    | undefined;
}

interface NamespaceAuditPanelProps {
  readonly events: ReadonlyArray<AuditEventShape>;
}

export function NamespaceAuditPanel({ events }: NamespaceAuditPanelProps) {
  return (
    <Card data-testid="namespace-audit-panel">
      <CardHeader>
        <CardTitle>Recent audit</CardTitle>
      </CardHeader>
      <CardContent>
        {events.length === 0 ? (
          <p className="text-sm text-foreground-muted">No audit events recorded yet.</p>
        ) : (
          <ul className="flex flex-col gap-3">
            {events.map((event) => (
              <li
                key={event.id}
                className="rounded border border-border-default px-3 py-2"
                data-event-id={event.id}
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <Badge intent="outline">{event.eventType}</Badge>
                    <span className="text-sm font-medium text-foreground-default">
                      {event.actor?.displayName ?? event.actor?.principalId ?? "(unknown)"}
                    </span>
                  </div>
                  <time className="text-xs text-foreground-muted">
                    {new Date(event.timestamp).toLocaleString()}
                  </time>
                </div>
                {event.changeSummary ? (
                  <p className="mt-1 text-sm text-foreground-default">{event.changeSummary}</p>
                ) : null}
                {event.lifecycleReason ? (
                  <p className="mt-1 text-xs text-foreground-muted">
                    Reason: <span className="text-foreground-default">{event.lifecycleReason}</span>
                  </p>
                ) : null}
                {event.fieldChanges && event.fieldChanges.length > 0 ? (
                  <ul className="mt-2 flex flex-col gap-1 text-xs text-foreground-muted">
                    {event.fieldChanges.map((change) => (
                      <li key={change.field} className="font-mono">
                        <span className="font-semibold">{change.field}</span>: changed
                      </li>
                    ))}
                  </ul>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
