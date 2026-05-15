import type { Meta, StoryObj } from "@storybook/nextjs";

import { DeadLetterIndicator } from "./dead-letter-indicator";

const meta: Meta<typeof DeadLetterIndicator> = {
  title: "Domain/DeadLetterIndicator",
  component: DeadLetterIndicator,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof DeadLetterIndicator>;

export const Zero: Story = { args: { count: 0 } };
export const Low: Story = { args: { count: 3 } };
export const High: Story = { args: { count: 1240 } };

export const Sizes: Story = {
  render: () => (
    <div className="flex items-center gap-3">
      <DeadLetterIndicator count={14} size="sm" />
      <DeadLetterIndicator count={14} />
    </div>
  ),
};

export const Rtl: Story = {
  args: { count: 14 },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
