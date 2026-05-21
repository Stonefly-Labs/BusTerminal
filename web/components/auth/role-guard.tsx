"use client";

import type { ReactNode } from "react";

import { useHasRole } from "@/hooks/use-has-role";
import { authorizedRoles, type OperationClass } from "@/lib/auth/role-permission-matrix";

interface RoleGuardProps {
  readonly operationClass: OperationClass;
  readonly children: ReactNode;
  readonly fallback?: ReactNode;
}

export function RoleGuard({ operationClass, children, fallback = null }: RoleGuardProps) {
  const authorized = useHasRole(authorizedRoles(operationClass));
  if (!authorized) {
    return <>{fallback}</>;
  }
  return <>{children}</>;
}
