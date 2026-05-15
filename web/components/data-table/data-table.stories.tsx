import type { Meta, StoryObj } from "@storybook/nextjs";
import type { ColumnDef } from "@tanstack/react-table";

import { Badge } from "@/components/ui/badge";

import { DataTable } from "./data-table";

interface Queue {
  readonly id: string;
  readonly name: string;
  readonly active: number;
  readonly deadLetter: number;
  readonly status: "active" | "idle" | "error";
}

const QUEUES: Queue[] = [
  { id: "q1", name: "orders.in", active: 1240, deadLetter: 0, status: "active" },
  { id: "q2", name: "orders.out", active: 980, deadLetter: 2, status: "active" },
  { id: "q3", name: "audit.in", active: 22, deadLetter: 0, status: "idle" },
  { id: "q4", name: "billing.errors", active: 0, deadLetter: 14, status: "error" },
];

const COLUMNS: ColumnDef<Queue>[] = [
  { accessorKey: "name", header: "Name", cell: (info) => <span className="font-mono">{info.getValue<string>()}</span> },
  { accessorKey: "active", header: "Active" },
  { accessorKey: "deadLetter", header: "Dead-letter" },
  {
    accessorKey: "status",
    header: "Status",
    cell: (info) => {
      const value = info.getValue<Queue["status"]>();
      const intent = value === "active" ? "success" : value === "error" ? "error" : "neutral";
      return <Badge intent={intent}>{value}</Badge>;
    },
  },
];

const meta: Meta<typeof DataTable<Queue>> = {
  title: "Data table/DataTable",
  component: DataTable,
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj<typeof DataTable<Queue>>;

export const Default: Story = {
  render: () => (
    <DataTable
      columns={COLUMNS}
      data={QUEUES}
      getRowId={(row) => row.id}
      caption="Queues"
      paginationMode="paginated"
      searchColumnId="name"
    />
  ),
};

export const Loading: Story = {
  render: () => (
    <DataTable
      columns={COLUMNS}
      data={[]}
      getRowId={(row) => row.id}
      caption="Queues"
      isLoading
    />
  ),
};

export const Empty: Story = {
  render: () => (
    <DataTable
      columns={COLUMNS}
      data={[]}
      getRowId={(row) => row.id}
      caption="Queues"
    />
  ),
};

export const ErrorState: Story = {
  render: () => (
    <DataTable
      columns={COLUMNS}
      data={[]}
      getRowId={(row) => row.id}
      caption="Queues"
      error={{ message: "Backend offline", retry: () => undefined }}
    />
  ),
};
