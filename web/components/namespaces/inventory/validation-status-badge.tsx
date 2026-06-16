/**
 * Spec 008 / T106 / US2 / FR-041. Validation status pill — Healthy /
 * Degraded / Unhealthy. Color + icon + text together (NEVER color alone per
 * FR-041).
 *
 * Reused by inventory, details header, and validation panel. RSC-safe.
 */

import { CircleAlert, CircleCheck, CircleSlash } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import type { ValidationStatus } from "@/lib/namespaces/types";

interface ValidationStatusBadgeProps {
  readonly status: ValidationStatus | null | undefined;
}

export function ValidationStatusBadge({ status }: ValidationStatusBadgeProps) {
  switch (status) {
    case "Healthy":
      return (
        <Badge intent="success" icon={CircleCheck} aria-label="Validation status: Healthy">
          Healthy
        </Badge>
      );
    case "Degraded":
      return (
        <Badge intent="warning" icon={CircleAlert} aria-label="Validation status: Degraded">
          Degraded
        </Badge>
      );
    case "Unhealthy":
      return (
        <Badge intent="error" icon={CircleSlash} aria-label="Validation status: Unhealthy">
          Unhealthy
        </Badge>
      );
    default:
      return (
        <Badge intent="outline" aria-label="Validation status: Unknown">
          —
        </Badge>
      );
  }
}
