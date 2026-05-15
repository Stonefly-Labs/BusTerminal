import type { Meta, StoryObj } from "@storybook/nextjs";

import { Separator } from "./separator";

const meta: Meta<typeof Separator> = {
  title: "Primitives/Separator",
  component: Separator,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Separator>;

export const Default: Story = {
  render: () => (
    <div className="w-72">
      <p className="text-sm">Above</p>
      <Separator className="my-3" />
      <p className="text-sm">Below</p>
    </div>
  ),
};

export const Vertical: Story = {
  render: () => (
    <div className="flex h-12 items-center gap-3">
      <span className="text-sm">A</span>
      <Separator orientation="vertical" />
      <span className="text-sm">B</span>
      <Separator orientation="vertical" />
      <span className="text-sm">C</span>
    </div>
  ),
};
