import NextAuth, { type NextAuthConfig, type Session } from "next-auth";
import MicrosoftEntraID from "next-auth/providers/microsoft-entra-id";

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
      if (account?.access_token) {
        token.accessToken = account.access_token;
      }
      if (isMockTenant && !token.accessToken) {
        token.accessToken = MOCK_ACCESS_TOKEN;
        token.sub = MOCK_USER.id;
        token.name = MOCK_USER.name;
        token.email = MOCK_USER.email;
      }
      return token;
    },
    async session({ session, token }) {
      const extended = session as Session & { accessToken?: string };
      if (typeof token.accessToken === "string") {
        extended.accessToken = token.accessToken;
      }
      if (isMockTenant && session.user) {
        session.user.name ??= MOCK_USER.name;
        session.user.email ??= MOCK_USER.email;
      }
      return extended;
    },
  },
  pages: {
    signIn: "/signin",
    signOut: "/signout",
  },
};

export const { handlers, auth, signIn, signOut } = NextAuth(authConfig);

export const isMockAuthActive = isMockTenant;
