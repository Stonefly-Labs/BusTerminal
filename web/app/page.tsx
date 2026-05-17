import type { Route } from "next";
import { redirect } from "next/navigation";

import { auth } from "@/lib/auth";

export const dynamic = "force-dynamic";

export default async function RootPage() {
  const session = await auth();
  if (session?.user) {
    redirect("/platform-status" as Route);
  }
  redirect("/signin" as Route);
}
