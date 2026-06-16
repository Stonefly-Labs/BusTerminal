/**
 * Spec 008 / T003 — placeholder layout for the namespaces section.
 *
 * The full nav + breadcrumb shell lands in a later Phase 3+ task. For now this
 * is a transparent passthrough so the `/namespaces/*` route tree compiles and
 * the upstream `(authenticated)` shell continues to render its chrome.
 */

import type { ReactNode } from "react";

export default function NamespacesLayout({ children }: { readonly children: ReactNode }) {
  return <>{children}</>;
}
