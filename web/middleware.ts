import { NextResponse, type NextRequest } from "next/server";
import { auth } from "@/lib/auth";

const PUBLIC_PATHS = new Set<string>([
  "/signin",
  "/signout",
]);

function isPublicPath(pathname: string): boolean {
  if (PUBLIC_PATHS.has(pathname)) return true;
  if (pathname.startsWith("/api/auth/")) return true;
  if (pathname.startsWith("/_next/")) return true;
  if (pathname.startsWith("/healthz/")) return true;
  // Slice-001 design-system showcase is dev-only and reachable without auth.
  if (pathname === "/showcase" || pathname.startsWith("/showcase/")) return true;
  return false;
}

export default auth((request: NextRequest & { auth: unknown }) => {
  const { pathname, search } = request.nextUrl;

  if (isPublicPath(pathname)) {
    return NextResponse.next();
  }

  if (request.auth) {
    return NextResponse.next();
  }

  const signInUrl = request.nextUrl.clone();
  signInUrl.pathname = "/signin";
  signInUrl.search = `callbackUrl=${encodeURIComponent(pathname + search)}`;
  return NextResponse.redirect(signInUrl);
});

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico|api/auth).*)"],
};
