/**
 * Home dashboard route shell. Thin RSC wrapper around the Client driver —
 * keeps the route file boilerplate-free so the navigation hierarchy stays
 * legible.
 */

import { HomeDashboard } from "@/components/home/home-dashboard";

export default function HomePage() {
  return <HomeDashboard />;
}
