import type { Meta, StoryObj } from "@storybook/nextjs";
import { Loader2, Save, Trash2 } from "lucide-react";

import { Button } from "./button";

const meta: Meta<typeof Button> = {
  title: "Primitives/Button",
  component: Button,
  parameters: { layout: "centered" },
  argTypes: {
    intent: {
      control: "select",
      options: ["primary", "secondary", "outline", "ghost", "destructive", "link"],
    },
    size: {
      control: "select",
      options: ["sm", "md", "lg", "icon"],
    },
    disabled: { control: "boolean" },
  },
};

export default meta;

type Story = StoryObj<typeof Button>;

export const Default: Story = {
  args: { children: "Save changes" },
};

export const AllIntents: Story = {
  render: () => (
    <div className="flex flex-wrap gap-3">
      <Button intent="primary">Primary</Button>
      <Button intent="secondary">Secondary</Button>
      <Button intent="outline">Outline</Button>
      <Button intent="ghost">Ghost</Button>
      <Button intent="destructive">Destructive</Button>
      <Button intent="link">Link</Button>
    </div>
  ),
};

export const AllSizes: Story = {
  render: () => (
    <div className="flex items-center gap-3">
      <Button size="sm">Small</Button>
      <Button size="md">Medium</Button>
      <Button size="lg">Large</Button>
      <Button size="icon" aria-label="Save">
        <Save />
      </Button>
    </div>
  ),
};

export const States: Story = {
  render: () => (
    <div className="flex flex-col gap-3">
      <div className="flex gap-3">
        <Button>Idle</Button>
        <Button disabled>Disabled</Button>
        <Button intent="destructive">
          <Trash2 /> Delete
        </Button>
      </div>
      <div className="flex gap-3">
        <Button disabled>
          <Loader2 className="animate-spin" /> Saving
        </Button>
        <Button intent="secondary">
          <Save /> With icon
        </Button>
      </div>
    </div>
  ),
};

export const AsChild: Story = {
  render: () => (
    <Button asChild>
      <a href="#">Link rendered as button</a>
    </Button>
  ),
};
