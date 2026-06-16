"use client";

import type { Route } from "next";
import { Database, Home as HomeIcon, Layers, type LucideIcon } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

import { AppShell } from "@/components/app-shell/app-shell";
import { Footer } from "@/components/app-shell/footer";
import { NavigationHeader } from "@/components/layout/navigation-header";
import { useResolvedRoleContext } from "@/components/auth/role-context";
import { authorizedRoles, type OperationClass } from "@/lib/auth/role-permission-matrix";
import { cn } from "@/lib/design-system/cn";

export interface NavigationShellProps {
  readonly children: ReactNode;
  readonly userMenu?: ReactNode;
}

interface NavEntry {
  readonly href: Route;
  readonly label: string;
  readonly operationClass: OperationClass;
  readonly icon?: LucideIcon;
  readonly matchPrefix?: boolean;
}

const NAV_ENTRIES: readonly NavEntry[] = [
  { href: "/home" as Route, label: "Home", operationClass: "Read", icon: HomeIcon },
  { href: "/platform-status" as Route, label: "Platform status", operationClass: "Read" },
  {
    href: "/registry" as Route,
    label: "Registry",
    operationClass: "Read",
    icon: Database,
    matchPrefix: true,
  },
  // Spec 008 / contracts/outputs-contract.md §3.2. Gated by `Read` so any
  // authenticated tenant user can browse the inventory; the wizard and write
  // actions inside the section are gated separately by the
  // namespace-administrator role at the API layer.
  {
    href: "/namespaces" as Route,
    label: "Namespaces",
    operationClass: "Read",
    icon: Layers,
    matchPrefix: true,
  },
];

function NavigationSidebar() {
  const pathname = usePathname();
  const { effectiveRoles } = useResolvedRoleContext();
  const visibleEntries = NAV_ENTRIES.filter((entry) => {
    const allowed = authorizedRoles(entry.operationClass);
    return allowed.some((role) => effectiveRoles.has(role));
  });

  return (
    <aside
      aria-label="Primary navigation"
      className="hidden w-56 shrink-0 border-e border-border-default bg-surface-elevated p-4 text-foreground-muted md:flex md:flex-col"
    >
      <p className="text-xs uppercase tracking-wide text-foreground-subtle">Navigation</p>
      {visibleEntries.length === 0 ? (
        <p className="mt-2 text-sm">No items available for your role.</p>
      ) : (
        <nav className="mt-2 flex flex-col gap-1" data-testid="primary-navigation">
          {visibleEntries.map((entry) => {
            const active = entry.matchPrefix
              ? pathname === entry.href || pathname.startsWith(`${entry.href}/`)
              : pathname === entry.href;
            const Icon = entry.icon;
            return (
              <Link
                key={entry.href}
                href={entry.href}
                aria-current={active ? "page" : undefined}
                data-testid={`nav-${entry.href.replace(/^\//, "")}`}
                className={cn(
                  "flex items-center gap-2 rounded px-2 py-1 text-sm transition-colors",
                  active
                    ? "bg-accent-primary/10 text-foreground-default"
                    : "text-foreground-muted hover:text-foreground-default",
                )}
              >
                {Icon ? <Icon aria-hidden="true" className="size-4 shrink-0" /> : null}
                <span>{entry.label}</span>
              </Link>
            );
          })}
        </nav>
      )}
    </aside>
  );
}

export function NavigationShell({ children, userMenu }: NavigationShellProps) {
  return (
    <AppShell
      sidebar={<NavigationSidebar />}
      topBar={<NavigationHeader userMenu={userMenu} />}
      main={<div className="mx-auto w-full max-w-6xl p-6">{children}</div>}
      footer={<Footer />}
    />
  );
}
