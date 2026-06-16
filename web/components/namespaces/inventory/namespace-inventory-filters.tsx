"use client";

/**
 * Spec 008 / T107 / US2 / FR-017–FR-020. Chip-based filter row for the
 * namespace inventory. URL-synced — each chip toggle updates the corresponding
 * search param so the page renders + shareable links work.
 *
 * Supported facets: environment, lifecycleStatus, validationStatus, tagKey,
 * tagValue, includeArchived.
 */

import { useRouter, usePathname, useSearchParams } from "next/navigation";
import { useState, useTransition } from "react";
import { Search, X } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/design-system/cn";
import type { LifecycleStatus, ValidationStatus } from "@/lib/namespaces/types";

const LIFECYCLE_VALUES: ReadonlyArray<LifecycleStatus> = ["Active", "Disabled", "Archived"];
const VALIDATION_VALUES: ReadonlyArray<ValidationStatus> = ["Healthy", "Degraded", "Unhealthy"];

interface NamespaceInventoryFiltersProps {
  readonly className?: string | undefined;
  readonly environments?: readonly string[] | undefined;
}

export function NamespaceInventoryFilters({
  className,
  environments = [],
}: NamespaceInventoryFiltersProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [, startTransition] = useTransition();

  const [searchInput, setSearchInput] = useState<string>(searchParams.get("q") ?? "");

  const activeEnv = searchParams.get("environment") ?? "";
  const activeLifecycle = searchParams.get("lifecycleStatus") ?? "";
  const activeValidation = searchParams.get("validationStatus") ?? "";
  const tagKey = searchParams.get("tagKey") ?? "";
  const includeArchived = searchParams.get("includeArchived") === "true";

  const setParam = (name: string, value: string | undefined) => {
    const next = new URLSearchParams(searchParams.toString());
    if (value === undefined || value === "") {
      next.delete(name);
    } else {
      next.set(name, value);
    }
    next.delete("continuationToken");
    startTransition(() => {
      router.replace(`${pathname}?${next.toString()}` as never);
    });
  };

  const commitSearch = () => {
    setParam("q", searchInput.trim() === "" ? undefined : searchInput.trim());
  };

  const hasAnyFilter = activeEnv || activeLifecycle || activeValidation || tagKey || includeArchived || searchInput;

  return (
    <div
      data-testid="namespace-inventory-filters"
      className={cn("flex flex-wrap items-center gap-2", className)}
    >
      <div className="flex items-center gap-2">
        <Search className="size-4 text-foreground-muted" aria-hidden="true" />
        <Input
          aria-label="Search by display name or business unit"
          placeholder="Search by name or business unit…"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          onBlur={commitSearch}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              commitSearch();
            }
          }}
          className="w-64"
        />
      </div>

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

      <FilterGroup label="Lifecycle">
        {LIFECYCLE_VALUES.map((s) => (
          <Chip
            key={s}
            label={s}
            active={activeLifecycle === s}
            onClick={() => setParam("lifecycleStatus", activeLifecycle === s ? undefined : s)}
          />
        ))}
      </FilterGroup>

      <FilterGroup label="Validation">
        {VALIDATION_VALUES.map((s) => (
          <Chip
            key={s}
            label={s}
            active={activeValidation === s}
            onClick={() => setParam("validationStatus", activeValidation === s ? undefined : s)}
          />
        ))}
      </FilterGroup>

      <FilterGroup label="Archived">
        <Chip
          label="Include archived"
          active={includeArchived}
          onClick={() => setParam("includeArchived", includeArchived ? undefined : "true")}
        />
      </FilterGroup>

      {hasAnyFilter ? (
        <Button
          intent="ghost"
          size="sm"
          onClick={() => {
            const next = new URLSearchParams(searchParams.toString());
            for (const k of [
              "environment",
              "lifecycleStatus",
              "validationStatus",
              "tagKey",
              "tagValue",
              "includeArchived",
              "q",
              "continuationToken",
            ]) {
              next.delete(k);
            }
            setSearchInput("");
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
