"use client";

import { MsalProvider as Msal } from "@azure/msal-react";
import { useEffect, type ReactNode } from "react";

import { msalReady, pca } from "@/lib/auth/msal-instance";

interface MsalProviderProps {
  readonly children: ReactNode;
}

export function MsalProvider({ children }: MsalProviderProps) {
  useEffect(() => {
    void msalReady.catch(() => {
      // Initialization failures surface on the first interactive call.
    });
  }, []);

  return <Msal instance={pca}>{children}</Msal>;
}
