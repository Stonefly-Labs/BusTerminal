"use client";

import * as React from "react";
import * as DialogPrimitive from "@radix-ui/react-dialog";
import { X } from "lucide-react";

import { cn } from "@/lib/design-system/cn";
import { variants, type VariantPropsOf } from "@/lib/design-system/variants";
import { t } from "@/lib/i18n";

export const Sheet = DialogPrimitive.Root;
export const SheetTrigger = DialogPrimitive.Trigger;
export const SheetClose = DialogPrimitive.Close;
export const SheetPortal = DialogPrimitive.Portal;

export const SheetOverlay = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Overlay>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Overlay>
>(function SheetOverlay({ className, ...rest }, ref) {
  return (
    <DialogPrimitive.Overlay
      ref={ref}
      className={cn(
        "fixed inset-0 z-50 bg-foreground-default/40 backdrop-blur-sm",
        "data-[state=open]:animate-in data-[state=closed]:animate-out",
        className,
      )}
      {...rest}
    />
  );
});

const sheetVariants = variants(
  cn(
    "fixed z-50 gap-4 bg-surface-overlay p-6 text-foreground-default shadow-elevation-overlay",
    "outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
    "transition-transform",
  ),
  {
    variants: {
      side: {
        top: "inset-x-0 top-0 border-b border-border-default",
        bottom: "inset-x-0 bottom-0 border-t border-border-default",
        start: "inset-y-0 start-0 h-full w-3/4 max-w-sm border-e border-border-default",
        end: "inset-y-0 end-0 h-full w-3/4 max-w-sm border-s border-border-default",
      },
    },
    defaultVariants: { side: "end" },
  },
);

export type SheetVariants = VariantPropsOf<typeof sheetVariants>;

export interface SheetContentProps
  extends React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content>,
    SheetVariants {}

export const SheetContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  SheetContentProps
>(function SheetContent({ side, className, children, ...rest }, ref) {
  return (
    <SheetPortal>
      <SheetOverlay />
      <DialogPrimitive.Content
        ref={ref}
        className={cn(sheetVariants({ side }), className)}
        {...rest}
      >
        {children}
        <DialogPrimitive.Close
          aria-label={t("sheet.close")}
          className={cn(
            "absolute end-4 top-4 inline-flex h-7 w-7 items-center justify-center rounded-sm text-foreground-muted",
            "transition-colors hover:bg-interactive-hover hover:text-foreground-default",
            "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
          )}
        >
          <X className="h-4 w-4" aria-hidden="true" />
        </DialogPrimitive.Close>
      </DialogPrimitive.Content>
    </SheetPortal>
  );
});

export function SheetHeader({ className, ...rest }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("mb-4 flex flex-col gap-1.5", className)} {...rest} />;
}

export function SheetFooter({ className, ...rest }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn("mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end", className)} {...rest} />
  );
}

export const SheetTitle = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Title>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(function SheetTitle({ className, ...rest }, ref) {
  return (
    <DialogPrimitive.Title
      ref={ref}
      className={cn("text-base font-semibold leading-none text-foreground-default", className)}
      {...rest}
    />
  );
});

export const SheetDescription = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Description>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(function SheetDescription({ className, ...rest }, ref) {
  return (
    <DialogPrimitive.Description
      ref={ref}
      className={cn("text-sm text-foreground-muted", className)}
      {...rest}
    />
  );
});
