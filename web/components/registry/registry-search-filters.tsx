"use client";

/**
 * Spec 006 / T114. Chip-style filter row for the registry search route.
 * URL-synced — each chip toggle updates the `?entityType` / `?environment`
 * / `?status` / `?tagKey` query params so the search results table can
 * react via TanStack Query.
 */

import { useRouter, usePathname, useSearchParams } from "next/navigation";
import { useTransition } from "react";
import { X } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/design-system/cn";
import type { RegistryEntityType } from "@/lib/registry/types";

const ENTITY_TYPES: ReadonlyArray<RegistryEntityType> = [
  "Namespace",
  "Queue",
  "Topic",
  "Subscription",
  "Rule",
];
const STATUS_VALUES = ["Active", "Deprecated"] as const;

interface RegistrySearchFiltersProps {
  readonly className?: string | undefined;
  readonly environments?: readonly string[] | undefined;
}

export function RegistrySearchFilters({
  className,
  environments = [],
}: RegistrySearchFiltersProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [, startTransition] = useTransition();

  const setParam = (name: string, value: string | undefined) => {
    const next = new URLSearchParams(searchParams.toString());
    if (value === undefined || value === "") {
      next.delete(name);
    } else {
      next.set(name, value);
    }
    // Reset page when filters change.
    next.delete("page");
    startTransition(() => {
      router.replace(`${pathname}?${next.toString()}` as never);
    });
  };

  const activeType = searchParams.get("entityType") ?? "";
  const activeEnv = searchParams.get("environment") ?? "";
  const activeStatus = searchParams.get("status") ?? "";
  const tagKey = searchParams.get("tagKey") ?? "";

  const hasAnyFilter = activeType || activeEnv || activeStatus || tagKey;

  return (
    <div
      data-testid="registry-search-filters"
      className={cn("flex flex-wrap items-center gap-2", className)}
    >
      <FilterGroup label="Type">
        {ENTITY_TYPES.map((type) => (
          <Chip
            key={type}
            label={type}
            active={activeType === type}
            onClick={() => setParam("entityType", activeType === type ? undefined : type)}
          />
        ))}
      </FilterGroup>

      {environments.length > 0 ? (
        <FilterGroup label="Environment">
          {environments.map((env) => (
            <Chip
              key={env}
              label={env}
              active={activeEnv === env}
              onClick={() => setParam("environment", activeEnv === env ? undefined : env)}
            />
          ))}
        </FilterGroup>
      ) : null}

      <FilterGroup label="Status">
        {STATUS_VALUES.map((status) => (
          <Chip
            key={status}
            label={status}
            active={activeStatus === status}
            onClick={() => setParam("status", activeStatus === status ? undefined : status)}
          />
        ))}
      </FilterGroup>

      {hasAnyFilter ? (
        <Button
          intent="ghost"
          size="sm"
          onClick={() => {
            const next = new URLSearchParams(searchParams.toString());
            for (const k of ["entityType", "environment", "status", "tagKey", "tagValue", "page"]) {
              next.delete(k);
            }
            startTransition(() => {
              router.replace(`${pathname}?${next.toString()}` as never);
            });
          }}
          data-testid="clear-filters"
          className="ms-auto"
        >
          <X className="me-1 size-4" aria-hidden="true" />
          Clear filters
        </Button>
      ) : null}
    </div>
  );
}

function FilterGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-1">
      <span className="me-1 text-xs font-medium uppercase tracking-wide text-foreground-subtle">
        {label}:
      </span>
      <div className="flex flex-wrap items-center gap-1">{children}</div>
    </div>
  );
}

function Chip({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      data-active={active}
      className={cn(
        "rounded-full border px-2.5 py-0.5 text-xs font-medium transition-colors",
        active
          ? "border-accent-primary bg-accent-primary text-accent-primary-foreground"
          : "border-border-default bg-surface-canvas text-foreground-default hover:bg-interactive-hover",
      )}
    >
      {label}
    </button>
  );
}
