import type { Meta, StoryObj } from "@storybook/nextjs";

import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "./table";

const meta: Meta<typeof Table> = {
  title: "Primitives/Table",
  component: Table,
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj<typeof Table>;

const QUEUES = [
  { name: "orders.in", count: 1240, dl: 0 },
  { name: "orders.out", count: 980, dl: 2 },
  { name: "audit.in", count: 22, dl: 0 },
];

export const Default: Story = {
  render: () => (
    <Table>
      <TableCaption>Queues in orders-westus</TableCaption>
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Active count</TableHead>
          <TableHead>Dead-lettered</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {QUEUES.map((q) => (
          <TableRow key={q.name}>
            <TableCell mono>{q.name}</TableCell>
            <TableCell>{q.count}</TableCell>
            <TableCell>{q.dl}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  ),
};

/**
 * Demonstrates the monospace cell convention (FR-009 / T102) — technical
 * identifiers (queue names, correlation IDs) read with unambiguous
 * character widths next to body-family columns.
 */
const ROWS = [
  {
    name: "orders.westus.priority",
    correlationId: "01H8XK4Y5Z9M2N6P3Q1R8T7V0W",
    active: 1240,
  },
  {
    name: "orders.westus.standard",
    correlationId: "01H8XK4Y5Z9M2N6P3Q1R8T7V1X",
    active: 980,
  },
  {
    name: "audit.westus.events",
    correlationId: "01H8XK4Y5Z9M2N6P3Q1R8T7V2Y",
    active: 22,
  },
];

export const MonoIdentifiers: Story = {
  render: () => (
    <Table>
      <TableCaption>Messaging identifiers render in the monospace family</TableCaption>
      <TableHeader>
        <TableRow>
          <TableHead>Queue name</TableHead>
          <TableHead>Correlation ID</TableHead>
          <TableHead>Active</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {ROWS.map((row) => (
          <TableRow key={row.name}>
            <TableCell mono>{row.name}</TableCell>
            <TableCell mono>{row.correlationId}</TableCell>
            <TableCell>{row.active}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  ),
};
