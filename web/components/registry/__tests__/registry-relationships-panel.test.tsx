/**
 * Spec 006 / T118 [US3] [TEST]. Vitest for the relationships panel. Covers:
 * leaf-type short-circuit, loaded/empty/error states, and the row-click
 * navigation contract.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { userEvent } from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import type { RegistryEntity } from "@/lib/registry/types";

const pushMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock, replace: vi.fn(), prefetch: vi.fn() }),
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

const listEntitiesMock = vi.fn();
vi.mock("@/lib/registry/api", () => ({
  listEntities: (...args: unknown[]) => listEntitiesMock(...args),
  RegistryApiError: class RegistryApiError extends Error {
    constructor(message: string, public readonly status: number) {
      super(message);
    }
  },
}));

import { RegistryRelationshipsPanel } from "../registry-relationships-panel";

function makeChild(overrides: Partial<RegistryEntity> = {}): RegistryEntity {
  return {
    id: overrides.id ?? "22222222-2222-2222-2222-222222222222",
    entityType: overrides.entityType ?? "Topic",
    name: overrides.name ?? "orders-topic",
    environment: overrides.environment ?? "dev",
    status: overrides.status ?? "Active",
    createdAtUtc: "2026-06-01T00:00:00Z",
    updatedAtUtc: "2026-06-01T00:00:00Z",
    source: "Manual",
    tags: [],
    ...overrides,
  } as RegistryEntity;
}

function withQueryClient(children: React.ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe("RegistryRelationshipsPanel", () => {
  beforeEach(() => {
    listEntitiesMock.mockReset();
    pushMock.mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("short-circuits with a leaf-type message for Queue entities", () => {
    render(
      withQueryClient(
        <RegistryRelationshipsPanel
          entity={{ id: "id", entityType: "Queue", environment: "dev" }}
        />,
      ),
    );
    const panel = screen.getByTestId("registry-relationships-panel");
    expect(panel).toHaveAttribute("data-variant", "leaf");
    expect(panel.textContent).toMatch(/Queue entities have no children/);
    expect(listEntitiesMock).not.toHaveBeenCalled();
  });

  it("short-circuits with a leaf-type message for Rule entities", () => {
    render(
      withQueryClient(
        <RegistryRelationshipsPanel
          entity={{ id: "id", entityType: "Rule", environment: "dev" }}
        />,
      ),
    );
    expect(screen.getByTestId("registry-relationships-panel")).toHaveAttribute(
      "data-variant",
      "leaf",
    );
  });

  it("renders loaded children rows", async () => {
    listEntitiesMock.mockResolvedValueOnce({
      items: [
        makeChild({ id: "33333333-3333-3333-3333-333333333333", name: "orders-events" }),
        makeChild({ id: "44444444-4444-4444-4444-444444444444", name: "billing-events" }),
      ],
      continuationToken: null,
    });

    render(
      withQueryClient(
        <RegistryRelationshipsPanel
          entity={{
            id: "11111111-1111-1111-1111-111111111111",
            entityType: "Namespace",
            environment: "dev",
          }}
        />,
      ),
    );

    await waitFor(() =>
      expect(screen.getByTestId("registry-relationships-panel")).toHaveAttribute(
        "data-variant",
        "loaded",
      ),
    );
    expect(screen.getByText("orders-events")).toBeInTheDocument();
    expect(screen.getByText("billing-events")).toBeInTheDocument();
  });

  it("renders the empty-children placeholder when the query returns no items", async () => {
    listEntitiesMock.mockResolvedValueOnce({ items: [], continuationToken: null });

    render(
      withQueryClient(
        <RegistryRelationshipsPanel
          entity={{
            id: "11111111-1111-1111-1111-111111111111",
            entityType: "Namespace",
            environment: "dev",
          }}
        />,
      ),
    );

    await waitFor(() =>
      expect(screen.getByTestId("registry-relationships-panel")).toHaveAttribute(
        "data-variant",
        "empty",
      ),
    );
  });

  it("navigates to the child detail page on row click", async () => {
    listEntitiesMock.mockResolvedValueOnce({
      items: [makeChild({ id: "33333333-3333-3333-3333-333333333333", name: "drill-target" })],
      continuationToken: null,
    });

    render(
      withQueryClient(
        <RegistryRelationshipsPanel
          entity={{
            id: "11111111-1111-1111-1111-111111111111",
            entityType: "Namespace",
            environment: "dev",
          }}
        />,
      ),
    );

    // Wait for the row to appear, then click on a cell outside the Link
    // wrapper (the entity-type cell) so the row-level onClick fires.
    const row = await waitFor(() =>
      screen.getByText("drill-target").closest("tr")!,
    );
    expect(row).not.toBeNull();
    // The "Topic" type label is in a separate <td> without the Link wrapper.
    await userEvent.click(screen.getByText("Topic"));
    expect(pushMock).toHaveBeenCalledWith(
      "/registry/Topic/33333333-3333-3333-3333-333333333333",
    );
  });

  it("surfaces an error state when the query fails", async () => {
    listEntitiesMock.mockRejectedValueOnce(new Error("boom"));

    render(
      withQueryClient(
        <RegistryRelationshipsPanel
          entity={{
            id: "11111111-1111-1111-1111-111111111111",
            entityType: "Namespace",
            environment: "dev",
          }}
        />,
      ),
    );

    await waitFor(() =>
      expect(screen.getByTestId("registry-relationships-panel")).toHaveAttribute(
        "data-variant",
        "error",
      ),
    );
  });
});
