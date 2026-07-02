/**
 * Spec 009 / T089 / US3.
 *
 * Server component that renders the full DiscoveryRun document for an
 * operator investigating a single run. Distinct cards group the run's
 * identity (id, namespace, trigger), timing (started/completed/duration),
 * classification counts, entity-type counts, failure detail (only on
 * `Failed` runs), and the coalescing audit array.
 *
 * The failure card surfaces the operator-safe message returned by the
 * worker's `FailureMessageSanitizer` (no PII; ARM IDs and entity names
 * already redacted upstream).
 */

import { AlertTriangle, Clock, Hash, Layers, User } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import type {
  DiscoveryFailureCategory,
  DiscoveryPhase,
  DiscoveryRun,
  DiscoveryRunStatus,
} from "@/lib/discovery/schemas";

interface DiscoveryRunDetailProps {
  readonly run: DiscoveryRun;
}

const STATUS_INTENT: Record<DiscoveryRunStatus, "info" | "success" | "warning" | "error"> = {
  Queued: "info",
  InProgress: "info",
  Succeeded: "success",
  Failed: "error",
};

const FAILURE_CATEGORY_LABEL: Record<DiscoveryFailureCategory, string> = {
  Authn: "Authentication failure",
  Authz: "Authorization denied",
  NotFound: "Resource not found",
  Throttled: "Throttled (ARM 429)",
  Transport: "Transport / network error",
  Internal: "Internal worker error",
  WorkerLost: "Worker lost (stale lock recovered)",
  Unknown: "Unclassified error",
};

const FAILURE_PHASE_LABEL: Record<DiscoveryPhase, string> = {
  LockAcquire: "Acquire run lock",
  Enqueue: "Enqueue discovery request",
  FetchQueues: "Fetch queues",
  FetchTopics: "Fetch topics",
  FetchSubscriptions: "Fetch subscriptions",
  FetchRules: "Fetch rules",
  Persist: "Persist results",
  ResultWrite: "Write run result",
};

