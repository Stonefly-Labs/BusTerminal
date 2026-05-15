import type { Meta, StoryObj } from "@storybook/nextjs";

import { RadioGroup, RadioGroupItem } from "./radio-group";
import { Label } from "./label";

const meta: Meta<typeof RadioGroup> = {
  title: "Primitives/RadioGroup",
  component: RadioGroup,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof RadioGroup>;

export const Default: Story = {
  render: () => (
    <RadioGroup defaultValue="active">
      <div className="flex items-center gap-2">
        <RadioGroupItem value="active" id="r-active" />
        <Label htmlFor="r-active">Active</Label>
      </div>
      <div className="flex items-center gap-2">
        <RadioGroupItem value="paused" id="r-paused" />
        <Label htmlFor="r-paused">Paused</Label>
      </div>
      <div className="flex items-center gap-2">
        <RadioGroupItem value="disabled" id="r-disabled" disabled />
        <Label htmlFor="r-disabled">Disabled</Label>
      </div>
    </RadioGroup>
  ),
};
