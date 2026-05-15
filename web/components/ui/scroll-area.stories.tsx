import type { Meta, StoryObj } from "@storybook/nextjs";

import { ScrollArea } from "./scroll-area";

const meta: Meta<typeof ScrollArea> = {
  title: "Primitives/ScrollArea",
  component: ScrollArea,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof ScrollArea>;

const ITEMS = Array.from({ length: 30 }, (_, index) => `Subscription ${index + 1}`);

export const Default: Story = {
  render: () => (
    <ScrollArea className="h-48 w-72 rounded-md border border-border-default p-3">
      <div className="flex flex-col gap-2">
        {ITEMS.map((item) => (
          <div key={item} className="text-sm">
            {item}
          </div>
        ))}
      </div>
    </ScrollArea>
  ),
};
