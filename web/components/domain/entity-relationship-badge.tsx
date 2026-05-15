import * as React from "react";
import { ArrowRight } from "lucide-react";

import { cn } from "@/lib/design-system/cn";
import { t } from "@/lib/i18n/strings";

export type EntityRelationshipKind =
  | "forwards"
  | "subscribes"
  | "deadLetters"
  | "publishes"
  | "parentOf";

export interface EntityRelationshipBadgeProps
  extends Omit<React.HTMLAttributes<HTMLSpanElement>, "children"> {
  readonly kind: EntityRelationshipKind;
  readonly from: string;
  readonly to: string;
}

const KIND_KEY = {
  forwards: "domain.relationship.forwards",
  subscribes: "domain.relationship.subscribes",
  deadLetters: "domain.relationship.deadLetters",
  publishes: "domain.relationship.publishes",
  parentOf: "domain.relationship.parentOf",
} as const satisfies Record<
  EntityRelationshipKind,
  | "domain.relationship.forwards"
  | "domain.relationship.subscribes"
  | "domain.relationship.deadLetters"
  | "domain.relationship.publishes"
  | "domain.relationship.parentOf"
>;

/**
 * Compact "FROM → TO" affordance used by topology-aware screens to show the
 * relationship between two entities. The arrow is decorative; the relationship
 * verb is text and is read by assistive technology as part of the composed
 * accessible label.
 */
export const EntityRelationshipBadge = React.forwardRef<
  HTMLSpanElement,
  EntityRelationshipBadgeProps
>(function EntityRelationshipBadge({ kind, from, to, className, ...rest }, ref) {
  const verb = t(KIND_KEY[kind]);
  return (
    <span
      ref={ref}
      role="group"
      aria-label={`${from} — ${verb} ${to}`}
      className={cn(
        "inline-flex items-center gap-1.5 rounded-md border border-border-default bg-surface-muted px-2 py-0.5 text-xs",
        className,
      )}
      {...rest}
    >
      <span className="font-mono text-foreground-default">{from}</span>
      <ArrowRight
        aria-hidden="true"
        className="size-3 shrink-0 text-foreground-muted rtl:-scale-x-100"
      />
      <span className="text-foreground-muted">{verb}</span>
      <span className="font-mono text-foreground-default">{to}</span>
    </span>
  );
});
