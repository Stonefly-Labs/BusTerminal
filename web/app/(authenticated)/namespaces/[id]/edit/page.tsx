/**
 * Spec 008 / T138 / US3. Namespace edit route shell.
 *
 * Thin RSC shell that forwards the dynamic `id` segment to the Client edit
 * experience. Visibility gating (NamespaceAdministrator) happens inside the
 * Client Component.
 */

import { NamespaceEditClient } from "@/components/namespaces/edit/namespace-edit-client";

interface PageProps {
  readonly params: Promise<{ readonly id: string }>;
}

export default async function NamespaceEditPage({ params }: PageProps) {
  const { id } = await params;
  return <NamespaceEditClient id={id} />;
}
