import { describe, expect, it } from "vitest";
import { render, fireEvent } from "@testing-library/react";
import { axe } from "vitest-axe";
import type { ColumnDef } from "@tanstack/react-table";

import { DataTable } from "./data-table";

interface Row {
  readonly id: string;
  readonly name: string;
}

const COLUMNS: ColumnDef<Row>[] = [{ accessorKey: "name", header: "Name" }];
const ROWS: Row[] = [
  { id: "1", name: "orders.in" },
  { id: "2", name: "audit.in" },
];

describe("DataTable", () => {
  it("is axe-clean", async () => {
    const { container } = render(
      <DataTable columns={COLUMNS} data={ROWS} getRowId={(r) => r.id} caption="Queues" />,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it("supports keyboard arrow navigation between rows", () => {
    const { container } = render(
      <DataTable columns={COLUMNS} data={ROWS} getRowId={(r) => r.id} caption="Queues" />,
    );
    const rows = container.querySelectorAll<HTMLTableRowElement>("tbody tr");
    expect(rows.length).toBe(2);
    const first = rows.item(0);
    const second = rows.item(1);
    if (!first || !second) throw new Error("expected two rows");
    first.focus();
    fireEvent.keyDown(first, { key: "ArrowDown" });
    expect(document.activeElement).toBe(second);
  });

  it("renders the empty state when there are no rows", () => {
    const { getByRole } = render(
      <DataTable columns={COLUMNS} data={[]} getRowId={(r) => r.id} caption="Queues" />,
    );
    expect(getByRole("status")).toBeInTheDocument();
  });

  it("renders the error state when error is provided", () => {
    const { getByRole } = render(
      <DataTable
        columns={COLUMNS}
        data={[]}
        getRowId={(r) => r.id}
        caption="Queues"
        error={{ message: "boom" }}
      />,
    );
    expect(getByRole("alert")).toBeInTheDocument();
  });
});
