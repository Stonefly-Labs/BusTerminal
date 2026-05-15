import type { Meta, StoryObj } from "@storybook/nextjs";

import { Switch } from "./switch";
import { Label } from "./label";

const meta: Meta<typeof Switch> = {
  title: "Primitives/Switch",
  component: Switch,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Switch>;

export const Default: Story = {
  render: () => (
    <div className="flex items-center gap-3">
      <Switch id="s1" />
      <Label htmlFor="s1">Auto-rotate connection strings</Label>
    </div>
  ),
};

export const States: Story = {
  render: () => (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-3">
        <Switch id="ss1" />
        <Label htmlFor="ss1">Off</Label>
      </div>
      <div className="flex items-center gap-3">
        <Switch id="ss2" defaultChecked />
        <Label htmlFor="ss2">On</Label>
      </div>
      <div className="flex items-center gap-3">
        <Switch id="ss3" disabled />
        <Label htmlFor="ss3">Disabled</Label>
      </div>
    </div>
  ),
};
