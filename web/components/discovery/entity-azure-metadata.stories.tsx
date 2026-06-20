/**
 * Spec 009 / T079 / US2. Stories for `<EntityAzureMetadata>`.
 * Each story renders the panel with realistic `azureSourced` shapes for
 * Queue, Topic, Subscription and Rule (including the Rule edge case where
 * filter + action expressions are absent).
 */

import type { Meta, StoryObj } from "@storybook/nextjs";

import { EntityAzureMetadata } from "./entity-azure-metadata";

const meta: Meta<typeof EntityAzureMetadata> = {
  title: "Discovery/EntityAzureMetadata",
  component: EntityAzureMetadata,
  parameters: { layout: "padded" },
};

export default meta;
type Story = StoryObj<typeof EntityAzureMetadata>;

export const Queue: Story = {
  args: {
    entity: {
      entityType: "Queue",
      azureSourced: {
        status: "Active",
        azureResourceId:
          "/subscriptions/x/resourceGroups/y/providers/Microsoft.ServiceBus/namespaces/myns/queues/orders-inbox",
        lockDuration: "PT1M",
        maxDeliveryCount: 10,
        duplicateDetection: { enabled: true, historyTimeWindow: "PT10M" },
        deadLettering: { deadLetterOnMessageExpiration: true },
        partitioning: { enabled: false },
        session: { enabled: false },
        forwarding: { forwardTo: null },
        defaultTimeToLive: "P14D",
        maxSizeInMegabytes: 5120,
      },
    },
  },
};

export const Topic: Story = {
  args: {
    entity: {
      entityType: "Topic",
      azureSourced: {
        status: "Active",
        azureResourceId: "/subscriptions/x/topics/t-orders",
        duplicateDetection: { enabled: false, historyTimeWindow: null },
        partitioning: { enabled: true },
        defaultTimeToLive: "P14D",
        maxSizeInMegabytes: 1024,
      },
    },
  },
};

export const Subscription: Story = {
  args: {
    entity: {
      entityType: "Subscription",
      azureSourced: {
        status: "Active",
        azureResourceId: "/subscriptions/x/subscriptions/s-fulfillment",
        lockDuration: "PT30S",
        maxDeliveryCount: 5,
        deadLettering: { deadLetterOnMessageExpiration: false },
        session: { enabled: true },
        forwarding: { forwardTo: "queues/dest" },
        defaultTimeToLive: "P1D",
      },
    },
  },
};

export const RuleWithExpressions: Story = {
  args: {
    entity: {
      entityType: "Rule",
      azureSourced: {
        status: "Active",
        azureResourceId: "/subscriptions/x/rules/r-1",
        filterType: "Sql",
        filterExpression: "type = 'orders'",
        actionExpression: "SET priority = 'high'",
      },
    },
  },
};

export const RuleEdgeCase_NoExpressions: Story = {
  args: {
    entity: {
      entityType: "Rule",
      azureSourced: {
        status: "Active",
        azureResourceId: "/subscriptions/x/rules/r-2",
        filterType: "Sql",
      },
    },
  },
};
