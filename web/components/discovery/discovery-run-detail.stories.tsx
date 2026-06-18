/**
 * Spec 009 / T092. DiscoveryRunDetail stories — succeeded, failed (Throttled
 * + WorkerLost + Authn variants), in-progress, with coalesced requests.
 */

import type { Meta, StoryObj } from "@storybook/nextjs";

import type { DiscoveryRun } from "@/lib/discovery/schemas";

import { DiscoveryRunDetail } from "./discovery-run-detail";

function makeRun(partial: Partial<DiscoveryRun> = {}): DiscoveryRun {
  return {
    id: "dr_DEMO00000000000000000001",
    schemaVersion: "1.0",
    namespaceId: "ns_demo",
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
    correlationId: "00-demo-trace",
    ...partial,
  };
}

const meta: Meta<typeof DiscoveryRunDetail> = {
  title: "Discovery/DiscoveryRunDetail",
  component: DiscoveryRunDetail,
  parameters: { layout: "padded" },
};

export default meta;
type Story = StoryObj<typeof DiscoveryRunDetail>;

export const Succeeded: Story = {
  args: { run: makeRun() },
};

export const InProgress: Story = {
  args: {
    run: makeRun({
      status: "InProgress",
      completedUtc: null,
      durationMs: null,
      newCount: 0,
      updatedCount: 0,
      unchangedCount: 0,
      missingCount: 0,
    }),
  },
};

export const FailedThrottled: Story = {
  args: {
    run: makeRun({
      status: "Failed",
      completedUtc: "2026-06-17T10:05:00Z",
      durationMs: 300_000,
      failure: {
        category: "Throttled",
        message: "ARM 429 (retries exhausted)",
        occurredAtPhase: "FetchSubscriptions",
        retriesExhausted: 3,
      },
    }),
  },
};

export const FailedAuthn: Story = {
  args: {
    run: makeRun({
      status: "Failed",
      completedUtc: "2026-06-17T10:00:45Z",
      durationMs: 45_000,
      failure: {
        category: "Authn",
        message: "Failed to acquire token for ARM. Check workload identity federation.",
        occurredAtPhase: "FetchQueues",
        retriesExhausted: 0,
      },
    }),
  },
};

export const FailedWorkerLost: Story = {
  args: {
    run: makeRun({
      status: "Failed",
      completedUtc: "2026-06-17T10:05:11Z",
      durationMs: 311_000,
      failure: {
        category: "WorkerLost",
        message: "(redacted)",
        occurredAtPhase: "LockAcquire",
      },
    }),
  },
};

export const WithCoalescedRequests: Story = {
  args: {
    run: makeRun({
      coalescedRequests: [
        { requestedUtc: "2026-06-17T10:00:42Z", requestedBy: "user-coalesce-1" },
        { requestedUtc: "2026-06-17T10:01:18Z", requestedBy: "user-coalesce-2" },
      ],
    }),
  },
};
