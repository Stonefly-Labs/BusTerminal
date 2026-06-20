"use client";

/**
 * Spec 006 / T085. The left-pane explorer tree. Lazily loads children on
 * expand via TanStack Query so the operator can drill down without
 * over-fetching the partition. Top-level shows Namespaces in the current
 * environment; expanding a Namespace fetches its Queues + Topics; expanding
 * a Topic fetches its Subscriptions; expanding a Subscription fetches its
 * Rules.
 */

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";

import {
  listEntities,
  resolveApiOptions,
  type RegistryApiOptions,
} from "@/lib/registry/api";
import { registryQueryKeys } from "@/lib/registry/query-keys";
import type { RegistryEntity, RegistryEntityType } from "@/lib/registry/types";
import { useAcquireToken } from "@/hooks/use-acquire-token";

import { RegistryTreeNode } from "./registry-tree-node";
import { RegistryEmptyState } from "./registry-empty-state";

interface RegistryExplorerTreeProps {
  readonly environment: string;
  readonly selectedId?: string | undefined;
  readonly apiOptions?: RegistryApiOptions | undefined;
}

const CHILD_TYPES: Partial<Record<RegistryEntityType, RegistryEntityType[]>> = {
  Namespace: ["Queue", "Topic"],
  Topic: ["Subscription"],
  Subscription: ["Rule"],
};

export function RegistryExplorerTree({
  environment,
  selectedId,
  apiOptions,
}: RegistryExplorerTreeProps) {
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set());

  const toggle = (id: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const getToken = useAcquireToken();

  const rootsQuery = useQuery({
    queryKey: registryQueryKeys.entities.list({
      environment,
      entityType: "Namespace",
    }),
    // Resolve a bearer token unless the caller supplied one — the registry
    // API client never acquires its own (see registry-env-switcher for the
    // full rationale).
    queryFn: async () =>
      listEntities(
        { environment, entityType: "Namespace", pageSize: 200 },
        await resolveApiOptions(apiOptions, getToken),
      ),
    enabled: !!environment,
  });

  if (rootsQuery.isPending) {
    return (
      <div className="px-2 py-3 text-sm text-foreground-muted" data-testid="registry-explorer-loading">
        Loading namespaces…
      </div>
    );
  }
  if (rootsQuery.isError) {
    return (
      <RegistryEmptyState
        variant="unavailable"
        title="Registry unavailable"
        description="Could not load the registry namespaces. Try again later."
      />
    );
  }

  const roots = rootsQuery.data?.items ?? [];
  if (roots.length === 0) {
    return (
      <RegistryEmptyState
        variant="no-data"
        title="No namespaces yet"
        description={`No registry entities are present in the '${environment}' environment. Use New to register your first namespace.`}
      />
    );
  }

  return (
    <div role="tree" aria-label="Registry explorer" data-testid="registry-explorer-tree" className="flex flex-col gap-0.5">
      {roots.map((root) => (
        <TreeBranch
          key={root.id}
          entity={root}
          depth={0}
          environment={environment}
          selectedId={selectedId}
          expanded={expanded}
          onToggle={toggle}
          apiOptions={apiOptions}
        />
      ))}
    </div>
  );
}

interface TreeBranchProps {
  readonly entity: RegistryEntity;
  readonly depth: number;
  readonly environment: string;
  readonly selectedId?: string | undefined;
  readonly expanded: ReadonlySet<string>;
  readonly onToggle: (id: string) => void;
  readonly apiOptions?: RegistryApiOptions | undefined;
}

function TreeBranch({
  entity,
  depth,
  environment,
  selectedId,
  expanded,
  onToggle,
  apiOptions,
}: TreeBranchProps) {
  const childTypes = CHILD_TYPES[entity.entityType] ?? [];
  const canExpand = childTypes.length > 0;
  const isExpanded = expanded.has(entity.id);
  const getToken = useAcquireToken();

  const childrenQuery = useQuery({
    queryKey: registryQueryKeys.entities.list({ environment, parentId: entity.id }),
    queryFn: async () =>
      listEntities(
        { environment, parentId: entity.id, pageSize: 200 },
        await resolveApiOptions(apiOptions, getToken),
      ),
    enabled: canExpand && isExpanded,
  });

  const children = childrenQuery.data?.items ?? [];

  return (
    <RegistryTreeNode
      entity={entity}
      depth={depth}
      expanded={isExpanded}
      hasChildren={canExpand}
      selected={selectedId === entity.id}
      onToggle={onToggle}
    >
      {childrenQuery.isPending && isExpanded ? (
        <div className="ps-10 py-1 text-xs text-foreground-muted">Loading children…</div>
      ) : null}
      {children.map((child) => (
        <TreeBranch
          key={child.id}
          entity={child}
          depth={depth + 1}
          environment={environment}
          selectedId={selectedId}
          expanded={expanded}
          onToggle={onToggle}
          apiOptions={apiOptions}
        />
      ))}
    </RegistryTreeNode>
  );
}
