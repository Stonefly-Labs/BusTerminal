import type { Meta, StoryObj } from "@storybook/nextjs";

import { HealthSummaryIndicator } from "./health-summary-indicator";

const meta: Meta<typeof HealthSummaryIndicator> = {
  title: "Domain/HealthSummaryIndicator",
  component: HealthSummaryIndicator,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof HealthSummaryIndicator>;

export const Healthy: Story = {
  args: { counts: { healthy: 24, degraded: 0, unhealthy: 0 } },
};
export const Degraded: Story = {
  args: { counts: { healthy: 18, degraded: 3, unhealthy: 0 } },
};
export const Unhealthy: Story = {
  args: { counts: { healthy: 14, degraded: 2, unhealthy: 1 } },
};
export const Empty: Story = {
  args: { counts: { healthy: 0, degraded: 0, unhealthy: 0 } },
};

export const Rtl: Story = {
  args: { counts: { healthy: 18, degraded: 3, unhealthy: 0 } },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
