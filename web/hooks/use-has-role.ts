"use client";

import type { PlatformRole } from "@/lib/auth/role-permission-matrix";

import { useRoles } from "./use-roles";

export function useHasRole(role: PlatformRole | readonly PlatformRole[]): boolean {
  const roles = useRoles();
  const required: readonly PlatformRole[] = Array.isArray(role) ? role : [role as PlatformRole];
  return required.some((r) => roles.has(r));
}
