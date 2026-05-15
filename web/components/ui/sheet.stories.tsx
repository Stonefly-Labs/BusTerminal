import type { Meta, StoryObj } from "@storybook/nextjs";

import { Button } from "./button";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "./sheet";

const meta: Meta<typeof Sheet> = {
  title: "Primitives/Sheet",
  component: Sheet,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof Sheet>;

export const Default: Story = {
  render: () => (
    <Sheet>
      <SheetTrigger asChild>
        <Button>Open detail</Button>
      </SheetTrigger>
      <SheetContent>
        <SheetHeader>
          <SheetTitle>orders.in</SheetTitle>
          <SheetDescription>
            Premium queue · 1240 active · 0 dead-lettered
          </SheetDescription>
        </SheetHeader>
        <p className="text-sm text-foreground-muted">Detail panel content…</p>
      </SheetContent>
    </Sheet>
  ),
};
