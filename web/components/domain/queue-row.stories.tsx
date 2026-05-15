import type { Meta, StoryObj } from "@storybook/nextjs";

import { QueueRow } from "./queue-row";
import type { QueueSummary } from "./queue-types";

const meta: Meta<typeof QueueRow> = {
  title: "Domain/QueueRow",
  component: QueueRow,
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj<typeof QueueRow>;

const ACTIVE: QueueSummary = {
  id: "q-001",
  name: "orders.in",
  status: "active",
  activeCount: 1240,
  deadLetterCount: 0,
  maxDelivery: 10,
};

export const Active: Story = { args: { queue: ACTIVE } };
export const Idle: Story = {
  args: {
    queue: { ...ACTIVE, id: "q-idle", name: "audit.in", status: "idle", activeCount: 22 },
  },
};
export const ErrorState: Story = {
  args: {
    queue: {
      ...ACTIVE,
      id: "q-err",
      name: "billing.errors",
      status: "error",
      activeCount: 0,
      deadLetterCount: 14,
    },
  },
};
export const DeadLettered: Story = {
  args: {
    queue: {
      ...ACTIVE,
      id: "q-dl",
      name: "shipping.notifications",
      status: "dead-lettered",
      activeCount: 312,
      deadLetterCount: 7,
    },
  },
};

export const OversizedName: Story = {
  args: {
    queue: {
      ...ACTIVE,
      id: "q-long",
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
