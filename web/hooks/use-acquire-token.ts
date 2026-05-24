"use client";

import { InteractionRequiredAuthError } from "@azure/msal-browser";
import { useMsal } from "@azure/msal-react";
import { useCallback } from "react";

import { API_SCOPE_REQUEST } from "@/lib/auth/scopes";

export interface AcquireTokenOptions {
  readonly forceRefresh?: boolean;
}

export type AcquireToken = (options?: AcquireTokenOptions) => Promise<string | null>;

export function useAcquireToken(): AcquireToken {
  const { instance, accounts } = useMsal();
  return useCallback<AcquireToken>(
    async (options = {}) => {
      const account = instance.getActiveAccount() ?? accounts[0] ?? null;
      if (!account) {
        return null;
      }
      try {
        const response = await instance.acquireTokenSilent({
          scopes: [...API_SCOPE_REQUEST.scopes],
          account,
          forceRefresh: options.forceRefresh ?? false,
        });
        return response.accessToken;
      } catch (err) {
        if (err instanceof InteractionRequiredAuthError) {
          await instance.acquireTokenRedirect({ scopes: [...API_SCOPE_REQUEST.scopes] });
          return null;
        }
        throw err;
      }
    },
    [instance, accounts],
  );
}
