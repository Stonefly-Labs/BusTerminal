/**
 * Mock MSAL PublicClientApplication for spec-007 E2E tests.
 *
 * Active only when `NEXT_PUBLIC_AUTH_MODE === "mock"`. Mirrors the
 * behaviour the backend's `MockAuthenticationHandler` already provides on
 * the API side: a synthetic, role-scoped principal that never touches a
 * real identity provider.
 *
 * Design
 * ------
 *
 *  - We instantiate a **real** `PublicClientApplication` (cheap; doesn't
 *    talk to any IdP until you invoke a method that requires one) so the
 *    `<MsalProvider>` initialisation path stays unchanged and the
 *    msal-react hooks (`useMsal`, `useIsAuthenticated`, `useAccount`)
 *    work without a separate adapter.
 *
 *  - We then override the methods the BusTerminal SPA actually consults
 *    (`getAllAccounts`, `getActiveAccount`, `acquireTokenSilent`, etc.)
 *    so they synthesise data derived from the active persona instead of
 *    calling into MSAL's real machinery.
 *
 *  - The active persona lives in `sessionStorage[E2E_PERSONA_SESSION_KEY]`.
 *    The Playwright fixture writes it via `addInitScript` before any app
 *    code runs in a context; we read it lazily on every method call so a
 *    re-seeded persona is picked up without a SPA restart.
 *
 *  - When no persona is seeded, `getAllAccounts()` returns `[]` →
 *    `useIsAuthenticated()` is false → `AuthGuard` redirects to
 *    `/signin`. This matches the behaviour an unauthenticated user gets
 *    today and lets the unauthenticated-state specs continue to work.
 *
 * Safety
 * ------
 *
 *  - Production guard lives in `msal-instance.ts` — this file is only
 *    *imported* when the auth-mode branch resolves to mock. A direct
 *    import in production code is itself a defect.
 */

import {
  PublicClientApplication,
  type AccountInfo,
  type AuthenticationResult,
  type Configuration,
} from "@azure/msal-browser";

import { E2E_PERSONA_SESSION_KEY, isPersona, PERSONA_CONFIGS, type Persona } from "@/tests/auth/personas";

// Stable identifiers for the synthetic tenant + client. NOT real Entra
// values — they exist so the SPA has something deterministic to render
// and so the access-token authority URL parses cleanly.
const MOCK_TENANT_ID = "00000000-0000-0000-0000-0000000000aa";
const MOCK_CLIENT_ID = "00000000-0000-0000-0000-0000000000bb";

function readActivePersona(): Persona | null {
  if (typeof window === "undefined") {
    return null;
  }
  const raw = window.sessionStorage.getItem(E2E_PERSONA_SESSION_KEY);
  return isPersona(raw) ? raw : null;
}

function buildAccountInfo(persona: Persona): AccountInfo {
  const cfg = PERSONA_CONFIGS[persona];
  const { oid, upn, displayName } = cfg.mockAccount;
  return {
    homeAccountId: `${oid}.${MOCK_TENANT_ID}`,
    environment: "login.windows.net",
    tenantId: MOCK_TENANT_ID,
    username: upn,
    localAccountId: oid,
    name: displayName,
    idTokenClaims: {
      oid,
      tid: MOCK_TENANT_ID,
      name: displayName,
      preferred_username: upn,
      roles: [...cfg.expectedRoleAssignments],
    },
  };
}

function buildMockAuthResult(account: AccountInfo): AuthenticationResult {
  const now = Date.now();
  return {
    accessToken: `mock-${account.localAccountId}`,
    account,
    authority: `https://login.microsoftonline.com/${MOCK_TENANT_ID}`,
    expiresOn: new Date(now + 60 * 60 * 1000),
    extExpiresOn: new Date(now + 60 * 60 * 1000),
    familyId: "",
    fromCache: false,
    idToken: "mock-id-token",
    idTokenClaims: account.idTokenClaims ?? {},
    scopes: ["openid", "profile", "offline_access"],
    state: "",
    tenantId: account.tenantId,
    tokenType: "Bearer",
    uniqueId: account.localAccountId,
    correlationId:
      typeof crypto !== "undefined" && "randomUUID" in crypto
        ? crypto.randomUUID()
        : "00000000-0000-0000-0000-000000000000",
  };
}

const MOCK_CONFIG: Configuration = {
  auth: {
    clientId: MOCK_CLIENT_ID,
    authority: `https://login.microsoftonline.com/${MOCK_TENANT_ID}`,
    navigateToLoginRequestUrl: false,
  },
  cache: {
    // Match the real config so anything else reading sessionStorage MSAL
    // keys (none in our SPA, but defensively) sees consistent shape.
    cacheLocation: "sessionStorage",
    storeAuthStateInCookie: false,
  },
};

export function buildMockPca(): PublicClientApplication {
  const pca = new PublicClientApplication(MOCK_CONFIG);

  pca.getAllAccounts = (() => {
    const persona = readActivePersona();
    return persona === null ? [] : [buildAccountInfo(persona)];
  }) as typeof pca.getAllAccounts;

  pca.getActiveAccount = (() => {
    const accounts = pca.getAllAccounts();
    return accounts[0] ?? null;
  }) as typeof pca.getActiveAccount;

  // setActiveAccount is a no-op — the active account is derived from
  // sessionStorage every call. Components that try to "switch" accounts
  // by calling setActiveAccount get the next sessionStorage read instead.
  pca.setActiveAccount = (() => {
    /* intentional no-op */
  }) as typeof pca.setActiveAccount;

  pca.acquireTokenSilent = (async () => {
    const persona = readActivePersona();
    if (persona === null) {
      // Matches the real MSAL contract — silent acquisition without an
      // account throws. The api-client treats that as "no token" and
      // skips the Authorization header.
      throw new Error("mock-msal: no persona seeded — sessionStorage[bt.e2e.persona] is unset");
    }
    return buildMockAuthResult(buildAccountInfo(persona));
  }) as typeof pca.acquireTokenSilent;

  // Redirect-interaction flows are a no-op under mock auth — there's no
  // remote IdP to navigate to. The page stays where it is; on the next
  // render the AuthGuard either sees a seeded persona (and renders) or
  // doesn't (and re-redirects to /signin, which is what would happen on
  // a real failed login too).
  pca.acquireTokenRedirect = (async () => {
    /* intentional no-op */
  }) as typeof pca.acquireTokenRedirect;

  pca.loginRedirect = (async () => {
    /* intentional no-op — auth state derives from sessionStorage */
  }) as typeof pca.loginRedirect;

  pca.logoutRedirect = (async (request) => {
    if (typeof window === "undefined") return;
    window.sessionStorage.removeItem(E2E_PERSONA_SESSION_KEY);
    const target = request?.postLogoutRedirectUri ?? "/signin";
    window.location.href = target;
  }) as typeof pca.logoutRedirect;

  return pca;
}

export const MOCK_AUTH_MODE_FLAG = "mock";
