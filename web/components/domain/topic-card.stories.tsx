import type { Meta, StoryObj } from "@storybook/nextjs";

import { TopicCard } from "./topic-card";
import type { TopicSummary } from "./topic-types";

const meta: Meta<typeof TopicCard> = {
  title: "Domain/TopicCard",
  component: TopicCard,
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj<typeof TopicCard>;

const ACTIVE: TopicSummary = {
  id: "t-001",
  name: "orders.events",
  status: "active",
  subscriptionCount: 4,
  messageCount: 18_420,
};

export const Active: Story = { args: { topic: ACTIVE } };
export const Idle: Story = {
  args: { topic: { ...ACTIVE, status: "idle", subscriptionCount: 0, messageCount: 0 } },
};
export const ErrorState: Story = {
  args: { topic: { ...ACTIVE, status: "error" } },
};

export const OversizedName: Story = {
  args: {
    topic: {
      ...ACTIVE,
      name: "operations-platform-event-routing-prod-westus3-orders-domain-events-shadow",
    },
  },
};

export const Rtl: Story = {
  args: { topic: ACTIVE },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
