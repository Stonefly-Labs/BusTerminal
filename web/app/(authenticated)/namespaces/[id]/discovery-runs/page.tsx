/**
 * Spec 009 / T090 / US3. Discovery history list route shell.
 *
 * Thin RSC shell that forwards the dynamic `id` segment to the client
 * viewer. The viewer fetches the first page via TanStack Query, mounts
 * `<DiscoveryRunsTable>`, and exposes cursor-paged "Load more" for the
 * subsequent pages.
 */

import { DiscoveryRunsHistoryViewer } from "@/components/discovery/discovery-runs-history-viewer";

interface PageProps {
  readonly params: Promise<{ readonly id: string }>;
}

export default async function DiscoveryRunsPage({ params }: PageProps) {
  const { id } = await params;
  return (
    <section
      aria-labelledby="discovery-history-heading"
      className="flex flex-col gap-4 p-6"
      data-testid="discovery-history-page"
    >
      <div className="flex flex-col gap-1">
        <h1 id="discovery-history-heading" className="text-2xl font-semibold">
          Discovery history
        </h1>
        <p className="text-sm text-foreground-muted">
          Past discovery runs for this namespace. Click a run id to drill into timing, counts, and any failure detail.
        </p>
      </div>
      <DiscoveryRunsHistoryViewer namespaceId={id} />
    </section>
  );
}
