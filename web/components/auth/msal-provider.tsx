"use client";

import { MsalProvider as Msal } from "@azure/msal-react";
import { useEffect, useState, type ReactNode } from "react";

import { msalReady, pca } from "@/lib/auth/msal-instance";

interface MsalProviderProps {
  readonly children: ReactNode;
}

export function MsalProvider({ children }: MsalProviderProps) {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let cancelled = false;
    msalReady
      .catch(() => {
        // Initialization failure is surfaced by MSAL itself on the next
        // interactive call; flipping ready=true here lets the tree render
        // instead of locking the UI behind the skeleton.
      })
      .finally(() => {
        if (!cancelled) {
          setReady(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  if (!ready) {
    return <div aria-busy="true" aria-live="polite" data-testid="msal-provider-pending" />;
  }

  return <Msal instance={pca}>{children}</Msal>;
}
