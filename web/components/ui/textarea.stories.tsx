import type { Meta, StoryObj } from "@storybook/nextjs";

import { Textarea } from "./textarea";
import { Label } from "./label";

const meta: Meta<typeof Textarea> = {
  title: "Primitives/Textarea",
  component: Textarea,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Textarea>;

export const Default: Story = {
  render: () => (
    <div className="flex w-96 flex-col gap-2">
      <Label htmlFor="rule-filter">Subscription rule SQL filter</Label>
      <Textarea id="rule-filter" placeholder="user.region = 'westus'" />
    </div>
  ),
};

export const States: Story = {
  render: () => (
    <div className="flex w-96 flex-col gap-4">
      <Textarea placeholder="Idle" />
      <Textarea placeholder="Disabled" disabled />
      <Textarea aria-invalid="true" defaultValue="invalid expression" />
    </div>
  ),
};
