import type { Meta, StoryObj } from "@storybook/nextjs";

import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "./resizable";

const meta: Meta<typeof ResizablePanelGroup> = {
  title: "Primitives/Resizable",
  component: ResizablePanelGroup,
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj<typeof ResizablePanelGroup>;

export const Default: Story = {
  render: () => (
    <div className="h-72 w-[40rem] overflow-hidden rounded-md border border-border-default">
      <ResizablePanelGroup direction="horizontal">
        <ResizablePanel defaultSize={40} minSize={20}>
          <div className="flex h-full items-center justify-center bg-surface-muted text-sm text-foreground-muted">
            Master pane
          </div>
        </ResizablePanel>
        <ResizableHandle withHandle />
        <ResizablePanel defaultSize={60}>
          <div className="flex h-full items-center justify-center bg-surface-elevated text-sm text-foreground-default">
            Detail pane
          </div>
        </ResizablePanel>
      </ResizablePanelGroup>
    </div>
  ),
};
