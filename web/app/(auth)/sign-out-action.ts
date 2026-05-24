"use server";

import { redirect } from "next/navigation";

/**
 * Server-action shim used by the user menu (a Client Component) until the menu
 * is migrated to call MSAL directly (Phase 4 / T055). MSAL `logoutRedirect`
 * can only run in the browser, so the action redirects to `/signout` which
 * performs the actual MSAL log-out on mount.
 */
export async function signOutAction() {
  redirect("/signout");
}
