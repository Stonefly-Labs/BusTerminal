import type { Meta, StoryObj } from "@storybook/nextjs";

import { MessageCountIndicator } from "./message-count-indicator";

const meta: Meta<typeof MessageCountIndicator> = {
  title: "Domain/MessageCountIndicator",
  component: MessageCountIndicator,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof MessageCountIndicator>;

export const Zero: Story = { args: { count: 0 } };
export const Default: Story = { args: { count: 1_240 } };
export const High: Story = { args: { count: 1_240_000 } };

export const WithSparkline: Story = {
  args: {
    count: 1_240,
    sparkline: (
      <svg width="48" height="14" viewBox="0 0 48 14" className="text-accent-primary">
        <polyline
          fill="none"
          stroke="currentColor"
          strokeWidth="1.5"
          points="0,10 8,8 16,9 24,5 32,6 40,3 48,4"
        />
      </svg>
    ),
  },
};

export const Rtl: Story = {
  args: { count: 1_240 },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
