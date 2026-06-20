"use client";

/**
 * Spec 009 / T073 / US2. URL-state-driven multi-select chip group for the
 * `lifecycleStatus` query parameter. Multiple values can be selected and are
 * persisted to the URL as `?lifecycleStatus=Active&lifecycleStatus=Missing`
 * (the convention the backend's `GET /api/entities` expects per
 * contracts/openapi.yaml#searchEntities).
 *
 * Built from primitive shadcn `Button` + `Badge` rather than introducing a
 * second checkbox-group component so the visual treatment matches the
 * existing `registry-search-filters.tsx` chips on the page.
 */

import { useRouter, usePathname, useSearchParams } from "next/navigation";
import { useTransition } from "react";

import { cn } from "@/lib/design-system/cn";
import { LIFECYCLE_STATUSES, type LifecycleStatus } from "@/lib/discovery/schemas";

export const LIFECYCLE_QUERY_KEY = "lifecycleStatus";

interface LifecycleFilterProps {
  readonly className?: string | undefined;
}

export function LifecycleFilter({ className }: LifecycleFilterProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [, startTransition] = useTransition();

  const selected = new Set(searchParams.getAll(LIFECYCLE_QUERY_KEY) as LifecycleStatus[]);

  const toggle = (status: LifecycleStatus) => {
    const next = new URLSearchParams(searchParams.toString());
    next.delete(LIFECYCLE_QUERY_KEY);
    const updated = new Set(selected);
    if (updated.has(status)) {
      updated.delete(status);
    } else {
      updated.add(status);
    }
    for (const value of updated) {
      next.append(LIFECYCLE_QUERY_KEY, value);
    }
    next.delete("page");
    startTransition(() => {
      router.replace(`${pathname}?${next.toString()}` as never);
    });
  };

  return (
    <div
      data-testid="lifecycle-filter"
      role="group"
      aria-label="Filter by lifecycle status"
      className={cn("flex flex-wrap items-center gap-1", className)}
    >
      <span className="me-1 text-xs font-medium uppercase tracking-wide text-foreground-muted">
        Lifecycle:
      </span>
      {LIFECYCLE_STATUSES.map((status) => {
        const active = selected.has(status);
        return (
          <button
            key={status}
            type="button"
            role="checkbox"
            aria-checked={active}
            data-active={active}
            data-value={status}
            onClick={() => toggle(status)}
            className={cn(
              "rounded-full border px-2.5 py-0.5 text-xs font-medium transition-colors",
              active
                ? "border-accent-primary bg-accent-primary text-accent-primary-foreground"
                : "border-border-default bg-surface-canvas text-foreground-default hover:bg-interactive-hover",
            )}
          >
            {status}
          </button>
        );
      })}
    </div>
  );
}
