import type { Meta, StoryObj } from "@storybook/nextjs";

import { Checkbox } from "./checkbox";
import { Label } from "./label";

const meta: Meta<typeof Checkbox> = {
  title: "Primitives/Checkbox",
  component: Checkbox,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Checkbox>;

export const Default: Story = {
  render: () => (
    <div className="flex items-center gap-2">
      <Checkbox id="dl-on-read" />
      <Label htmlFor="dl-on-read">Enable dead-letter on read failure</Label>
    </div>
  ),
};

export const States: Story = {
  render: () => (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-2">
        <Checkbox id="s1" />
        <Label htmlFor="s1">Unchecked</Label>
      </div>
      <div className="flex items-center gap-2">
        <Checkbox id="s2" defaultChecked />
        <Label htmlFor="s2">Checked</Label>
      </div>
      <div className="flex items-center gap-2">
        <Checkbox id="s3" checked="indeterminate" />
        <Label htmlFor="s3">Indeterminate</Label>
      </div>
      <div className="flex items-center gap-2">
        <Checkbox id="s4" disabled />
        <Label htmlFor="s4">Disabled</Label>
      </div>
    </div>
  ),
};
