import type { Meta, StoryObj } from "@storybook/nextjs";

import { EntityRelationshipBadge } from "./entity-relationship-badge";

const meta: Meta<typeof EntityRelationshipBadge> = {
  title: "Domain/EntityRelationshipBadge",
  component: EntityRelationshipBadge,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof EntityRelationshipBadge>;

export const Forwards: Story = {
  args: { kind: "forwards", from: "orders.in", to: "orders.archive" },
};
export const Subscribes: Story = {
  args: { kind: "subscribes", from: "billing-pipeline", to: "orders.events" },
};
export const DeadLetters: Story = {
  args: { kind: "deadLetters", from: "orders.in", to: "orders.dlq" },
};
export const Publishes: Story = {
  args: { kind: "publishes", from: "orders-api", to: "orders.events" },
};
export const ParentOf: Story = {
  args: { kind: "parentOf", from: "orders-westus", to: "orders.in" },
};

export const Rtl: Story = {
  args: { kind: "subscribes", from: "billing-pipeline", to: "orders.events" },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
