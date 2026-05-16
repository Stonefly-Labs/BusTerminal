import type { Meta, StoryObj } from "@storybook/nextjs";

import { Button } from "./button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "./dialog";

const meta: Meta<typeof Dialog> = {
  title: "Primitives/Dialog",
  component: Dialog,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Dialog>;

export const Default: Story = {
  render: () => (
    <Dialog>
      <DialogTrigger asChild>
        <Button>Configure dead-letter</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Dead-letter configuration</DialogTitle>
          <DialogDescription>
            Choose how poisoned messages are routed for orders.in.
          </DialogDescription>
        </DialogHeader>
        <p className="text-sm text-foreground-muted">Settings live here…</p>
        <DialogFooter>
          <Button intent="ghost">Cancel</Button>
          <Button>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  ),
};
