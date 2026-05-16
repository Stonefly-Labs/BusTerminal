"use client";

import * as React from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";

export interface SidebarProps {
  readonly children?: React.ReactNode;
  readonly defaultCollapsed?: boolean;
  readonly className?: string;
}

const STORAGE_KEY = "bt:foundation:sidebar-collapsed";

/**
 * Application sidebar (T089). Collapse/expand state is keyboard-toggled via
 * `Cmd/Ctrl+B`, persisted to `localStorage`, and animates respectfully under
 * `prefers-reduced-motion` (the global CSS rule shortens transitions).
 */
export function Sidebar({ children, defaultCollapsed = false, className }: SidebarProps) {
  const [collapsed, setCollapsed] = React.useState<boolean>(() => {
    if (typeof window === "undefined") return defaultCollapsed;
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return raw == null ? defaultCollapsed : raw === "true";
  });

  React.useEffect(() => {
    if (typeof window === "undefined") return;
    window.localStorage.setItem(STORAGE_KEY, String(collapsed));
  }, [collapsed]);

  React.useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.key.toLowerCase() === "b" && (event.metaKey || event.ctrlKey)) {
        event.preventDefault();
        setCollapsed((prev) => !prev);
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  return (
    <aside
      data-collapsed={collapsed ? "true" : "false"}
      className={cn(
        "hidden lg:flex shrink-0 flex-col border-e border-border-default bg-surface-elevated transition-[width] duration-200",
        collapsed ? "w-14" : "w-72",
        className,
      )}
    >
      <div className="flex h-12 items-center justify-between gap-2 border-b border-border-muted px-2">
        {!collapsed ? (
          <span className="text-xs font-semibold uppercase tracking-wider text-foreground-muted">
            BusTerminal
          </span>
        ) : null}
        <Button
          intent="ghost"
          size="icon"
          onClick={() => setCollapsed((prev) => !prev)}
          aria-label={collapsed ? t("appshell.sidebar.toggle.expand") : t("appshell.sidebar.toggle.collapse")}
        >
          {collapsed ? <ChevronRight className="rtl:rotate-180" /> : <ChevronLeft className="rtl:rotate-180" />}
        </Button>
      </div>
      <nav className="flex flex-1 flex-col gap-1 overflow-y-auto p-2">{children}</nav>
    </aside>
  );
}

export interface SidebarItemProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  readonly active?: boolean;
  readonly icon?: React.ReactNode;
  readonly label: string;
}

export function SidebarItem({ active, icon, label, className, ...rest }: SidebarItemProps) {
  return (
    <button
      type="button"
      aria-current={active ? "page" : undefined}
      className={cn(
        "flex items-center gap-3 rounded-md px-3 py-2 text-sm",
        "transition-colors hover:bg-interactive-hover",
        active ? "bg-interactive-hover font-medium text-foreground-default" : "text-foreground-muted",
        "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--focus-ring-color)",
        className,
      )}
      {...rest}
    >
      {icon ? <span aria-hidden="true">{icon}</span> : null}
      <span className="truncate">{label}</span>
    </button>
  );
}
