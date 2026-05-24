"use client";

import { createContext, useContext, type ReactNode } from "react";

import type { PlatformRole } from "@/lib/auth/role-permission-matrix";

export interface ResolvedRoleContext {
  readonly effectiveRoles: ReadonlySet<PlatformRole>;
  /** True once `/whoami` has resolved (success or failure). */
  readonly resolved: boolean;
}

const RoleContext = createContext<ResolvedRoleContext>({
  effectiveRoles: new Set(),
  resolved: false,
});

export interface RoleContextProviderProps {
  readonly value: ResolvedRoleContext;
  readonly children: ReactNode;
}

export function RoleContextProvider({ value, children }: RoleContextProviderProps) {
  return <RoleContext.Provider value={value}>{children}</RoleContext.Provider>;
}

export function useResolvedRoleContext(): ResolvedRoleContext {
  return useContext(RoleContext);
}
