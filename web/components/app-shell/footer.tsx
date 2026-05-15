import * as React from "react";

import { cn } from "@/lib/design-system/cn";

export const Footer = React.forwardRef<HTMLElement, React.HTMLAttributes<HTMLElement>>(
  function Footer({ className, children, ...rest }, ref) {
    return (
      <footer
        ref={ref}
        className={cn(
          "flex h-10 items-center justify-between border-t border-border-default bg-surface-elevated px-4 text-xs text-foreground-muted",
          className,
        )}
        {...rest}
      >
        {children ?? (
          <>
            <span>BusTerminal · foundation</span>
            <span className="font-mono">v0</span>
          </>
        )}
      </footer>
    );
  },
);
