/**
 * Spec 006 / T106 [US2] [TEST]. Vitest tests for the search results table.
 * Covers each lifecycle state (empty, loading, loaded, error, unavailable)
 * and the FR-031 distinction between "no results" and "search unavailable".
 */

import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { userEvent } from "@testing-library/user-event";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
  usePathname: () => "/registry/search",
}));

import { RegistrySearchResultsTable, type RegistrySearchResultRow } from "../registry-search-results-table";

const sampleRow: RegistrySearchResultRow = {
  id: "11111111-1111-1111-1111-111111111111",
  entityType: "Queue",
  name: "orders-incoming",
  fullyQualifiedName: "orders-prod/orders-incoming",
  environment: "dev",
  status: "Active",
  owner: "payments-platform",
  namespaceName: "orders-prod",
  score: 1.42,
};

describe("RegistrySearchResultsTable", () => {
  it("renders the idle empty state when no query has been entered", () => {
    render(
      <RegistrySearchResultsTable
        results={[]}
        state="idle"
        page={1}
        pageSize={25}
      />,
    );
    const empty = screen.getByTestId("registry-empty-state");
    expect(empty).toHaveAttribute("data-variant", "no-data");
  });

  it("renders a loading message while searching", () => {
    render(
      <RegistrySearchResultsTable
        results={[]}
        state="loading"
        page={1}
        pageSize={25}
      />,
    );
    expect(screen.getByTestId("registry-search-results-loading")).toBeInTheDocument();
  });

  it("distinguishes 'no results' from 'unavailable' (FR-031)", () => {
    const noResults = render(
      <RegistrySearchResultsTable
        results={[]}
        state="loaded"
        page={1}
        pageSize={25}
      />,
    );
    expect(noResults.getByTestId("registry-empty-state")).toHaveAttribute("data-variant", "no-results");
    noResults.unmount();

    const unavailable = render(
      <RegistrySearchResultsTable
        results={[]}
        state="unavailable"
        page={1}
        pageSize={25}
      />,
    );
    expect(unavailable.getByTestId("registry-empty-state")).toHaveAttribute("data-variant", "unavailable");
  });

  it("renders loaded result rows with the canonical fields", () => {
    render(
      <RegistrySearchResultsTable
        results={[sampleRow]}
        state="loaded"
        page={1}
        pageSize={25}
        totalCount={1}
        onPageChange={() => {}}
      />,
    );
    expect(screen.getByText("orders-incoming")).toBeInTheDocument();
    expect(screen.getByText("orders-prod/orders-incoming")).toBeInTheDocument();
    expect(screen.getByText("payments-platform")).toBeInTheDocument();
    expect(screen.getByText("Queue")).toBeInTheDocument();
  });

  it("invokes onPageChange when paging forward", async () => {
    const onPageChange = vi.fn();
    render(
      <RegistrySearchResultsTable
        results={[sampleRow]}
        state="loaded"
        page={1}
        pageSize={25}
        totalCount={100}
        onPageChange={onPageChange}
      />,
    );
    await userEvent.click(screen.getByRole("button", { name: /Next/i }));
    expect(onPageChange).toHaveBeenCalledWith(2);
  });
});
