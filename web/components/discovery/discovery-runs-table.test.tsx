/**
 * Spec 009 / T082 / US3. Component test for `<DiscoveryRunsTable>`.
 *
 * Covers:
 *   - reverse-chronological render order (table preserves caller order;
 *     server already sorts so the test asserts the data flows through)
 *   - status badge mapping (Succeeded → success intent, Failed → error)
 *   - duration formatting (ms / s / m s)
 *   - row link → router.push to the per-run detail route
 *   - "Load more" only renders when a continuationToken is present
 */

import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import type { DiscoveryRun } from "@/lib/discovery/schemas";
import { DiscoveryRunsTable } from "./discovery-runs-table";

const pushMock = vi.fn();
const acquireTokenMock = vi.fn(async () => null);
const listDiscoveryRunsMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: (...args: unknown[]) => pushMock(...args) }),
}));

vi.mock("@/hooks/use-acquire-token", () => ({
  useAcquireToken: () => acquireTokenMock,
}));

vi.mock("@/lib/discovery/api", () => ({
  listDiscoveryRuns: (...args: unknown[]) => listDiscoveryRunsMock(...args),
}));

function makeRun(partial: Partial<DiscoveryRun> = {}): DiscoveryRun {
  // Use spread overlay so explicit `null`/`undefined` in `partial` survive
  // — `??` would silently coalesce them back to defaults.
  return {
    id: "dr_RUN0000000000000000000001",
    schemaVersion: "1.0",
    namespaceId: "ns_1",
    status: "Succeeded",
    trigger: "Manual",
    startedUtc: "2026-06-17T10:00:00Z",
    completedUtc: "2026-06-17T10:02:30Z",
    durationMs: 150_000,
    requestedBy: "00000000-1111-2222-3333-444444444444",
    queueCount: 0,
    topicCount: 0,
    subscriptionCount: 0,
    ruleCount: 0,
    newCount: 1,
    updatedCount: 2,
    unchangedCount: 3,
    missingCount: 0,
    failure: null,
    coalescedRequests: [],
    correlationId: "00-test-trace",
    ...partial,
  };
}

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("<DiscoveryRunsTable>", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders one row per item with status badges", () => {
    const items = [
      makeRun({ id: "dr_OK", status: "Succeeded" }),
      makeRun({ id: "dr_BAD", status: "Failed" }),
    ];
    renderWithClient(<DiscoveryRunsTable namespaceId="ns_1" initialItems={items} />);

    expect(screen.getByTestId("run-status-dr_OK")).toHaveTextContent("Succeeded");
    expect(screen.getByTestId("run-status-dr_BAD")).toHaveTextContent("Failed");
  });

  it("preserves caller ordering (reverse-chronological from the server)", () => {
    const newer = makeRun({ id: "dr_NEWER", startedUtc: "2026-06-17T12:00:00Z" });
    const older = makeRun({ id: "dr_OLDER", startedUtc: "2026-06-17T08:00:00Z" });
    renderWithClient(
      <DiscoveryRunsTable namespaceId="ns_1" initialItems={[newer, older]} />,
    );

    const rows = screen.getAllByRole("row").filter((r) => r.getAttribute("tabindex") === "0");
    // Header row is excluded by the tabindex filter; first body row should be `newer`.
    expect(rows[0]).toContainElement(screen.getByTestId("run-link-dr_NEWER"));
    expect(rows[1]).toContainElement(screen.getByTestId("run-link-dr_OLDER"));
  });

  it("formats durations across ms / s / m boundaries", () => {
    const items = [
      makeRun({ id: "dr_FAST", durationMs: 250 }),
      makeRun({ id: "dr_SECS", durationMs: 4500 }),
      makeRun({ id: "dr_MINS", durationMs: 150_000 }),
      makeRun({ id: "dr_UNKNOWN", durationMs: null }),
    ];
    renderWithClient(<DiscoveryRunsTable namespaceId="ns_1" initialItems={items} />);

    expect(screen.getByTestId("run-duration-dr_FAST")).toHaveTextContent("250 ms");
    expect(screen.getByTestId("run-duration-dr_SECS")).toHaveTextContent("4.5 s");
    expect(screen.getByTestId("run-duration-dr_MINS")).toHaveTextContent("2m 30s");
    expect(screen.getByTestId("run-duration-dr_UNKNOWN")).toHaveTextContent("—");
  });

  it("renders the per-classification count summary", () => {
    const run = makeRun({
      id: "dr_COUNTS",
      newCount: 5,
      updatedCount: 2,
      unchangedCount: 30,
      missingCount: 1,
    });
    renderWithClient(<DiscoveryRunsTable namespaceId="ns_1" initialItems={[run]} />);

    expect(screen.getByTestId("run-counts-dr_COUNTS")).toHaveTextContent(
      "5 new · 2 upd · 30 same · 1 miss",
    );
  });

  it("navigates to the run detail route on row-link click", async () => {
    const run = makeRun({ id: "dr_CLICK" });
    renderWithClient(<DiscoveryRunsTable namespaceId="ns_abc" initialItems={[run]} />);

    await userEvent.click(screen.getByTestId("run-link-dr_CLICK"));

    expect(pushMock).toHaveBeenCalledWith("/namespaces/ns_abc/discovery-runs/dr_CLICK");
  });

  it("renders empty state when the page is empty", () => {
    renderWithClient(<DiscoveryRunsTable namespaceId="ns_1" initialItems={[]} />);
    expect(screen.getByText("No discovery runs yet")).toBeInTheDocument();
  });

  it("hides the Load more button when there is no continuation token", () => {
    renderWithClient(
      <DiscoveryRunsTable namespaceId="ns_1" initialItems={[makeRun()]} initialContinuationToken={null} />,
    );
    expect(screen.queryByTestId("discovery-runs-load-more")).toBeNull();
  });

  it("renders the Load more button when a continuation token is present", () => {
    renderWithClient(
      <DiscoveryRunsTable
        namespaceId="ns_1"
        initialItems={[makeRun()]}
        initialContinuationToken="cursor-abc"
      />,
    );
    expect(screen.getByTestId("discovery-runs-load-more")).toBeInTheDocument();
  });
});
