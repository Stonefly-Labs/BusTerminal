import * as React from "react";

import { cn } from "@/lib/design-system/cn";

export const PageContainer = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  function PageContainer({ className, ...rest }, ref) {
    return (
      <div
        ref={ref}
        className={cn("mx-auto flex w-full max-w-screen-2xl flex-col gap-6 px-4 py-6 md:px-8", className)}
        {...rest}
      />
    );
  },
);
