import type { Meta, StoryObj } from "@storybook/nextjs";

import { EnvironmentBadge } from "./environment-badge";

const meta: Meta<typeof EnvironmentBadge> = {
  title: "Domain/EnvironmentBadge",
  component: EnvironmentBadge,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof EnvironmentBadge>;

export const AllEnvironments: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-2">
      <EnvironmentBadge environment="dev" />
      <EnvironmentBadge environment="test" />
      <EnvironmentBadge environment="staging" />
      <EnvironmentBadge environment="prod" />
    </div>
  ),
};

export const Sizes: Story = {
  render: () => (
    <div className="flex items-center gap-2">
      <EnvironmentBadge environment="prod" size="sm" />
      <EnvironmentBadge environment="prod" />
    </div>
  ),
};

export const Rtl: Story = {
  args: { environment: "prod" },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
