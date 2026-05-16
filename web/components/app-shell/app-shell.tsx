"use client";

import * as React from "react";

import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";

export interface AppShellProps {
  readonly sidebar?: React.ReactNode;
  readonly topBar?: React.ReactNode;
  readonly main: React.ReactNode;
  readonly footer?: React.ReactNode;
  readonly className?: string;
}

/**
 * Top-level application shell composition (T088). Slots are intentionally
 * decoupled — page-level layouts assemble the actual sidebar/top-bar/footer.
 */
export function AppShell({ sidebar, topBar, main, footer, className }: AppShellProps) {
  return (
    <div className={cn("flex min-h-screen w-full bg-surface-canvas text-foreground-default", className)}>
      <a
        href="#bt-main"
        className="sr-only focus:not-sr-only focus:absolute focus:top-2 focus:start-2 focus:z-50 focus:rounded-md focus:bg-accent-primary focus:px-3 focus:py-2 focus:text-accent-primary-foreground"
      >
        {t("a11y.skipToContent")}
      </a>
      {sidebar}
      <div className="flex min-w-0 flex-1 flex-col">
        {topBar}
        <main id="bt-main" className="flex-1 overflow-auto">
          {main}
        </main>
        {footer}
      </div>
    </div>
  );
}
