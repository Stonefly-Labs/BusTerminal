import type { AccountInfo } from "@azure/msal-browser";

export interface PlatformIdTokenClaims {
  readonly oid?: string;
  readonly tid?: string;
  readonly name?: string;
  readonly preferred_username?: string;
  readonly roles?: readonly string[];
}

function claimsOf(account: AccountInfo | null): PlatformIdTokenClaims | undefined {
  return (account?.idTokenClaims ?? undefined) as PlatformIdTokenClaims | undefined;
}

export function getOid(account: AccountInfo | null): string | null {
  if (!account) return null;
  return account.localAccountId || claimsOf(account)?.oid || null;
}

export function getTid(account: AccountInfo | null): string | null {
  if (!account) return null;
  return account.tenantId || claimsOf(account)?.tid || null;
}

export function getName(account: AccountInfo | null): string | null {
  if (!account) return null;
  return claimsOf(account)?.name ?? account.name ?? null;
}

export function getPreferredUsername(account: AccountInfo | null): string | null {
  if (!account) return null;
  return claimsOf(account)?.preferred_username ?? account.username ?? null;
}

export function getRoles(account: AccountInfo | null): readonly string[] {
  if (!account) return [];
  return claimsOf(account)?.roles ?? [];
}
