import type { Route } from "next";
import { redirect } from "next/navigation";
import type { ReactNode } from "react";

import { NavigationShell } from "@/components/layout/navigation-shell";
import { UserMenu } from "@/components/layout/user-menu";
import { auth } from "@/lib/auth";

export default async function AuthenticatedLayout({
  children,
}: {
  readonly children: ReactNode;
}) {
  const session = await auth();
  if (!session?.user) {
    redirect("/signin" as Route);
  }
  return (
    <NavigationShell userMenu={<UserMenu user={session.user} />}>{children}</NavigationShell>
  );
}
