import { PublicClientApplication } from "@azure/msal-browser";

import { buildMsalConfig } from "./msal-config";
import { buildMockPca, MOCK_AUTH_MODE_FLAG } from "./msal-mock";

// Build-time safety guard. Mock auth is an E2E-only shim — shipping it
// to production would let any caller of the SPA bypass authentication.
// Next.js inlines NODE_ENV at build time; this branch becomes dead code
// (and gets tree-shaken) in any non-production build. In a production
// build with mock mode accidentally set, it throws and fails the build.
if (
  process.env.NEXT_PUBLIC_AUTH_MODE === MOCK_AUTH_MODE_FLAG &&
  process.env.NODE_ENV === "production"
) {
  throw new Error(
    "BUILD GUARD: NEXT_PUBLIC_AUTH_MODE=mock is incompatible with NODE_ENV=production. " +
      "Mock auth is a test-only shim and must never ship to a production build.",
  );
}

const useMock = process.env.NEXT_PUBLIC_AUTH_MODE === MOCK_AUTH_MODE_FLAG;

export const pca: PublicClientApplication = useMock
  ? buildMockPca()
  : new PublicClientApplication(buildMsalConfig());

export const msalReady: Promise<void> =
  typeof window !== "undefined" ? pca.initialize() : Promise.resolve();
