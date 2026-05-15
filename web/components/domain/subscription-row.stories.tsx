import type { Meta, StoryObj } from "@storybook/nextjs";

import { SubscriptionRow } from "./subscription-row";
import type { SubscriptionSummary } from "./subscription-types";

const meta: Meta<typeof SubscriptionRow> = {
  title: "Domain/SubscriptionRow",
  component: SubscriptionRow,
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj<typeof SubscriptionRow>;

const ACTIVE: SubscriptionSummary = {
  id: "s-001",
  name: "billing-pipeline",
  parentTopic: "orders.events",
  status: "active",
  messageCount: 8_120,
  deadLetterCount: 0,
};

export const Active: Story = { args: { subscription: ACTIVE } };
export const Idle: Story = {
  args: { subscription: { ...ACTIVE, status: "idle", messageCount: 4 } },
};
export const ErrorState: Story = {
  args: { subscription: { ...ACTIVE, status: "error" } },
};
export const DeadLettered: Story = {
  args: { subscription: { ...ACTIVE, status: "dead-lettered", deadLetterCount: 3 } },
};

export const OversizedName: Story = {
  args: {
    subscription: {
      ...ACTIVE,
      name: "operations-platform-billing-pipeline-prod-westus3-orders-events-shadow",
    },
  },
};

export const Rtl: Story = {
  args: { subscription: ACTIVE },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
