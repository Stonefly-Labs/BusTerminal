"use client";

/**
 * Spec 009 / T074 / US2. Combines a service-id input (free-text for v1 —
 * future spec wires the picker per R-15 service lookup) with a role
 * narrowing checkbox group. State is persisted to URL params:
 *   ?associatedServiceId=<id>
 *   ?associationRole=Owner&associationRole=Consumer
 *
 * The combobox-with-popover shape the task originally specified depends on
 * a service catalog endpoint that isn't part of Spec 009 — we ship a typed
 * text input now and a Phase 6 / future-spec patch can swap it for the
 * popover + cmdk picker without changing the URL state contract.
 */

import { useRouter, usePathname, useSearchParams } from "next/navigation";
import { useTransition } from "react";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/design-system/cn";
import {
  ENTITY_SERVICE_ROLES,
  type EntityServiceRole,
} from "@/lib/discovery/schemas";

export const ASSOCIATED_SERVICE_QUERY_KEY = "associatedServiceId";
export const ASSOCIATION_ROLE_QUERY_KEY = "associationRole";

interface ServiceAssociationFilterProps {
  readonly className?: string | undefined;
}

export function ServiceAssociationFilter({ className }: ServiceAssociationFilterProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [, startTransition] = useTransition();

  const serviceId = searchParams.get(ASSOCIATED_SERVICE_QUERY_KEY) ?? "";
  const roles = new Set(searchParams.getAll(ASSOCIATION_ROLE_QUERY_KEY) as EntityServiceRole[]);

  const update = (
    name: typeof ASSOCIATED_SERVICE_QUERY_KEY | typeof ASSOCIATION_ROLE_QUERY_KEY,
    values: string[],
  ) => {
    const next = new URLSearchParams(searchParams.toString());
    next.delete(name);
    for (const v of values) {
      if (v.length > 0) next.append(name, v);
    }
    next.delete("page");
    startTransition(() => {
      router.replace(`${pathname}?${next.toString()}` as never);
    });
  };

  const onServiceIdChange = (value: string) => {
    update(ASSOCIATED_SERVICE_QUERY_KEY, value.trim().length === 0 ? [] : [value.trim()]);
  };

  const toggleRole = (role: EntityServiceRole) => {
    const updated = new Set(roles);
    if (updated.has(role)) updated.delete(role);
    else updated.add(role);
    update(ASSOCIATION_ROLE_QUERY_KEY, [...updated]);
  };

  return (
    <div
      data-testid="service-association-filter"
      role="group"
      aria-label="Filter by service association"
      className={cn("flex flex-col gap-2 sm:flex-row sm:items-end", className)}
    >
      <div className="flex flex-col gap-1">
        <Label htmlFor="associated-service-id" className="text-xs uppercase tracking-wide">
          Service ID
        </Label>
        <Input
          id="associated-service-id"
          data-testid="associated-service-input"
          value={serviceId}
          placeholder="svc_…"
          onChange={(event) => onServiceIdChange(event.currentTarget.value)}
          className="w-56"
        />
      </div>
      <fieldset className="flex flex-wrap items-center gap-1">
        <legend className="sr-only">Narrow by association role</legend>
        <span className="me-1 text-xs font-medium uppercase tracking-wide text-foreground-subtle">
          Roles:
        </span>
        {ENTITY_SERVICE_ROLES.map((role) => {
          const active = roles.has(role);
          return (
            <button
              key={role}
              type="button"
              role="checkbox"
              aria-checked={active}
              data-active={active}
              data-value={role}
              onClick={() => toggleRole(role)}
              disabled={serviceId.length === 0}
              className={cn(
                "rounded-full border px-2.5 py-0.5 text-xs font-medium transition-colors",
                active
                  ? "border-accent-primary bg-accent-primary text-accent-primary-foreground"
                  : "border-border-default bg-surface-canvas text-foreground-default hover:bg-interactive-hover",
                "disabled:pointer-events-none disabled:opacity-50",
              )}
            >
              {role}
            </button>
          );
        })}
      </fieldset>
    </div>
  );
}
