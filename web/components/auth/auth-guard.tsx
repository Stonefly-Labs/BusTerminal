"use client";

import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import { useEffect, type ReactNode } from "react";

import { API_SCOPE_REQUEST } from "@/lib/auth/scopes";

interface AuthGuardProps {
  readonly children: ReactNode;
  readonly fallback?: ReactNode;
}

export function AuthGuard({ children, fallback }: AuthGuardProps) {
  const isAuthenticated = useIsAuthenticated();
  const { instance, inProgress } = useMsal();

  useEffect(() => {
    if (!isAuthenticated && inProgress === "none") {
      void instance.loginRedirect({ scopes: [...API_SCOPE_REQUEST.scopes] }).catch(() => {
        // MSAL surfaces interactive errors via its event API; rethrowing here
        // would crash the render. Allow a subsequent navigation to retry.
      });
    }
  }, [isAuthenticated, inProgress, instance]);

  if (!isAuthenticated) {
    return (
      <>
        {fallback ?? (
          <div aria-busy="true" aria-live="polite" data-testid="auth-guard-pending" />
        )}
      </>
    );
  }

  return <>{children}</>;
}
