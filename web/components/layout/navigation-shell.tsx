import type { ReactNode } from "react";

import { AppShell } from "@/components/app-shell/app-shell";
import { Footer } from "@/components/app-shell/footer";
import { NavigationHeader } from "@/components/layout/navigation-header";

export interface NavigationShellProps {
  readonly children: ReactNode;
  readonly userMenu?: ReactNode;
}

function NavigationSidebar() {
  return (
    <aside
      aria-label="Primary navigation"
      className="hidden w-56 shrink-0 border-e border-border-default bg-surface-elevated p-4 text-foreground-muted md:flex md:flex-col"
    >
      <p className="text-xs uppercase tracking-wide text-foreground-subtle">Navigation</p>
      <p className="mt-2 text-sm">Navigation will live here.</p>
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
