/**
 * Spec 008 / T104 / FR-011. Server-side ownership display-name resolver.
 *
 * v1 implementation defaults to the captured `displayNameSnapshot` — this
 * matches the FR-011 contract: "fall back to the snapshot when Graph
 * re-resolution fails". A future tightening can call Microsoft Graph here to
 * get the current display name, then keep the snapshot for the unresolvable
 * case (transient Graph failures, deleted principals).
 *
 * RSC-safe: pure function, no fetch yet.
 */

import type { OwnershipAssignment, OwnershipBlock } from "./schemas";

export interface ResolvedOwnershipAssignment extends OwnershipAssignment {
  /** Display name to render. Equals `displayNameSnapshot` until Graph
   *  re-resolution lands; the UI hint flag below tells the panel whether the
   *  rendered value is stale (snapshot only). */
  readonly displayNameResolved: string;
  /** When true, the rendered display name is the snapshot only (Graph
   *  re-resolution failed or was skipped). The UI surfaces a subtle hint
   *  per FR-011. */
  readonly displayNameIsSnapshotOnly: boolean;
}

export interface ResolvedOwnershipBlock {
  readonly primaryOwner: ResolvedOwnershipAssignment;
  readonly secondaryOwners: ResolvedOwnershipAssignment[];
  readonly technicalStewards: ResolvedOwnershipAssignment[];
  readonly supportContacts: ResolvedOwnershipAssignment[];
}

function resolveAssignment(a: OwnershipAssignment): ResolvedOwnershipAssignment {
  return {
    ...a,
    displayNameResolved: a.displayNameSnapshot,
    displayNameIsSnapshotOnly: true,
  };
}

export function resolveOwnershipBlock(block: OwnershipBlock): ResolvedOwnershipBlock {
  return {
    primaryOwner: resolveAssignment(block.primaryOwner),
    secondaryOwners: (block.secondaryOwners ?? []).map(resolveAssignment),
    technicalStewards: (block.technicalStewards ?? []).map(resolveAssignment),
    supportContacts: (block.supportContacts ?? []).map(resolveAssignment),
  };
}
