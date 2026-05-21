/**
 * Phase 2 → Phase 4 transitional shim.
 *
 * Phase 1 (T001) removed `next-auth` from `package.json`. The inherited
 * 002 callers — `app/page.tsx`, `app/(authenticated)/**`, `app/(auth)/**`,
 * `app/api/auth/[...nextauth]/route.ts`, `middleware.ts` — still import
 * from this module. Phase 3 (T041–T047) rewires them to MSAL and Phase 4
 * (T055) deletes this file entirely.
 *
 * Until then, this hand-rolled shim supplies type-compatible no-ops so
 * `pnpm typecheck` stays green. The exports return null sessions and do
 * not authenticate anybody — the real wiring is being rebuilt around them.
 */

/* eslint-disable @typescript-eslint/no-unused-vars -- transitional shim */

import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";

export const MOCK_PROVIDER_ID = "mock-dev";
export const isMockAuthActive = false;

export interface SessionUser {
  readonly id?: string;
  readonly name?: string | null;
  readonly email?: string | null;
  readonly image?: string | null;
}

export interface Session {
  readonly accessToken?: string;
  readonly user?: SessionUser;
}

type AuthMiddlewareResult =
  | Response
  | undefined
  | void
  | Promise<Response | undefined | void>;

type AuthMiddlewareHandler = (
  request: NextRequest & { auth: unknown },
) => AuthMiddlewareResult;

export function auth(): Promise<Session | null>;
export function auth(
  handler: AuthMiddlewareHandler,
): (request: NextRequest) => Promise<Response>;
export function auth(
  handler?: AuthMiddlewareHandler,
): Promise<Session | null> | ((request: NextRequest) => Promise<Response>) {
  if (handler) {
    return async (request: NextRequest) => {
      const augmented = Object.assign(request, { auth: null as unknown });
      const result = await handler(augmented as NextRequest & { auth: unknown });
      return (result as Response | undefined) ?? NextResponse.next();
    };
  }
  return Promise.resolve(null);
}

interface SignInOptions {
  readonly redirectTo?: string;
}

export async function signIn(_provider?: string, _options?: SignInOptions): Promise<void> {
  // Phase 3 T041 replaces every caller with MSAL `loginRedirect`.
}

interface SignOutOptions {
  readonly redirectTo?: string;
}

export async function signOut(_options?: SignOutOptions): Promise<void> {
  // Phase 3 T042 / T047 replace callers with MSAL `logoutRedirect`.
}

export const handlers = {
  GET: async (_request: Request): Promise<Response> =>
    new Response(null, { status: 410, statusText: "Gone — NextAuth removed in spec 003" }),
  POST: async (_request: Request): Promise<Response> =>
    new Response(null, { status: 410, statusText: "Gone — NextAuth removed in spec 003" }),
};
