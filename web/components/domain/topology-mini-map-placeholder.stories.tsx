import type { Meta, StoryObj } from "@storybook/nextjs";

import { TopologyMiniMapPlaceholder } from "./topology-mini-map-placeholder";

const meta: Meta<typeof TopologyMiniMapPlaceholder> = {
  title: "Domain/TopologyMiniMapPlaceholder",
  component: TopologyMiniMapPlaceholder,
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj<typeof TopologyMiniMapPlaceholder>;

export const Default: Story = {};

export const Tall: Story = {
  args: { height: 320 },
};

export const Rtl: Story = {
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
