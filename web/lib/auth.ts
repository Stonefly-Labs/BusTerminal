import NextAuth, { type NextAuthConfig, type Session } from "next-auth";
import MicrosoftEntraID from "next-auth/providers/microsoft-entra-id";

declare module "next-auth" {
  interface Session {
    accessToken?: string;
  }
}

type JwtWithAccessToken = { accessToken?: string };

const DEV_TENANT_SENTINEL = "development";

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

function buildProviderConfig() {
  if (isMockTenant) {
    return {
      clientId: "mock-client",
      clientSecret: "mock-secret",
      issuer: "https://login.microsoftonline.com/common/v2.0",
    };
  }
  const base = { clientId, clientSecret };
  if (tenantId) {
    return { ...base, issuer: `https://login.microsoftonline.com/${tenantId}/v2.0` };
  }
  return base;
}

const providers = [MicrosoftEntraID(buildProviderConfig())];

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
