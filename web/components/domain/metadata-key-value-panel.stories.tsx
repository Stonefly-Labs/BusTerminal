import type { Meta, StoryObj } from "@storybook/nextjs";

import { MetadataKeyValuePanel } from "./metadata-key-value-panel";

const meta: Meta<typeof MetadataKeyValuePanel> = {
  title: "Domain/MetadataKeyValuePanel",
  component: MetadataKeyValuePanel,
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj<typeof MetadataKeyValuePanel>;

export const Populated: Story = {
  args: {
    entries: [
      { key: "Namespace", value: "orders-westus", mono: false },
      { key: "Resource ID", value: "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-orders/providers/Microsoft.ServiceBus/namespaces/orders-westus" },
      { key: "Created", value: "2026-05-14T18:22:00Z" },
      { key: "Owner", value: "ops-team@busterminal.dev" },
    ],
  },
};

export const Empty: Story = {
  args: { entries: [] },
};

export const Rtl: Story = {
  args: {
    entries: [
      { key: "Namespace", value: "orders-westus", mono: false },
      { key: "Owner", value: "ops-team@busterminal.dev" },
    ],
  },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
