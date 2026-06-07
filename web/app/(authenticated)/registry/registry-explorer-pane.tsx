"use client";

/**
 * Spec 006. Client-side wrapper around the explorer tree so the layout
 * (which can stay RSC) can mount the interactive Client Component cleanly.
 * Reads the active environment from the URL query.
 */

import Link from "next/link";
import type { Route } from "next";
import { useSearchParams } from "next/navigation";
import { Plus } from "lucide-react";

import { Button } from "@/components/ui/button";
import { RegistryExplorerTree } from "@/components/registry/registry-explorer-tree";

export function RegistryExplorerPane() {
  const searchParams = useSearchParams();
  const environment = searchParams.get("environment") ?? "";

  return (
    <div className="flex flex-col gap-3" data-testid="registry-explorer-pane">
      <Button asChild intent="primary" size="sm" className="w-full">
        <Link href={"/registry/new/Namespace" as Route}>
          <Plus className="me-1 size-4" aria-hidden="true" />
          New namespace
        </Link>
      </Button>
      {environment ? (
        <RegistryExplorerTree environment={environment} />
      ) : (
        <p className="px-2 text-sm text-foreground-muted">
          Choose an environment to browse the registry.
        </p>
      )}
    </div>
  );
}
