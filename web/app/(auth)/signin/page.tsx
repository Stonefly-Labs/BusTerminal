import type { Metadata } from "next";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { MOCK_PROVIDER_ID, isMockAuthActive, signIn } from "@/lib/auth";

export const metadata: Metadata = {
  title: "Sign in",
};

async function signInAction(formData: FormData) {
  "use server";
  const callbackUrl = (formData.get("callbackUrl") as string) || "/platform-status";
  const providerId = isMockAuthActive ? MOCK_PROVIDER_ID : "microsoft-entra-id";
  await signIn(providerId, { redirectTo: callbackUrl });
}

interface SignInPageProps {
  readonly searchParams: Promise<{ callbackUrl?: string }>;
}

export default async function SignInPage({ searchParams }: SignInPageProps) {
  const { callbackUrl = "/platform-status" } = await searchParams;
  return (
    <div className="flex min-h-screen items-center justify-center bg-surface-canvas p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Sign in to BusTerminal</CardTitle>
          <CardDescription>
            {isMockAuthActive
              ? "Development mode — sign in as the synthetic dev user."
              : "Authenticate with your organization's Microsoft Entra ID account."}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form action={signInAction}>
            <input type="hidden" name="callbackUrl" value={callbackUrl} />
            <Button type="submit" intent="primary" className="w-full">
              {isMockAuthActive ? "Continue as Dev User" : "Sign in with Microsoft Entra ID"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
