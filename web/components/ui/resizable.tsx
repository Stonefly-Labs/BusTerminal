"use client";

import * as React from "react";
import { GripVertical } from "lucide-react";
import {
  Panel as PanelPrimitive,
  PanelGroup as PanelGroupPrimitive,
  PanelResizeHandle as PanelResizeHandlePrimitive,
  type PanelGroupProps,
  type PanelProps,
  type PanelResizeHandleProps,
} from "react-resizable-panels";

import { cn } from "@/lib/design-system/cn";

export function ResizablePanelGroup({ className, ...rest }: PanelGroupProps) {
  return (
    <PanelGroupPrimitive
      className={cn(
        "flex h-full w-full data-[panel-group-direction=vertical]:flex-col",
        className,
      )}
      {...rest}
    />
  );
}

export const ResizablePanel = PanelPrimitive as React.FC<PanelProps>;

export interface ResizableHandleProps extends PanelResizeHandleProps {
  readonly withHandle?: boolean;
}

export function ResizableHandle({ withHandle, className, ...rest }: ResizableHandleProps) {
  return (
    <PanelResizeHandlePrimitive
      className={cn(
        "relative flex w-px items-center justify-center bg-border-default",
        "focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-(--focus-ring-color)",
        "data-[panel-group-direction=vertical]:h-px data-[panel-group-direction=vertical]:w-full",
        className,
      )}
      {...rest}
    >
      {withHandle ? (
        <span className="z-10 flex h-4 w-3 items-center justify-center rounded-sm border border-border-default bg-surface-elevated">
          <GripVertical className="size-2.5" aria-hidden="true" />
        </span>
      ) : null}
    </PanelResizeHandlePrimitive>
  );
}
