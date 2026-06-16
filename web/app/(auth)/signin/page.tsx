"use client";

import { InteractionStatus } from "@azure/msal-browser";
import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import { useSearchParams } from "next/navigation";
import { Suspense, useEffect } from "react";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { API_SCOPE_REQUEST } from "@/lib/auth/scopes";

function SignInRedirect() {
  const params = useSearchParams();
  const callbackUrl = params.get("callbackUrl") ?? "/home";
  const isAuthenticated = useIsAuthenticated();
  const { instance, inProgress } = useMsal();

  useEffect(() => {
    if (inProgress !== InteractionStatus.None) return;
    if (isAuthenticated) {
      window.location.replace(callbackUrl);
      return;
    }
    void instance
      .loginRedirect({
        scopes: [...API_SCOPE_REQUEST.scopes],
        redirectStartPage: callbackUrl,
      })
      .catch(() => {
        // Interactive errors surface via the MSAL event API; the page stays
        // on the skeleton so the user can refresh / re-attempt.
      });
  }, [callbackUrl, inProgress, instance, isAuthenticated]);

  return null;
}

export default function SignInPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-surface-canvas p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Signing in to BusTerminal</CardTitle>
          <CardDescription>
            Redirecting to Microsoft Entra ID. If nothing happens within a few seconds, refresh
            this page or contact your administrator.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div
            aria-busy="true"
            aria-live="polite"
            data-testid="signin-pending"
            className="h-1 w-full animate-pulse rounded bg-border-default"
          />
          <Suspense fallback={null}>
            <SignInRedirect />
          </Suspense>
        </CardContent>
      </Card>
    </div>
  );
}
