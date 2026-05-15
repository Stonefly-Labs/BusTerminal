import type { Meta, StoryObj } from "@storybook/nextjs";

import { NamespaceCard, type NamespaceSummary } from "./namespace-card";

const meta: Meta<typeof NamespaceCard> = {
  title: "Domain/NamespaceCard",
  component: NamespaceCard,
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj<typeof NamespaceCard>;

const HEALTHY: NamespaceSummary = {
  id: "ns-001",
  name: "orders-westus",
  tier: "premium",
  region: "westus",
  status: "healthy",
  queueCount: 12,
  topicCount: 5,
};

const DEGRADED: NamespaceSummary = {
  id: "ns-002",
  name: "billing-eastus2",
  tier: "standard",
  region: "eastus2",
  status: "degraded",
  queueCount: 7,
  topicCount: 3,
};

const UNHEALTHY: NamespaceSummary = {
  id: "ns-003",
  name: "audit-northeurope",
  tier: "basic",
  region: "northeurope",
  status: "unhealthy",
  queueCount: 2,
  topicCount: 0,
};

const LONG_NAME: NamespaceSummary = {
  ...HEALTHY,
  id: "ns-long",
  name: "operations-platform-message-routing-prod-westus3-ns-2026-rev-04-canary-shadow",
};

export const Healthy: Story = {
  args: { namespace: HEALTHY },
};

export const Degraded: Story = {
  args: { namespace: DEGRADED },
};

export const Unhealthy: Story = {
  args: { namespace: UNHEALTHY },
};

/**
 * The "Long entity names / wide content" edge case: visible name truncates
 * with CSS-only ellipsis and the full value surfaces via tooltip on hover
 * and on keyboard focus (Tab onto the name).
 */
export const OversizedName: Story = {
  args: { namespace: LONG_NAME },
};

export const Rtl: Story = {
  args: { namespace: HEALTHY },
  parameters: { direction: "rtl" },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
