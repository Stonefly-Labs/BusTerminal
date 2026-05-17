import type { Metadata } from "next";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { signOut } from "@/lib/auth";

export const metadata: Metadata = {
  title: "Sign out",
};

async function confirmSignOutAction() {
  "use server";
  await signOut({ redirectTo: "/" });
}

export default function SignOutPage() {
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
          <form action={confirmSignOutAction}>
            <Button type="submit" intent="primary" className="w-full">
              Confirm sign out
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
