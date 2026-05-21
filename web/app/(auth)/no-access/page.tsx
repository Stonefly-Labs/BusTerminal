"use client";

import { useCallback } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useCurrentUser } from "@/hooks/use-current-user";
import { getName, getOid, getPreferredUsername } from "@/lib/auth/claims";
import { msalReady, pca } from "@/lib/auth/msal-instance";

/**
 * No-platform-role experience (SC-008). Reached when the caller is signed in
 * but `effectiveRoles` is empty — the layout redirects here. The page shows
 * the user's display name, oid, and a request-access instruction so the user
 * can contact a tenant admin without inspecting their token.
 */
export default function NoAccessPage() {
  const account = useCurrentUser();
  const displayName = getName(account) ?? "—";
  const oid = getOid(account) ?? "—";
  const upn = getPreferredUsername(account);

  const handleSignOut = useCallback(() => {
    void msalReady
      .then(() => {
        const active = pca.getActiveAccount() ?? pca.getAllAccounts()[0] ?? null;
        return pca.logoutRedirect({
          account: active,
          postLogoutRedirectUri: "/",
        });
      })
      .catch(() => {
        // Soft failure — leave the user on the page to retry.
      });
  }, []);

  return (
    <div className="flex min-h-screen items-center justify-center bg-surface-canvas p-6">
      <Card className="w-full max-w-lg" data-testid="no-access-page">
        <CardHeader>
          <CardTitle>You don&apos;t have access to BusTerminal yet</CardTitle>
          <CardDescription>
            You are signed in, but no BusTerminal platform role has been assigned to your account.
            Without a role you can&apos;t use any BusTerminal feature.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-6">
          <section aria-labelledby="no-access-identity">
            <h2
              id="no-access-identity"
              className="text-xs font-medium uppercase tracking-wide text-foreground-subtle"
            >
              Your identity
            </h2>
            <dl className="mt-2 grid gap-1.5">
              <div className="grid grid-cols-[160px_1fr] gap-3">
                <dt className="text-xs font-medium text-foreground-muted">Display name</dt>
                <dd className="font-mono text-sm" data-testid="no-access-display-name">
                  {displayName}
                </dd>
              </div>
              {upn ? (
                <div className="grid grid-cols-[160px_1fr] gap-3">
                  <dt className="text-xs font-medium text-foreground-muted">UPN</dt>
                  <dd className="font-mono text-sm">{upn}</dd>
                </div>
              ) : null}
              <div className="grid grid-cols-[160px_1fr] gap-3">
                <dt className="text-xs font-medium text-foreground-muted">Object ID</dt>
                <dd className="break-all font-mono text-sm" data-testid="no-access-oid">
                  {oid}
                </dd>
              </div>
            </dl>
          </section>

          <section aria-labelledby="no-access-request">
            <h2
              id="no-access-request"
              className="text-xs font-medium uppercase tracking-wide text-foreground-subtle"
            >
              How to request access
            </h2>
            <p className="mt-2 text-sm text-foreground-default">
              Contact a BusTerminal administrator in your organization and share the Object ID
              above. They can assign you a role from the Entra portal&apos;s app registration for
              <span className="ms-1 font-mono">BusTerminal</span>.
            </p>
          </section>

          <Button
            type="button"
            intent="ghost"
            className="self-start"
            onClick={handleSignOut}
            data-testid="no-access-sign-out"
          >
            Sign out
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
