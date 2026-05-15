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
            <TableCell className="font-mono">{q.name}</TableCell>
            <TableCell>{q.count}</TableCell>
            <TableCell>{q.dl}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  ),
};
