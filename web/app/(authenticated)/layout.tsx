"use client";

import type { Route } from "next";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState, type ReactNode } from "react";

import { AuthGuard } from "@/components/auth/auth-guard";
import { RoleContextProvider } from "@/components/auth/role-context";
import { NavigationShell } from "@/components/layout/navigation-shell";
import { UserMenu } from "@/components/layout/user-menu";
import { apiGet } from "@/lib/api-client";
import { parseRole, type PlatformRole } from "@/lib/auth/role-permission-matrix";

interface WhoAmIResponse {
  principal: {
    oid: string;
    displayName: string | null;
    preferredUsername: string | null;
    tenantId: string;
    callerType: "Human" | "Workload";
    effectiveRoles: readonly string[];
  };
}

export default function AuthenticatedLayout({ children }: { readonly children: ReactNode }) {
  return (
    <AuthGuard>
      <ResolvedRoleScope>
        <Shell>{children}</Shell>
      </ResolvedRoleScope>
    </AuthGuard>
  );
}

function Shell({ children }: { readonly children: ReactNode }) {
  return <NavigationShell userMenu={<UserMenu />}>{children}</NavigationShell>;
}

function ResolvedRoleScope({ children }: { readonly children: ReactNode }) {
  const router = useRouter();
  const [effectiveRoles, setEffectiveRoles] = useState<ReadonlySet<PlatformRole>>(new Set());
  const [resolved, setResolved] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const result = await apiGet<WhoAmIResponse>("/whoami");
      if (cancelled) return;
      if (!result.ok) {
        setResolved(true);
        return;
      }
      const parsed = new Set<PlatformRole>();
      for (const raw of result.data.principal.effectiveRoles) {
        const role = parseRole(raw);
        if (role) parsed.add(role);
      }
      setEffectiveRoles(parsed);
      setResolved(true);
      if (parsed.size === 0) {
        router.replace("/no-access" as Route);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [router]);

  const ctx = useMemo(() => ({ effectiveRoles, resolved }), [effectiveRoles, resolved]);

  if (!resolved) {
    return (
      <div
        aria-busy="true"
        aria-live="polite"
        data-testid="authenticated-layout-pending"
        className="flex min-h-screen items-center justify-center bg-surface-canvas"
      >
        <span className="text-sm text-foreground-muted">Resolving access…</span>
      </div>
    );
  }

  if (effectiveRoles.size === 0) {
    return (
      <div
        aria-busy="true"
        aria-live="polite"
        data-testid="authenticated-layout-redirecting"
        className="flex min-h-screen items-center justify-center bg-surface-canvas"
      >
        <span className="text-sm text-foreground-muted">Redirecting…</span>
      </div>
    );
  }

  return <RoleContextProvider value={ctx}>{children}</RoleContextProvider>;
}
