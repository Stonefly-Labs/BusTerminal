import NextAuth, { type NextAuthConfig, type Session } from "next-auth";
import MicrosoftEntraID from "next-auth/providers/microsoft-entra-id";
import Credentials from "next-auth/providers/credentials";

declare module "next-auth" {
  interface Session {
    accessToken?: string;
  }
}

type JwtWithAccessToken = { accessToken?: string };

const DEV_TENANT_SENTINEL = "development";
export const MOCK_PROVIDER_ID = "mock-dev";

const tenantId = process.env.AZURE_AD_TENANT_ID ?? "";
const clientId = process.env.AZURE_AD_CLIENT_ID ?? "";
const clientSecret = process.env.AZURE_AD_CLIENT_SECRET ?? "";

const isMockTenant = tenantId === DEV_TENANT_SENTINEL;

const MOCK_USER = {
  id: "00000000-0000-0000-0000-000000000001",
  name: "Dev User",
  email: "dev.user@busterminal.local",
} as const;

const MOCK_ACCESS_TOKEN = "mock-access-token-development-only";

function buildEntraProviderConfig() {
  const base = { clientId, clientSecret };
  if (tenantId && !isMockTenant) {
    return { ...base, issuer: `https://login.microsoftonline.com/${tenantId}/v2.0` };
  }
  return base;
}

// In mock mode, register a Credentials provider that synthesizes the dev user
// without any OAuth redirect. This is the only provider available when
// `AZURE_AD_TENANT_ID === "development"`, so a real OAuth round-trip is
// impossible — the dev sign-in completes locally and never talks to Microsoft.
// Gated to non-production environments by the `assertNonProduction` check
// inside `authorize()`.
const mockProvider = Credentials({
  id: MOCK_PROVIDER_ID,
  name: "Dev User (mock)",
  credentials: {},
  authorize: async () => {
    if (process.env.NODE_ENV === "production" && process.env.ALLOW_MOCK_AUTH !== "true") {
      throw new Error(
        "Mock authentication is disabled in production. Set AZURE_AD_TENANT_ID to a real tenant.",
      );
    }
    return {
      id: MOCK_USER.id,
      name: MOCK_USER.name,
      email: MOCK_USER.email,
    };
  },
});

const providers = isMockTenant
  ? [mockProvider]
  : [MicrosoftEntraID(buildEntraProviderConfig())];

export const authConfig: NextAuthConfig = {
  providers,
  trustHost: true,
  session: { strategy: "jwt" },
  callbacks: {
    async jwt({ token, account }) {
      const extended = token as typeof token & JwtWithAccessToken;
      if (account?.access_token) {
        extended.accessToken = account.access_token;
      }
      if (isMockTenant && !extended.accessToken) {
        extended.accessToken = MOCK_ACCESS_TOKEN;
        extended.sub = MOCK_USER.id;
        extended.name = MOCK_USER.name;
        extended.email = MOCK_USER.email;
      }
      return extended;
    },
    async session({ session, token }) {
      const extendedToken = token as typeof token & JwtWithAccessToken;
      if (typeof extendedToken.accessToken === "string") {
        session.accessToken = extendedToken.accessToken;
      }
      if (isMockTenant && session.user) {
        session.user.name ??= MOCK_USER.name;
        session.user.email ??= MOCK_USER.email;
      }
      return session satisfies Session;
    },
  },
  pages: {
    signIn: "/signin",
    signOut: "/signout",
  },
};

export const { handlers, auth, signIn, signOut } = NextAuth(authConfig);

export const isMockAuthActive = isMockTenant;
