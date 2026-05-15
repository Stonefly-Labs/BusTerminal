import type { Meta, StoryObj } from "@storybook/nextjs";

import { Button } from "./button";
import { Toaster, toast } from "./toast";

const meta: Meta<typeof Toaster> = {
  title: "Primitives/Toast",
  component: Toaster,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Toaster>;

export const Default: Story = {
  render: () => (
    <div className="flex flex-col gap-2">
      <div className="flex gap-2">
        <Button onClick={() => toast("Subscription updated")}>Default</Button>
        <Button intent="secondary" onClick={() => toast.success("Rule published")}>Success</Button>
        <Button intent="secondary" onClick={() => toast.warning("Approaching quota")}>Warning</Button>
        <Button intent="destructive" onClick={() => toast.error("Authorization failed")}>Error</Button>
      </div>
      <Toaster />
    </div>
  ),
};
