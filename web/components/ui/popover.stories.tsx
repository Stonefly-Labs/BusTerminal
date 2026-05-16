import type { Meta, StoryObj } from "@storybook/nextjs";

import { Popover, PopoverContent, PopoverTrigger } from "./popover";
import { Button } from "./button";

const meta: Meta<typeof Popover> = {
  title: "Primitives/Popover",
  component: Popover,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Popover>;

export const Default: Story = {
  render: () => (
    <Popover>
      <PopoverTrigger asChild>
        <Button intent="secondary">Open details</Button>
      </PopoverTrigger>
      <PopoverContent>
        <div className="flex flex-col gap-2">
          <p className="text-sm font-medium">Premium namespace</p>
          <p className="text-xs text-foreground-muted">2 queues · 3 topics · 4 subscriptions</p>
        </div>
      </PopoverContent>
    </Popover>
  ),
};
