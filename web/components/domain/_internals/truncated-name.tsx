"use client";

import * as React from "react";

import { cn } from "@/lib/design-system/cn";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";

/**
 * Renders an entity name that truncates predictably via the CSS logical
 * `text-overflow: ellipsis` pattern (Tailwind `truncate`) and surfaces the
 * full value through a tooltip on hover AND keyboard focus.
 *
 * Used by the entity-card / entity-row composites (NamespaceCard, QueueRow,
 * TopicRow, SubscriptionRow, …) to satisfy the spec's "Long entity names /
 * wide content" edge case. The trigger receives `aria-label` set to the full
 * value so assistive technology hears the complete name even when the visible
 * span is clipped.
 */
export interface TruncatedNameProps
  extends Omit<React.HTMLAttributes<HTMLSpanElement>, "children"> {
  readonly name: string;
  readonly mono?: boolean;
  readonly headingLevel?: "h2" | "h3" | "h4" | "span";
}

export const TruncatedName = React.forwardRef<HTMLSpanElement, TruncatedNameProps>(
  function TruncatedName({ name, mono = false, headingLevel = "span", className, ...rest }, ref) {
    const Heading = headingLevel;
    return (
      <TooltipProvider delayDuration={200}>
        <Tooltip>
          <TooltipTrigger asChild>
            <Heading
              ref={ref as React.Ref<HTMLHeadingElement>}
              tabIndex={0}
              aria-label={name}
              data-testid="truncated-name-trigger"
              className={cn(
                "block min-w-0 max-w-full truncate",
                "rounded-sm outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
                mono && "font-mono",
                className,
              )}
              {...rest}
            >
              {name}
            </Heading>
          </TooltipTrigger>
          <TooltipContent data-testid="truncated-name-tooltip">{name}</TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  },
);
