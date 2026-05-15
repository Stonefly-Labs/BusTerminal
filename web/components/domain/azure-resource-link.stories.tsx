import type { Meta, StoryObj } from "@storybook/nextjs";

import { AzureResourceLink } from "./azure-resource-link";

const meta: Meta<typeof AzureResourceLink> = {
  title: "Domain/AzureResourceLink",
  component: AzureResourceLink,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof AzureResourceLink>;

const SAMPLE_ID =
  "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-orders/providers/Microsoft.ServiceBus/namespaces/orders-westus";

export const Default: Story = {
  args: {
    resourceId: SAMPLE_ID,
    portalUrl: `https://portal.azure.com/#@/resource${SAMPLE_ID}`,
  },
};

export const WithLabel: Story = {
  args: {
    resourceId: SAMPLE_ID,
    label: "orders-westus",
    portalUrl: `https://portal.azure.com/#@/resource${SAMPLE_ID}`,
  },
};

export const Rtl: Story = {
  args: {
    resourceId: SAMPLE_ID,
    label: "orders-westus",
    portalUrl: `https://portal.azure.com/#@/resource${SAMPLE_ID}`,
  },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
