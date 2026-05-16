import * as React from "react";
import { AlertCircle, CheckCircle2, Info } from "lucide-react";

import { cn } from "@/lib/design-system/cn";

export interface InlineValidationProps extends React.HTMLAttributes<HTMLParagraphElement> {
  readonly intent: "success" | "error" | "info";
  readonly message: string;
}

/**
 * Inline validation message for non-form contexts (T084 / FR-020). For
 * RHF + Zod fields, use `<FormMessage />` instead.
 */
export function InlineValidation({ intent, message, className, ...rest }: InlineValidationProps) {
  const Icon = intent === "success" ? CheckCircle2 : intent === "error" ? AlertCircle : Info;
  const colorClass =
    intent === "success"
      ? "text-success-foreground"
      : intent === "error"
        ? "text-error-foreground"
        : "text-info-foreground";
  return (
    <p
      role={intent === "error" ? "alert" : "status"}
      className={cn("inline-flex items-center gap-1 text-xs", colorClass, className)}
      {...rest}
    >
      <Icon className="size-3.5" aria-hidden="true" />
      {message}
    </p>
  );
}
