/**
 * Spec 008 / T053. Lifecycle transition predicates + UI error-message map.
 * Mirrors data-model.md §2 LifecycleStatus transition table:
 *
 *   Active   ⇄ Disabled
 *   Active   → Archived
 *   Disabled → Archived
 *   Archived → Disabled (restore)
 *
 * Predicates return `true` when the action is permitted given the current
 * status. The lifecycle dialog uses these to enable/disable the action
 * buttons; the backend LifecycleTransitionValidator enforces the same table
 * as the authoritative check.
 */

import type { LifecycleAction, LifecycleStatus } from "./schemas";

export function canDisable(status: LifecycleStatus): boolean {
  return status === "Active";
}

export function canEnable(status: LifecycleStatus): boolean {
  return status === "Disabled";
}

export function canArchive(status: LifecycleStatus): boolean {
  return status === "Active" || status === "Disabled";
}

export function canRestore(status: LifecycleStatus): boolean {
  return status === "Archived";
}

export function isActionPermitted(status: LifecycleStatus, action: LifecycleAction): boolean {
  switch (action) {
    case "disable":
      return canDisable(status);
    case "enable":
      return canEnable(status);
    case "archive":
      return canArchive(status);
    case "restore":
      return canRestore(status);
  }
}

export function permittedActionsFor(status: LifecycleStatus): readonly LifecycleAction[] {
  const candidates: LifecycleAction[] = ["disable", "enable", "archive", "restore"];
  return candidates.filter((action) => isActionPermitted(status, action));
}

export function invalidTransitionMessage(
  status: LifecycleStatus,
  action: LifecycleAction,
): string {
  return `Cannot ${action} a namespace in the ${status} lifecycle state.`;
}

export function requiresReason(action: LifecycleAction): boolean {
  return action !== "enable";
}
