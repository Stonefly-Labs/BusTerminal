/**
 * Spec 008 / T114 / US2. Namespace details route shell.
 *
 * Thin RSC shell that forwards the dynamic `id` segment to the Client
 * Component details experience. Next.js 16 dynamic params are async-aware
 * — destructure via `await params`.
 */

import { NamespaceDetails } from "@/components/namespaces/details/namespace-details";

interface PageProps {
  readonly params: Promise<{ readonly id: string }>;
}

export default async function NamespaceDetailsPage({ params }: PageProps) {
  const { id } = await params;
  return <NamespaceDetails id={id} />;
}
