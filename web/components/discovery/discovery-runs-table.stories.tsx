/**
 * Spec 009 / T092. DiscoveryRunsTable stories — empty, populated, mixed
 * statuses (Succeeded + Failed + InProgress + Queued), continuation
 * available, last-page.
 */

import type { Meta, StoryObj } from "@storybook/nextjs";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import type { DiscoveryRun } from "@/lib/discovery/schemas";

import { DiscoveryRunsTable } from "./discovery-runs-table";

function makeRun(partial: Partial<DiscoveryRun> = {}): DiscoveryRun {
  return {
    id: "dr_DEMO00000000000000000001",
    schemaVersion: "1.0",
    namespaceId: "ns_demo",
    status: "Succeeded",
    trigger: "Manual",
    startedUtc: "2026-06-17T10:00:00Z",
    completedUtc: "2026-06-17T10:02:30Z",
    durationMs: 150_000,
    requestedBy: "00000000-1111-2222-3333-444444444444",
    queueCount: 3,
    topicCount: 2,
    subscriptionCount: 7,
    ruleCount: 5,
    newCount: 1,
    updatedCount: 2,
    unchangedCount: 12,
    missingCount: 0,
    failure: null,
    coalescedRequests: [],
    correlationId: "00-demo-trace",
    ...partial,
  };
}

function StoryShell({ children }: { readonly children: React.ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={client}>
      <div className="p-6">{children}</div>
    </QueryClientProvider>
  );
}

const meta: Meta<typeof DiscoveryRunsTable> = {
  title: "Discovery/DiscoveryRunsTable",
  component: DiscoveryRunsTable,
  parameters: { layout: "padded" },
  decorators: [(Story) => <StoryShell>{Story()}</StoryShell>],
};

export default meta;
type Story = StoryObj<typeof DiscoveryRunsTable>;

export const Empty: Story = {
  args: {
    namespaceId: "ns_demo",
    initialItems: [],
  },
};

export const Populated: Story = {
  args: {
    namespaceId: "ns_demo",
    initialItems: [
      makeRun({ id: "dr_NEWEST", startedUtc: "2026-06-17T12:00:00Z" }),
      makeRun({ id: "dr_MIDDLE", startedUtc: "2026-06-17T11:00:00Z" }),
      makeRun({ id: "dr_OLDEST", startedUtc: "2026-06-17T09:00:00Z", durationMs: 240_000 }),
    ],
  },
};

export const MixedStatuses: Story = {
  args: {
    namespaceId: "ns_demo",
    initialItems: [
      makeRun({ id: "dr_OK", status: "Succeeded" }),
      makeRun({
        id: "dr_FAILED",
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
      makeRun({ id: "dr_INFLIGHT", status: "InProgress", completedUtc: null, durationMs: null }),
      makeRun({ id: "dr_QUEUED", status: "Queued", completedUtc: null, durationMs: null }),
    ],
  },
};

export const HasMorePages: Story = {
  args: {
    namespaceId: "ns_demo",
    initialItems: Array.from({ length: 3 }, (_, i) => makeRun({ id: `dr_PAGE_${i}` })),
    initialContinuationToken: "demo-cursor-page-2",
  },
};
