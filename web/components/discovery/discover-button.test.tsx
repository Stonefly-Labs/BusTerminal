/**
 * Spec 009 / T040 / US1. Component-level coverage for `<DiscoverButton>`:
 *   - hidden when the caller lacks NamespaceAdministrator
 *   - clicking POSTs and surfaces a toast on success
 *   - clicking surfaces a toast on failure
 */

import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { DiscoverButton } from "./discover-button";

const startDiscoveryMock = vi.fn();
const getDiscoveryRunMock = vi.fn();
const acquireTokenMock = vi.fn(async () => null);
const useHasRoleMock = vi.fn();
const toastInfo = vi.fn();
const toastError = vi.fn();
const toastSuccess = vi.fn();

vi.mock("@/lib/discovery/api", () => ({
  startDiscovery: (...args: unknown[]) => startDiscoveryMock(...args),
  getDiscoveryRun: (...args: unknown[]) => getDiscoveryRunMock(...args),
}));

vi.mock("@/hooks/use-acquire-token", () => ({
  useAcquireToken: () => acquireTokenMock,
}));

vi.mock("@/hooks/use-has-role", () => ({
  useHasRole: (...args: unknown[]) => useHasRoleMock(...args),
}));

vi.mock("@/hooks/use-toast", () => ({
  useToast: () => ({
    toast: {
      info: toastInfo,
      error: toastError,
      success: toastSuccess,
    },
  }),
}));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("<DiscoverButton>", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders nothing when the caller lacks NamespaceAdministrator", () => {
    useHasRoleMock.mockReturnValue(false);
    renderWithClient(<DiscoverButton namespaceId="ns_1" />);
    expect(screen.queryByTestId("discover-button")).toBeNull();
  });

  it("triggers startDiscovery and shows an info toast on success", async () => {
    useHasRoleMock.mockReturnValue(true);
    startDiscoveryMock.mockResolvedValueOnce({
      discoveryRunId: "dr_1",
      status: "Queued",
      coalescedFromExisting: false,
    });
    getDiscoveryRunMock.mockResolvedValue({
      id: "dr_1",
      status: "InProgress",
      schemaVersion: "1.0",
      namespaceId: "ns_1",
      trigger: "Manual",
      requestedBy: "u",
      startedUtc: new Date().toISOString(),
    });

    renderWithClient(<DiscoverButton namespaceId="ns_1" />);
    await userEvent.click(screen.getByTestId("discover-button"));

    await waitFor(() => expect(startDiscoveryMock).toHaveBeenCalledTimes(1));
    expect(toastInfo).toHaveBeenCalledWith("Discovery requested.");
  });

  it("shows a coalesced-info toast when the run was joined", async () => {
    useHasRoleMock.mockReturnValue(true);
    startDiscoveryMock.mockResolvedValueOnce({
      discoveryRunId: "dr_2",
      status: "InProgress",
      coalescedFromExisting: true,
    });
    getDiscoveryRunMock.mockResolvedValue({
      id: "dr_2",
      status: "InProgress",
      schemaVersion: "1.0",
      namespaceId: "ns_1",
      trigger: "Manual",
      requestedBy: "u",
      startedUtc: new Date().toISOString(),
    });

    renderWithClient(<DiscoverButton namespaceId="ns_1" />);
    await userEvent.click(screen.getByTestId("discover-button"));

    await waitFor(() =>
      expect(toastInfo).toHaveBeenCalledWith("A discovery is already in flight — joined the existing run."),
    );
  });

  it("shows an error toast when startDiscovery throws", async () => {
    useHasRoleMock.mockReturnValue(true);
    startDiscoveryMock.mockRejectedValueOnce(new Error("Boom"));

    renderWithClient(<DiscoverButton namespaceId="ns_1" />);
    await userEvent.click(screen.getByTestId("discover-button"));

    await waitFor(() => expect(toastError).toHaveBeenCalledWith("Boom"));
  });
});
