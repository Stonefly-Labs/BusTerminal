import type { Configuration } from "@azure/msal-browser";

const DEFAULT_TENANT_ID = "common";
const DEFAULT_CLIENT_ID = "00000000-0000-0000-0000-000000000000";

// Static-literal `process.env.NEXT_PUBLIC_*` reads — Next.js's compiler
// inlines these into the client bundle at build time. A dynamic-key form
// (`process.env[name]`) does NOT get inlined and silently resolves to
// undefined in the browser, which previously caused the SPA to fall back
// to the `common` tenant + zero-GUID client id even when `.env.local`
// supplied real values. Keep these as static literal accesses.
const ENV_TENANT_ID = process.env.NEXT_PUBLIC_AZURE_AD_TENANT_ID;
const ENV_CLIENT_ID = process.env.NEXT_PUBLIC_AZURE_AD_CLIENT_ID;

export function buildMsalConfig(): Configuration {
  const tenantId = ENV_TENANT_ID && ENV_TENANT_ID.length > 0 ? ENV_TENANT_ID : DEFAULT_TENANT_ID;
  const clientId = ENV_CLIENT_ID && ENV_CLIENT_ID.length > 0 ? ENV_CLIENT_ID : DEFAULT_CLIENT_ID;
  // `navigateToLoginRequestUrl` (default `true`) was removed from
  // BrowserAuthOptions in msal-browser v5 — it now lives in a
  // `handleRedirectPromise({ navigateToLoginRequestUrl })` option, which
  // msal-react calls with the default. We relied on the default, so there is
  // nothing to carry over here.
  const auth: Configuration["auth"] = {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
  };
  if (typeof window !== "undefined") {
    auth.redirectUri = window.location.origin;
    auth.postLogoutRedirectUri = window.location.origin;
  }
  return {
    auth,
    // `storeAuthStateInCookie` was removed from CacheOptions in msal-browser
    // v5 (IE11-era legacy); the default behaviour is unchanged for our flow.
    cache: {
      cacheLocation: "sessionStorage",
    },
  };
}
