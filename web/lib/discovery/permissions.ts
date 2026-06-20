/**
 * Spec 009 / T028. Client-side projection of the server-side
 * `RequireEntityMetadataEditor` policy (research §R-15).
 *
 * Use this to pre-gate edit affordances (Edit button, archive action,
 * association add/remove) in the UI. The backend is still the authoritative
 * enforcer — server endpoints re-run the same decision against fresh data,
 * so a client-side bypass cannot mutate.
 */

import type { PublishedEntity } from "./schemas";

export const ADMIN_ROLE = "BusTerminal.Admin" as const;
export const NAMESPACE_ADMIN_ROLE = "BusTerminal.NamespaceAdministrator" as const;

export interface EntityEditRoleContext {
  readonly roles: ReadonlySet<string>;
}

export function canEditEntityMetadata(
  entity: Pick<PublishedEntity, "serviceAssociations">,
  roleContext: EntityEditRoleContext,
  ownedServiceIds: ReadonlySet<string>,
): boolean {
  if (roleContext.roles.has(ADMIN_ROLE)) return true;
  if (roleContext.roles.has(NAMESPACE_ADMIN_ROLE)) return true;
  for (const assoc of entity.serviceAssociations) {
    if (assoc.role === "Owner" && ownedServiceIds.has(assoc.serviceId)) {
      return true;
    }
  }
  return false;
}
