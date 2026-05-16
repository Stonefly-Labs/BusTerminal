import type { NamespaceSummary } from "@/components/domain/namespace-card";
import type { QueueSummary } from "@/components/domain/queue-types";
import type { TopicSummary } from "@/components/domain/topic-types";
import type { SubscriptionSummary } from "@/components/domain/subscription-types";

/**
 * Realistic sample data driving the Phase 7 / US5 domain composite showcase.
 * Lives under the `_showcase` private route segment so it is excluded from the
 * routed build but importable by the demo page (T140).
 */

export const SHOWCASE_NAMESPACE: NamespaceSummary = {
  id: "ns-001",
  name: "orders-westus",
  tier: "premium",
  region: "westus",
  status: "healthy",
  queueCount: 12,
  topicCount: 5,
};

export const SHOWCASE_DOMAIN_QUEUE: QueueSummary = {
  id: "q-001",
  name: "orders.in",
  status: "active",
  activeCount: 1_240,
  deadLetterCount: 0,
  maxDelivery: 10,
};

export const SHOWCASE_DOMAIN_QUEUE_DLQ: QueueSummary = {
  id: "q-002",
  name: "billing.errors",
  status: "dead-lettered",
  activeCount: 14,
  deadLetterCount: 7,
  maxDelivery: 10,
};

export const SHOWCASE_TOPIC: TopicSummary = {
  id: "t-001",
  name: "orders.events",
  status: "active",
  subscriptionCount: 4,
  messageCount: 18_420,
};

export const SHOWCASE_SUBSCRIPTION: SubscriptionSummary = {
  id: "s-001",
  name: "billing-pipeline",
  parentTopic: "orders.events",
  status: "active",
  messageCount: 8_120,
  deadLetterCount: 0,
};

export const SHOWCASE_AZURE_RESOURCE_ID =
  "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-orders/providers/Microsoft.ServiceBus/namespaces/orders-westus";

export const SHOWCASE_PORTAL_URL = `https://portal.azure.com/#@/resource${SHOWCASE_AZURE_RESOURCE_ID}`;

export const SHOWCASE_METADATA = [
  { key: "Namespace", value: "orders-westus", mono: false },
  { key: "Resource ID", value: SHOWCASE_AZURE_RESOURCE_ID },
  { key: "Created", value: "2026-05-14T18:22:00Z" },
  { key: "Owner", value: "ops-team@busterminal.dev" },
] as const;

export const SHOWCASE_NOW = new Date("2026-05-15T12:00:00Z");
export const SHOWCASE_JOB_STARTED_AT = new Date(SHOWCASE_NOW.getTime() - 5 * 60_000);
