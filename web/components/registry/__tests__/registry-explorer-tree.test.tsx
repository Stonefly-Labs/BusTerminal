/**
 * Spec 006 / T068 [US1] [TEST]. Vitest component tests for the explorer tree.
 *
 * Mocks the registry API so the tree component renders deterministically
 * without a running backend.
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { userEvent } from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import type { RegistryEntity } from "@/lib/registry/types";

vi.mock("@/lib/registry/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/registry/api")>("@/lib/registry/api");
  return {
    ...actual,
    listEntities: vi.fn(),
  };
});

import { listEntities } from "@/lib/registry/api";
import { RegistryExplorerTree } from "../registry-explorer-tree";

function makeEntity(partial: Partial<RegistryEntity>): RegistryEntity {
  return {
    id: partial.id ?? crypto.randomUUID(),
    entityType: partial.entityType ?? "Namespace",
    name: partial.name ?? "untitled",
    environment: partial.environment ?? "dev",
    status: partial.status ?? "Active",
    createdAtUtc: "2026-06-01T00:00:00Z",
    updatedAtUtc: "2026-06-01T00:00:00Z",
    source: "Manual",
    tags: [],
    ...partial,
  } as RegistryEntity;
}

function renderTree(props: { environment: string }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <RegistryExplorerTree {...props} />
    </QueryClientProvider>,
  );
}

describe("RegistryExplorerTree", () => {
  beforeEach(() => {
    vi.mocked(listEntities).mockReset();
  });

  it("renders an empty state when no namespaces are present", async () => {
    vi.mocked(listEntities).mockResolvedValueOnce({ items: [], continuationToken: null });
    renderTree({ environment: "dev" });
    await waitFor(() => {
      expect(screen.getByTestId("registry-empty-state")).toBeInTheDocument();
    });
  });

  it("renders a single namespace as a tree node", async () => {
    vi.mocked(listEntities).mockResolvedValueOnce({
      items: [makeEntity({ name: "orders-prod" })],
      continuationToken: null,
    });
    renderTree({ environment: "dev" });
    await waitFor(() => {
      expect(screen.getByText("orders-prod")).toBeInTheDocument();
    });
  });

  it("expands a namespace and lazy-loads its children", async () => {
    const nsId = "00000000-0000-0000-0000-000000000001";
    vi.mocked(listEntities)
      .mockResolvedValueOnce({
        items: [makeEntity({ id: nsId, name: "orders-prod" })],
        continuationToken: null,
      })
      .mockResolvedValueOnce({
        items: [
          makeEntity({ entityType: "Queue", name: "orders-in", parentId: nsId }),
          makeEntity({ entityType: "Topic", name: "orders-events", parentId: nsId }),
        ],
        continuationToken: null,
      });

    renderTree({ environment: "dev" });
    await waitFor(() => {
      expect(screen.getByText("orders-prod")).toBeInTheDocument();
    });
    const toggle = screen.getByTestId(`tree-toggle-${nsId}`);
    await userEvent.click(toggle);
    await waitFor(() => {
      expect(screen.getByText("orders-in")).toBeInTheDocument();
      expect(screen.getByText("orders-events")).toBeInTheDocument();
    });
  });

  it("renders an unavailable state on backend error", async () => {
    vi.mocked(listEntities).mockRejectedValueOnce(new Error("backend down"));
    renderTree({ environment: "dev" });
    await waitFor(() => {
      const empty = screen.getByTestId("registry-empty-state");
      expect(empty).toHaveAttribute("data-variant", "unavailable");
    });
  });
});
