"use client";

import { useMemo } from "react";

import { getRoles } from "@/lib/auth/claims";
import { parseRole, type PlatformRole } from "@/lib/auth/role-permission-matrix";

import { useCurrentUser } from "./use-current-user";

export function useRoles(): ReadonlySet<PlatformRole> {
  const account = useCurrentUser();
  return useMemo(() => {
    const roles = new Set<PlatformRole>();
    for (const value of getRoles(account)) {
      const parsed = parseRole(value);
      if (parsed !== null) {
        roles.add(parsed);
      }
    }
    return roles;
  }, [account]);
}
