import type { NextConfig } from "next";
import withBundleAnalyzer from "@next/bundle-analyzer";

// Several feature clients call the API via same-origin relative paths
// (`/api/registry/...` — spec 006; `/api/namespaces/...` — spec 008;
// `/api/discovery-runs/...`, `/api/entities/...` — spec 009) rather than
// through `lib/api-client` (which targets `NEXT_PUBLIC_API_BASE_URL`
// directly). The rewrite below proxies every same-origin `/api/*` path to
// the backend.
//
// This matters in BOTH local dev (SPA :3000, backend :8090) AND the deployed
// environments: there the SPA and API are SEPARATE Container Apps on distinct
// FQDNs, so a same-origin `/api/*` request hits the web container and 404s
// unless proxied here. (Earlier this only covered `/api/registry`, which is
// why namespaces + discovery 404'd in deployed dev.) There are no local
// `web/app/api` route handlers, so a blanket `/api/:path*` proxy is safe and
// future-proof — new backend surfaces no longer need a matching rewrite.
const apiTarget =
  process.env.BUSTERMINAL_DEV_API_TARGET ??
  process.env.NEXT_PUBLIC_API_BASE_URL ??
  "http://localhost:8090";

const nextConfig: NextConfig = {
  typedRoutes: true,
  output: "standalone",
  devIndicators: false,
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiTarget}/api/:path*` }];
  },
};

const withAnalyzer = withBundleAnalyzer({
  enabled: process.env.ANALYZE === "true",
  openAnalyzer: false,
});

export default withAnalyzer(nextConfig);
