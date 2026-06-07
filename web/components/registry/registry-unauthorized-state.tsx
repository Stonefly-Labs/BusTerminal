"use client";

/**
 * Spec 006 / T103a / FR-031. Registry-specific unauthorized empty state.
 * Renders when an API call returns 401 — the user is no longer authenticated
 * and must re-acquire a token before the registry view will work.
 *
 * The "Sign in again" CTA triggers MSAL re-authentication and preserves the
 * current URL so the operator returns to where they were after sign-in.
 */

import { useRouter, usePathname } from "next/navigation";
import { LockKeyhole } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { Route } from "next";
import { cn } from "@/lib/design-system/cn";

interface RegistryUnauthorizedStateProps {
  readonly className?: string;
  readonly returnTo?: string;
}

export function RegistryUnauthorizedState({
  className,
  returnTo,
}: RegistryUnauthorizedStateProps) {
  const router = useRouter();
  const pathname = usePathname();

  const handleSignIn = () => {
    const target = returnTo ?? pathname ?? "/registry";
    const search = new URLSearchParams({ returnTo: target }).toString();
    router.push(`/signin?${search}` as Route);
  };

  return (
    <Alert
      data-testid="registry-unauthorized-state"
      intent="warning"
      className={cn(className)}
    >
      <LockKeyhole aria-hidden="true" className="size-5" />
      <AlertTitle>Your session expired</AlertTitle>
      <AlertDescription className="flex flex-col gap-3">
        <span>
          Your authentication token is no longer valid. Sign in again to continue working
          in the registry — you&apos;ll be returned to this page after signing in.
        </span>
        <Button onClick={handleSignIn} size="sm" className="w-fit">
          Sign in again
        </Button>
      </AlertDescription>
    </Alert>
  );
}
