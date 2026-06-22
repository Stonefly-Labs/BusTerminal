"use client";

import type { PlatformRole } from "@/lib/auth/role-permission-matrix";

import { useResolvedRoleContext } from "@/components/auth/role-context";

/**
 * Effective platform roles for the current user.
 *
 * Sourced from the `/whoami`-backed RoleContext (populated by the authenticated
 * layout) — NOT from the MSAL ID-token claims. BusTerminal app roles are
 * assigned on the backend API app registration, so they appear in the API
 * *access* token (which `/whoami` validates server-side) and never in the SPA's
 * ID token. Reading ID-token claims silently produced an empty role set under
 * real Entra auth, hiding every role-gated affordance (onboard, discover, edit,
 * lifecycle, …) even for users who hold the role. Mock-auth E2E previously hid
 * this because the mock injects roles into `idTokenClaims`; the RoleContext is
 * correct in both modes because the layout's `/whoami` carries the mock-roles
 * header in mock mode and the real access token under Entra auth.
 */
export function useRoles(): ReadonlySet<PlatformRole> {
  return useResolvedRoleContext().effectiveRoles;
}
