"use client";

/**
 * Spec 008 / T092 — Onboard route.
 *
 * Client Component (wizard owns RHF state + sessionStorage). Requires the
 * namespace-administrator role at the API; the UI surfaces a forbidden state
 * for users without it so they don't reach a 403 from `POST /api/namespaces`.
 */

import Link from "next/link";

import { useHasRole } from "@/hooks/use-has-role";
import { NamespaceOnboardingWizard } from "@/components/namespaces/wizard/namespace-onboarding-wizard";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";

export default function NamespaceOnboardPage() {
  const isAdmin = useHasRole("BusTerminal.NamespaceAdministrator");
  if (!isAdmin) {
    return <ForbiddenState />;
  }
  return (
    <div className="mx-auto w-full max-w-4xl p-6">
      <NamespaceOnboardingWizard />
    </div>
  );
}

function ForbiddenState() {
  return (
    <div className="mx-auto w-full max-w-2xl p-6">
      <Card>
        <CardContent className="flex flex-col gap-4 p-6">
          <h1 className="text-xl font-semibold text-foreground-default">
            Namespace administrator role required
          </h1>
          <p className="text-sm text-foreground-muted">
            Onboarding a namespace requires the <code className="font-mono">namespace-administrator</code> Entra App
            Role. Ask your tenant administrator to assign it via the BusTerminal Enterprise app, then
            reload this page.
          </p>
          <div>
            <Button asChild intent="outline">
              <Link href={"/namespaces" as never}>Back to inventory</Link>
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
