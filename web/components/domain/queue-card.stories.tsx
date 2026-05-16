import type { Meta, StoryObj } from "@storybook/nextjs";

import { QueueCard } from "./queue-card";
import type { QueueSummary } from "./queue-types";

const meta: Meta<typeof QueueCard> = {
  title: "Domain/QueueCard",
  component: QueueCard,
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj<typeof QueueCard>;

const ACTIVE: QueueSummary = {
  id: "q-001",
  name: "orders.in",
  status: "active",
  activeCount: 1240,
  deadLetterCount: 0,
};

export const Active: Story = { args: { queue: ACTIVE } };
export const Idle: Story = {
  args: { queue: { ...ACTIVE, status: "idle", activeCount: 22 } },
};
export const ErrorState: Story = {
  args: { queue: { ...ACTIVE, status: "error", activeCount: 0, deadLetterCount: 14 } },
};
export const DeadLettered: Story = {
  args: { queue: { ...ACTIVE, status: "dead-lettered", deadLetterCount: 7 } },
};

export const OversizedName: Story = {
  args: {
    queue: {
      ...ACTIVE,
      name: "operations-platform-message-routing-prod-westus3-orders-replay-canary-shadow",
    },
  },
};

export const Rtl: Story = {
  args: { queue: ACTIVE },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
