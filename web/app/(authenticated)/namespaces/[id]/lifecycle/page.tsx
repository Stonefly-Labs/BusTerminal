/**
 * Spec 008 / T139 / US3. Namespace lifecycle route shell.
 *
 * Thin RSC shell that forwards the dynamic `id` segment to the Client lifecycle
 * experience. Visibility gating (NamespaceAdministrator) happens inside the
 * Client Component.
 */

import { NamespaceLifecycleClient } from "@/components/namespaces/lifecycle/namespace-lifecycle-client";

interface PageProps {
  readonly params: Promise<{ readonly id: string }>;
}

export default async function NamespaceLifecyclePage({ params }: PageProps) {
  const { id } = await params;
  return <NamespaceLifecycleClient id={id} />;
}
