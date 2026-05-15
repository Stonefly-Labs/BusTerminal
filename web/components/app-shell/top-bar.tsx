"use client";

import * as React from "react";
import { Moon, Search, Sun, User } from "lucide-react";
import { useTheme } from "next-themes";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n";

export interface TopBarProps {
  readonly onSearchTrigger?: () => void;
  readonly className?: string;
}

/**
 * Top bar (T090): global search trigger + theme toggle + user menu placeholder.
 */
export function TopBar({ onSearchTrigger, className }: TopBarProps) {
  const { resolvedTheme, setTheme } = useTheme();
  const isDark = resolvedTheme === "dark";
  return (
    <header
      className={cn(
        "sticky top-0 z-30 flex h-12 items-center gap-2 border-b border-border-default bg-surface-canvas/95 px-3 backdrop-blur",
        className,
      )}
    >
      <Button
        intent="secondary"
        size="sm"
        className="me-auto justify-start gap-2 text-foreground-muted"
        onClick={onSearchTrigger}
        aria-label={t("appshell.topbar.search.placeholder")}
      >
        <Search className="size-4" />
        <span className="hidden md:inline">{t("appshell.topbar.search.placeholder")}</span>
      </Button>
      <Button
        intent="ghost"
        size="icon"
        onClick={() => setTheme(isDark ? "light" : "dark")}
        aria-label={isDark ? t("appshell.topbar.themeToggle.toLight") : t("appshell.topbar.themeToggle.toDark")}
      >
        {isDark ? <Sun /> : <Moon />}
      </Button>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button intent="ghost" size="icon" aria-label={t("appshell.topbar.userMenu.label")}>
            <User />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuLabel>BusTerminal</DropdownMenuLabel>
          <DropdownMenuSeparator />
          <DropdownMenuItem disabled>Profile (placeholder)</DropdownMenuItem>
          <DropdownMenuItem disabled>Sign out (placeholder)</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </header>
  );
}
