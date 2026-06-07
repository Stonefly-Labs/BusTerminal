/**
 * Spec 006 / T103. Shared empty-state component for the registry routes.
 * Distinguishes "no results" from "feature unavailable" via the variant prop
 * so the explorer / search / audit panels can each pick the right message.
 */

import type { ReactNode } from "react";
import { Inbox, AlertCircle, SearchX } from "lucide-react";

import { cn } from "@/lib/design-system/cn";

export type RegistryEmptyVariant = "no-data" | "no-results" | "unavailable";

interface RegistryEmptyStateProps {
  readonly variant?: RegistryEmptyVariant;
  readonly title: string;
  readonly description?: string;
  readonly action?: ReactNode;
  readonly className?: string;
}

const ICONS: Record<RegistryEmptyVariant, typeof Inbox> = {
  "no-data": Inbox,
  "no-results": SearchX,
  unavailable: AlertCircle,
};

export function RegistryEmptyState({
  variant = "no-data",
  title,
  description,
  action,
  className,
}: RegistryEmptyStateProps) {
  const Icon = ICONS[variant];
  return (
    <div
      data-testid="registry-empty-state"
      data-variant={variant}
      className={cn(
        "flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border-default bg-surface-canvas p-8 text-center",
        className,
      )}
    >
      <Icon aria-hidden="true" className="size-8 text-foreground-subtle" />
      <h3 className="text-base font-semibold text-foreground-default">{title}</h3>
      {description ? (
        <p className="max-w-md text-sm text-foreground-muted">{description}</p>
      ) : null}
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}
