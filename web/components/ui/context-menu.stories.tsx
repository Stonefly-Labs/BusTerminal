import type { Meta, StoryObj } from "@storybook/nextjs";

import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuLabel,
  ContextMenuSeparator,
  ContextMenuTrigger,
} from "./context-menu";

const meta: Meta<typeof ContextMenu> = {
  title: "Primitives/ContextMenu",
  component: ContextMenu,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof ContextMenu>;

export const Default: Story = {
  render: () => (
    <ContextMenu>
      <ContextMenuTrigger className="flex h-32 w-72 items-center justify-center rounded-md border border-dashed border-border-default text-sm text-foreground-muted">
        Right-click me
      </ContextMenuTrigger>
      <ContextMenuContent>
        <ContextMenuLabel>Row actions</ContextMenuLabel>
        <ContextMenuItem>Inspect</ContextMenuItem>
        <ContextMenuItem>Replay</ContextMenuItem>
        <ContextMenuSeparator />
        <ContextMenuItem intent="destructive">Delete</ContextMenuItem>
      </ContextMenuContent>
    </ContextMenu>
  ),
};
