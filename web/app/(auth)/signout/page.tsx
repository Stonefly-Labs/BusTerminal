"use client";

import { useCallback } from "react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { msalReady, pca } from "@/lib/auth/msal-instance";

export default function SignOutPage() {
  const handleSignOut = useCallback(() => {
    void msalReady
      .then(() => {
        const account = pca.getActiveAccount() ?? pca.getAllAccounts()[0] ?? null;
        return pca.logoutRedirect({
          account,
          postLogoutRedirectUri: "/",
        });
      })
      .catch(() => {
        // Redirect failure is rare; user can re-try via the button.
      });
  }, []);

  return (
    <div className="flex min-h-screen items-center justify-center bg-surface-canvas p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Sign out</CardTitle>
          <CardDescription>
            You will be signed out of BusTerminal. You can sign back in at any time.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button
            type="button"
            intent="primary"
            className="w-full"
            onClick={handleSignOut}
            data-testid="signout-confirm"
          >
            Confirm sign out
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
