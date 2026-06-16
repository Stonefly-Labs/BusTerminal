/**
 * Spec 008 / T105 / US2 / FR-041. Lifecycle status pill — Active / Disabled /
 * Archived. Color + icon + text together (NEVER color alone per FR-041).
 *
 * Reused by both the inventory table and the details header. RSC-safe — pure
 * presentational component with no state.
 */

import { Archive, CircleCheck, CircleOff } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import type { LifecycleStatus } from "@/lib/namespaces/types";

interface LifecycleStatusBadgeProps {
  readonly status: LifecycleStatus | null | undefined;
}

export function LifecycleStatusBadge({ status }: LifecycleStatusBadgeProps) {
  switch (status) {
    case "Active":
      return (
        <Badge intent="success" icon={CircleCheck} aria-label="Lifecycle status: Active">
          Active
        </Badge>
      );
    case "Disabled":
      return (
        <Badge intent="warning" icon={CircleOff} aria-label="Lifecycle status: Disabled">
          Disabled
        </Badge>
      );
    case "Archived":
      return (
        <Badge intent="neutral" icon={Archive} aria-label="Lifecycle status: Archived">
          Archived
        </Badge>
      );
    default:
      return (
        <Badge intent="outline" aria-label="Lifecycle status: Unknown">
          —
        </Badge>
      );
  }
}
