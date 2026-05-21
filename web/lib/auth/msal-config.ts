import type { Configuration } from "@azure/msal-browser";

const DEFAULT_TENANT_ID = "common";
const DEFAULT_CLIENT_ID = "00000000-0000-0000-0000-000000000000";

export function buildMsalConfig(): Configuration {
  const tenantId = readEnv("NEXT_PUBLIC_AZURE_AD_TENANT_ID", DEFAULT_TENANT_ID);
  const clientId = readEnv("NEXT_PUBLIC_AZURE_AD_CLIENT_ID", DEFAULT_CLIENT_ID);
  const auth: Configuration["auth"] = {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    navigateToLoginRequestUrl: true,
  };
  if (typeof window !== "undefined") {
    auth.redirectUri = window.location.origin;
    auth.postLogoutRedirectUri = window.location.origin;
  }
  return {
    auth,
    cache: {
      cacheLocation: "sessionStorage",
      storeAuthStateInCookie: false,
    },
  };
}

function readEnv(name: string, fallback: string): string {
  const value = process.env[name];
  return value && value.length > 0 ? value : fallback;
}
