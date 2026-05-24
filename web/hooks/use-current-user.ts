"use client";

import type { AccountInfo } from "@azure/msal-browser";
import { useAccount, useMsal } from "@azure/msal-react";

export function useCurrentUser(): AccountInfo | null {
  const { accounts, instance } = useMsal();
  const active = useAccount(instance.getActiveAccount() ?? accounts[0] ?? undefined);
  return active ?? null;
}
