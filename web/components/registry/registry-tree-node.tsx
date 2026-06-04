"use client";

/**
 * Spec 006 / T086. Single explorer tree node. Composed by registry-explorer-tree.
 * Visual cues: icon per entityType, FQN as tooltip, status badge for non-Active.
 */

import { ChevronRight, ChevronDown, Layers, Inbox, Megaphone, Bell, Filter } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import Link from "next/link";
import type { Route } from "next";

import { cn } from "@/lib/design-system/cn";
import type { RegistryEntity, RegistryEntityType } from "@/lib/registry/types";

import { RegistryStatusBadge } from "./registry-status-badge";

const ENTITY_ICONS: Record<RegistryEntityType, LucideIcon> = {
  Namespace: Layers,
  Queue: Inbox,
  Topic: Megaphone,
  Subscription: Bell,
  Rule: Filter,
};

interface RegistryTreeNodeProps {
  readonly entity: RegistryEntity;
  readonly depth: number;
  readonly expanded: boolean;
  readonly hasChildren: boolean;
  readonly selected: boolean;
  readonly onToggle: (id: string) => void;
  readonly children?: React.ReactNode;
}

export function RegistryTreeNode({
  entity,
  depth,
  expanded,
  hasChildren,
  selected,
  onToggle,
  children,
}: RegistryTreeNodeProps) {
  const Icon = ENTITY_ICONS[entity.entityType];
  const detailHref = `/registry/${entity.entityType}/${entity.id}` as Route;

  return (
    <div role="treeitem" aria-expanded={hasChildren ? expanded : undefined} data-testid="registry-tree-node">
      <div
        className={cn(
          "group flex items-center gap-2 rounded-md px-2 py-1.5 text-sm",
          "hover:bg-interactive-hover",
          selected && "bg-interactive-selected",
        )}
        style={{ paddingInlineStart: `${depth * 16 + 8}px` }}
      >
        {hasChildren ? (
          <button
            type="button"
            onClick={() => onToggle(entity.id)}
            className="rounded p-0.5 text-foreground-muted hover:bg-interactive-hover"
            aria-label={expanded ? "Collapse" : "Expand"}
            data-testid={`tree-toggle-${entity.id}`}
          >
            {expanded ? (
              <ChevronDown className="size-4" aria-hidden="true" />
            ) : (
              <ChevronRight className="size-4 rtl:rotate-180" aria-hidden="true" />
            )}
          </button>
        ) : (
          <span className="inline-block size-5" aria-hidden="true" />
        )}
        <Icon className="size-4 text-foreground-muted" aria-hidden="true" />
        <Link
          href={detailHref}
          className="flex-1 truncate text-foreground-default hover:underline"
          title={entity.fullyQualifiedName ?? entity.name}
        >
          {entity.name}
        </Link>
        {entity.status === "Deprecated" ? (
          <RegistryStatusBadge status="Deprecated" className="ms-auto" />
        ) : null}
      </div>
      {expanded && children ? <div role="group">{children}</div> : null}
    </div>
  );
}
