/**
 * Spec 009 / T091 / US3. Discovery-run detail route shell.
 *
 * Thin RSC shell that forwards the dynamic `id` (namespace) and `runId`
 * segments to the client viewer. The viewer fetches via
 * `GET /api/discovery-runs/{runId}?namespaceId={id}` and renders
 * `<DiscoveryRunDetail>`.
 */

import { DiscoveryRunDetailViewer } from "@/components/discovery/discovery-run-detail-viewer";

interface PageProps {
  readonly params: Promise<{ readonly id: string; readonly runId: string }>;
}

export default async function DiscoveryRunDetailPage({ params }: PageProps) {
  const { id, runId } = await params;
  return (
    <section
      aria-labelledby="discovery-run-heading"
      className="flex flex-col gap-4 p-6"
      data-testid="discovery-run-page"
    >
      <div className="flex flex-col gap-1">
        <h1 id="discovery-run-heading" className="text-2xl font-semibold">
          Discovery run
        </h1>
        <p className="text-sm text-foreground-muted">
          Detail for the selected run, including classification counts and any failure context.
        </p>
      </div>
      <DiscoveryRunDetailViewer namespaceId={id} runId={runId} />
    </section>
  );
}
