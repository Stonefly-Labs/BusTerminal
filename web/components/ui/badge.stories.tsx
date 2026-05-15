import type { Meta, StoryObj } from "@storybook/nextjs";

import { Badge } from "./badge";

const meta: Meta<typeof Badge> = {
  title: "Primitives/Badge",
  component: Badge,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Badge>;

export const AllIntents: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      <Badge intent="neutral">Neutral</Badge>
      <Badge intent="accent">Accent</Badge>
      <Badge intent="success">Success</Badge>
      <Badge intent="warning">Warning</Badge>
      <Badge intent="error">Error</Badge>
      <Badge intent="info">Info</Badge>
      <Badge intent="outline">Outline</Badge>
    </div>
  ),
};
