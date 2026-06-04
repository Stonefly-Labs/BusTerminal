/**
 * Spec 006 / T090 / FR-013a + FR-047. Visual badge for the two-state
 * registry lifecycle. Colour AND icon AND label so the cue is not
 * conveyed by colour alone (constitution accessibility rule).
 */

import { CheckCircle2, Clock4 } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/design-system/cn";
import type { RegistryEntityStatus } from "@/lib/registry/types";

interface RegistryStatusBadgeProps {
  readonly status: RegistryEntityStatus;
  readonly className?: string;
}

export function RegistryStatusBadge({ status, className }: RegistryStatusBadgeProps) {
  const isActive = status === "Active";
  const Icon = isActive ? CheckCircle2 : Clock4;
  const label = isActive ? "Active" : "Deprecated";

  return (
    <Badge
      intent={isActive ? "success" : "warning"}
      icon={Icon}
      data-testid="registry-status-badge"
      data-status={status}
      className={cn("gap-1.5", className)}
    >
      <span>{label}</span>
    </Badge>
  );
}
