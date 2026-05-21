import { PublicClientApplication } from "@azure/msal-browser";

import { buildMsalConfig } from "./msal-config";

export const pca = new PublicClientApplication(buildMsalConfig());

export const msalReady: Promise<void> =
  typeof window !== "undefined" ? pca.initialize() : Promise.resolve();
