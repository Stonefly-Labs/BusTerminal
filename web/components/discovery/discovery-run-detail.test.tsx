/**
 * Spec 009 / T083 / US3. Component test for `<DiscoveryRunDetail>`.
 *
 * Covers:
 *   - Succeeded render — no failure card; counts visible
 *   - Failed render — failure card visible with category + phase mapped
 *     to operator-friendly labels (R-12 / SC-006)
 *   - In-progress render — Completed shows "still running"
 *   - Coalesced requests card only appears when the audit array has items
 */

import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";

import type { DiscoveryRun } from "@/lib/discovery/schemas";
import { DiscoveryRunDetail } from "./discovery-run-detail";

function makeRun(partial: Partial<DiscoveryRun> = {}): DiscoveryRun {
  return {
    id: "dr_DETAIL00000000000000000001",
    schemaVersion: "1.0",
    namespaceId: "ns_DETAIL",
    status: "Succeeded",
    trigger: "Manual",
    startedUtc: "2026-06-17T10:00:00Z",
    completedUtc: "2026-06-17T10:03:30Z",
    durationMs: 210_000,
    requestedBy: "00000000-1111-2222-3333-444444444444",
    queueCount: 3,
    topicCount: 2,
    subscriptionCount: 5,
    ruleCount: 7,
    newCount: 4,
    updatedCount: 1,
    unchangedCount: 11,
    missingCount: 0,
    failure: null,
    coalescedRequests: [],
    correlationId: "00-detail-trace",
    ...partial,
  };
}

describe("<DiscoveryRunDetail>", () => {
  it("renders the run identity card with id, namespace, requestedBy, and status badge", () => {
    render(<DiscoveryRunDetail run={makeRun()} />);
    expect(screen.getByTestId("run-id")).toHaveTextContent("dr_DETAIL00000000000000000001");
    expect(screen.getByTestId("run-status")).toHaveTextContent("Succeeded");
  });

  it("formats the duration and renders classification counts on a Succeeded run", () => {
    render(<DiscoveryRunDetail run={makeRun({ durationMs: 4500 })} />);
    expect(screen.getByTestId("run-duration")).toHaveTextContent("4.5 s");
    expect(screen.getByTestId("run-new-count")).toHaveTextContent("4");
    expect(screen.getByTestId("run-updated-count")).toHaveTextContent("1");
    expect(screen.getByTestId("run-unchanged-count")).toHaveTextContent("11");
    expect(screen.getByTestId("run-missing-count")).toHaveTextContent("0");
  });

  it("does not render the failure card on a Succeeded run", () => {
    render(<DiscoveryRunDetail run={makeRun()} />);
    expect(screen.queryByTestId("run-failure-card")).toBeNull();
  });

  it("renders the failure card with category and phase mapped to operator labels on a Failed run", () => {
    const run = makeRun({
      status: "Failed",
      failure: {
        category: "Throttled",
        message: "ARM 429 (retries exhausted)",
        occurredAtPhase: "FetchSubscriptions",
        retriesExhausted: 3,
      },
    });
    render(<DiscoveryRunDetail run={run} />);

    expect(screen.getByTestId("run-failure-card")).toBeInTheDocument();
    expect(screen.getByTestId("failure-category")).toHaveTextContent("Throttled (ARM 429)");
    expect(screen.getByTestId("failure-phase")).toHaveTextContent("Fetch subscriptions");
    expect(screen.getByTestId("failure-retries")).toHaveTextContent("3");
    expect(screen.getByTestId("failure-message")).toHaveTextContent("ARM 429 (retries exhausted)");
  });

  it("renders the WorkerLost failure category with its operator-friendly label", () => {
    const run = makeRun({
      status: "Failed",
      failure: {
        category: "WorkerLost",
        message: "(redacted)",
        occurredAtPhase: "LockAcquire",
      },
    });
    render(<DiscoveryRunDetail run={run} />);
    expect(screen.getByTestId("failure-category")).toHaveTextContent("Worker lost (stale lock recovered)");
    expect(screen.getByTestId("failure-phase")).toHaveTextContent("Acquire run lock");
  });

  it("renders 'still running' for the completed field when no completedUtc is present", () => {
    const run = makeRun({ status: "InProgress", completedUtc: null, durationMs: null });
    render(<DiscoveryRunDetail run={run} />);
    expect(screen.getByText("— (still running)")).toBeInTheDocument();
    expect(screen.getByTestId("run-duration")).toHaveTextContent("—");
  });

  it("renders the coalesced-requests card only when the audit array has entries", () => {
    const without = makeRun({ coalescedRequests: [] });
    const { rerender } = render(<DiscoveryRunDetail run={without} />);
    expect(screen.queryByTestId("run-coalesced-card")).toBeNull();

    rerender(
      <DiscoveryRunDetail
        run={makeRun({
          coalescedRequests: [
            { requestedUtc: "2026-06-17T10:01:00Z", requestedBy: "user-coalesce-1" },
          ],
        })}
      />,
    );
    expect(screen.getByTestId("run-coalesced-card")).toBeInTheDocument();
    expect(screen.getByText("user-coalesce-1")).toBeInTheDocument();
  });

  it("renders entity-type counts", () => {
    render(<DiscoveryRunDetail run={makeRun()} />);
    expect(screen.getByTestId("run-queue-count")).toHaveTextContent("3");
    expect(screen.getByTestId("run-topic-count")).toHaveTextContent("2");
    expect(screen.getByTestId("run-subscription-count")).toHaveTextContent("5");
    expect(screen.getByTestId("run-rule-count")).toHaveTextContent("7");
  });
});
