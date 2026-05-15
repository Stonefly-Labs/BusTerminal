import type { ColumnDef } from "@tanstack/react-table";

import { Badge } from "@/components/ui/badge";

export type QueueStatus = "active" | "idle" | "error";

export interface QueueRow {
  readonly id: string;
  readonly name: string;
  readonly active: number;
  readonly deadLetter: number;
  readonly maxDelivery: number;
  readonly status: QueueStatus;
}

export const SHOWCASE_QUEUES: ReadonlyArray<QueueRow> = [
  { id: "q-001", name: "orders.in", active: 1240, deadLetter: 0, maxDelivery: 10, status: "active" },
  { id: "q-002", name: "orders.out", active: 980, deadLetter: 2, maxDelivery: 10, status: "active" },
  { id: "q-003", name: "audit.in", active: 22, deadLetter: 0, maxDelivery: 5, status: "idle" },
  { id: "q-004", name: "billing.errors", active: 0, deadLetter: 14, maxDelivery: 5, status: "error" },
  { id: "q-005", name: "shipping.notifications", active: 312, deadLetter: 1, maxDelivery: 10, status: "active" },
  { id: "q-006", name: "search.indexing", active: 9, deadLetter: 0, maxDelivery: 8, status: "idle" },
];

const STATUS_INTENT: Record<QueueStatus, "success" | "warning" | "error" | "neutral"> = {
  active: "success",
  idle: "neutral",
  error: "error",
};

import { createElement } from "react";

export const SHOWCASE_COLUMNS: ReadonlyArray<ColumnDef<QueueRow>> = [
  {
    id: "name",
    accessorKey: "name",
    header: "Name",
    cell: (info) =>
      createElement("span", { className: "font-mono" }, info.getValue<string>()),
  },
  { id: "active", accessorKey: "active", header: "Active" },
  { id: "deadLetter", accessorKey: "deadLetter", header: "Dead-letter" },
  { id: "maxDelivery", accessorKey: "maxDelivery", header: "Max delivery" },
  {
    id: "status",
    accessorKey: "status",
    header: "Status",
    cell: (info) => {
      const value = info.getValue<QueueStatus>();
      return createElement(Badge, { intent: STATUS_INTENT[value] }, value);
    },
  },
];
