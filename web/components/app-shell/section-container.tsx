import * as React from "react";

import { cn } from "@/lib/design-system/cn";

export const SectionContainer = React.forwardRef<HTMLElement, React.HTMLAttributes<HTMLElement>>(
  function SectionContainer({ className, ...rest }, ref) {
    return <section ref={ref} className={cn("flex flex-col gap-3", className)} {...rest} />;
  },
);
