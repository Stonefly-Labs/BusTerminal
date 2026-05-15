"use client";

import * as React from "react";

import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { cn } from "@/lib/design-system/cn";

export interface SplitPanelProps {
  readonly start: React.ReactNode;
  readonly end: React.ReactNode;
  readonly direction?: "horizontal" | "vertical";
  readonly defaultStartSize?: number;
  readonly className?: string;
}

export function SplitPanel({
  start,
  end,
  direction = "horizontal",
  defaultStartSize = 50,
  className,
}: SplitPanelProps) {
  return (
    <div className={cn("h-full w-full", className)}>
      <ResizablePanelGroup direction={direction}>
        <ResizablePanel defaultSize={defaultStartSize} minSize={20}>
          {start}
        </ResizablePanel>
        <ResizableHandle withHandle />
        <ResizablePanel defaultSize={100 - defaultStartSize}>{end}</ResizablePanel>
      </ResizablePanelGroup>
    </div>
  );
}