export function DiscoveryRunDetail({ run }: DiscoveryRunDetailProps) {
  return (
    <div className="flex flex-col gap-4" data-testid="discovery-run-detail">
      <Card>
        <CardHeader>
          <CardTitle>Run identity</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          <Row icon={<Hash aria-hidden="true" />} label="Run id">
            <span className="font-mono text-xs" data-testid="run-id">
              {run.id}
            </span>
          </Row>
          <Row icon={<Hash aria-hidden="true" />} label="Namespace">
            <span className="font-mono text-xs">{run.namespaceId}</span>
          </Row>
          <Row icon={<User aria-hidden="true" />} label="Requested by">
            <span className="font-mono text-xs">{run.requestedBy}</span>
          </Row>
          <Row icon={<Hash aria-hidden="true" />} label="Trigger">
            <span className="text-sm">{run.trigger}</span>
          </Row>
          <Row icon={<Hash aria-hidden="true" />} label="Status">
            <Badge
              intent={STATUS_INTENT[run.status]}
              aria-label={`Status: ${run.status}`}
              data-testid="run-status"
            >
              {run.status}
            </Badge>
          </Row>
          {run.correlationId ? (
            <Row icon={<Hash aria-hidden="true" />} label="Correlation id">
              <span className="font-mono text-[11px] text-foreground-muted">{run.correlationId}</span>
            </Row>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Timing</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          <Row icon={<Clock aria-hidden="true" />} label="Started">
            <time dateTime={run.startedUtc}>{new Date(run.startedUtc).toLocaleString()}</time>
          </Row>
          <Row icon={<Clock aria-hidden="true" />} label="Completed">
            {run.completedUtc ? (
              <time dateTime={run.completedUtc}>
                {new Date(run.completedUtc).toLocaleString()}
              </time>
            ) : (
              <span className="text-foreground-muted">— (still running)</span>
            )}
          </Row>
          <Row icon={<Clock aria-hidden="true" />} label="Duration">
            <span className="font-mono text-xs" data-testid="run-duration">
              {formatDuration(run.durationMs)}
            </span>
          </Row>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Classification</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Stat label="New" value={run.newCount ?? 0} testId="run-new-count" />
          <Stat label="Updated" value={run.updatedCount ?? 0} testId="run-updated-count" />
          <Stat label="Unchanged" value={run.unchangedCount ?? 0} testId="run-unchanged-count" />
          <Stat label="Missing" value={run.missingCount ?? 0} testId="run-missing-count" />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Entity counts</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Stat
            label="Queues"
            value={run.queueCount ?? 0}
            icon={<Layers className="size-4" aria-hidden="true" />}
            testId="run-queue-count"
          />
          <Stat
            label="Topics"
            value={run.topicCount ?? 0}
            icon={<Layers className="size-4" aria-hidden="true" />}
            testId="run-topic-count"
          />
          <Stat
            label="Subscriptions"
            value={run.subscriptionCount ?? 0}
            icon={<Hash className="size-4" aria-hidden="true" />}
            testId="run-subscription-count"
          />
          <Stat
            label="Rules"
            value={run.ruleCount ?? 0}
            icon={<Hash className="size-4" aria-hidden="true" />}
            testId="run-rule-count"
          />
        </CardContent>
      </Card>

      {run.status === "Failed" && run.failure ? (
        <Card data-testid="run-failure-card" className="border-error-foreground/40">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <AlertTriangle className="size-4 text-error-foreground" aria-hidden="true" />
              Failure
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <Row icon={<AlertTriangle aria-hidden="true" />} label="Category">
              <span data-testid="failure-category" className="text-sm">
                {FAILURE_CATEGORY_LABEL[run.failure.category]}
              </span>
            </Row>
            <Row icon={<AlertTriangle aria-hidden="true" />} label="Phase">
              <span data-testid="failure-phase" className="text-sm">
                {FAILURE_PHASE_LABEL[run.failure.occurredAtPhase]}
              </span>
            </Row>
            {run.failure.retriesExhausted != null ? (
              <Row icon={<AlertTriangle aria-hidden="true" />} label="Retries exhausted">
                <span data-testid="failure-retries" className="font-mono text-xs">
                  {run.failure.retriesExhausted}
                </span>
              </Row>
            ) : null}
            <Separator />
            <div className="flex flex-col gap-1">
              <span className="text-xs uppercase tracking-wide text-foreground-muted">Message</span>
              <pre
                data-testid="failure-message"
                className="overflow-auto whitespace-pre-wrap rounded-sm border border-border-default bg-surface-muted p-3 font-mono text-xs"
              >
                {run.failure.message || "(no message)"}
              </pre>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {run.coalescedRequests && run.coalescedRequests.length > 0 ? (
        <Card data-testid="run-coalesced-card">
          <CardHeader>
            <CardTitle>Coalesced requests</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col gap-2">
              {run.coalescedRequests.map((req, index) => (
                <li
                  key={`coalesced-${index}`}
                  className="flex items-center gap-3 text-sm"
                >
                  <Clock className="size-3.5 text-foreground-muted" aria-hidden="true" />
                  <time dateTime={req.requestedUtc ?? undefined}>
                    {req.requestedUtc ? new Date(req.requestedUtc).toLocaleString() : "—"}
                  </time>
                  <span className="font-mono text-xs text-foreground-muted">
                    {req.requestedBy ?? "—"}
                  </span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}

function Row({
  icon,
  label,
  children,
}: {
  icon: React.ReactNode;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-center gap-2 text-sm">
      <span className="text-foreground-muted">{icon}</span>
      <span className="text-foreground-muted">{label}:</span>
      <span className="font-medium text-foreground-default">{children}</span>
    </div>
  );
}

function Stat({
  label,
  value,
  icon,
  testId,
}: {
  label: string;
  value: number;
  icon?: React.ReactNode;
  testId?: string;
}) {
  return (
    <div className="flex items-center gap-2" data-testid={testId}>
      {icon ? <span className="text-foreground-muted">{icon}</span> : null}
      <span className="text-foreground-muted">{label}:</span>
      <span className="font-medium text-foreground-default">{value}</span>
    </div>
  );
}

function formatDuration(durationMs: number | null | undefined): string {
  if (durationMs == null) return "—";
  if (durationMs < 1000) return `${durationMs} ms`;
  if (durationMs < 60_000) return `${(durationMs / 1000).toFixed(1)} s`;
  const minutes = Math.floor(durationMs / 60_000);
  const seconds = Math.floor((durationMs % 60_000) / 1000);
  return `${minutes}m ${seconds}s`;
}
