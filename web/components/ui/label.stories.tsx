import type { Meta, StoryObj } from "@storybook/nextjs";

import { Input } from "./input";
import { Label } from "./label";

const meta: Meta<typeof Label> = {
  title: "Primitives/Label",
  component: Label,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Label>;

export const Default: Story = {
  render: () => (
    <div className="flex w-80 flex-col gap-2">
      <Label htmlFor="ns-name">Namespace name</Label>
      <Input id="ns-name" />
    </div>
  ),
};
