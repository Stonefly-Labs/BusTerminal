import type { NextConfig } from "next";
import withBundleAnalyzer from "@next/bundle-analyzer";

// In dev the SPA runs on :3000 and the backend on a sibling port (see
// the spec-007 mock-auth quickstart — backend defaults to
// `BUSTERMINAL_API_PORT=8090`). Some client code calls the API via the
// same-origin relative path `/api/registry/...` rather than through
// `lib/api-client` (which honours `NEXT_PUBLIC_API_BASE_URL`). The
// rewrite below forwards those same-origin paths to the backend so both
// patterns work locally. In production the SPA and the API are
// co-located behind the same ingress; the rewrite has no effect there.
const apiTarget =
  process.env.BUSTERMINAL_DEV_API_TARGET ??
  process.env.NEXT_PUBLIC_API_BASE_URL ??
  "http://localhost:8090";

const nextConfig: NextConfig = {
  typedRoutes: true,
  output: "standalone",
  devIndicators: false,
  async rewrites() {
    return [
      { source: "/api/registry/:path*", destination: `${apiTarget}/api/registry/:path*` },
      { source: "/api/registry", destination: `${apiTarget}/api/registry` },
    ];
  },
};

const withAnalyzer = withBundleAnalyzer({
  enabled: process.env.ANALYZE === "true",
  openAnalyzer: false,
});

export default withAnalyzer(nextConfig);
