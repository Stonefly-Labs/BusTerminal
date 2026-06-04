/**
 * Spec 006 / T083. Two-pane registry shell:
 *   - Left pane: explorer tree (Client Component, lazy-loads on expand)
 *   - Right pane: outlet for sub-routes (page / detail / new / edit)
 *
 * The environment switcher is mounted at the top — its selection propagates
 * via URL query so RSC pages below see it via searchParams.
 */

import type { ReactNode } from "react";

import { RegistryEnvSwitcher } from "@/components/registry/registry-env-switcher";
import { RegistryGlobalSearchTrigger } from "@/components/registry/registry-global-search-trigger";

import { RegistryExplorerPane } from "./registry-explorer-pane";

export default function RegistryLayout({ children }: { readonly children: ReactNode }) {
  return (
    <div data-testid="registry-layout" className="grid h-full min-h-[calc(100vh-4rem)] grid-rows-[auto_1fr]">
      <header className="flex items-center justify-between gap-3 border-b border-border-default bg-surface-elevated px-4 py-3">
        <h2 className="text-lg font-semibold text-foreground-default">Service Bus Registry</h2>
        <div className="flex items-center gap-3">
          <RegistryGlobalSearchTrigger placement="registry" />
          <RegistryEnvSwitcher />
        </div>
      </header>
      <div className="grid grid-cols-1 md:grid-cols-[20rem_1fr] gap-0">
        <aside className="border-e border-border-default bg-surface-canvas p-3 overflow-y-auto">
          <RegistryExplorerPane />
        </aside>
        <main className="p-6 overflow-y-auto">{children}</main>
      </div>
    </div>
  );
}
