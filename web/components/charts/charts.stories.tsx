import type { Meta, StoryObj } from "@storybook/nextjs";

import { ChartArea } from "./chart-area";
import { ChartBar } from "./chart-bar";
import { ChartLine } from "./chart-line";

const meta: Meta = {
  title: "Charts/Wrappers",
  parameters: { layout: "padded" },
};

export default meta;

type Story = StoryObj;

const TIMELINE = [
  { hour: "00:00", queue: 12, dl: 0 },
  { hour: "04:00", queue: 18, dl: 1 },
  { hour: "08:00", queue: 42, dl: 2 },
  { hour: "12:00", queue: 88, dl: 1 },
  { hour: "16:00", queue: 60, dl: 4 },
  { hour: "20:00", queue: 30, dl: 0 },
];

export const Line: Story = {
  render: () => (
    <ChartLine
      data={TIMELINE}
      xKey="hour"
      series={[
        { id: "queue", accessor: "queue", label: "Active" },
        { id: "dl", accessor: "dl", label: "Dead-letter" },
      ]}
      accessibleLabel="Queue depth over the last 24 hours"
    />
  ),
};

export const Bar: Story = {
  render: () => (
    <ChartBar
      data={TIMELINE}
      xKey="hour"
      series={[{ id: "queue", accessor: "queue", label: "Active" }]}
      accessibleLabel="Hourly active message count"
    />
  ),
};

export const Area: Story = {
  render: () => (
    <ChartArea
      data={TIMELINE}
      xKey="hour"
      series={[{ id: "queue", accessor: "queue", label: "Active" }]}
      accessibleLabel="Hourly active message count area"
    />
  ),
};

/**
 * Reduced-motion contract (T108 / FR-025 / SC-008).
 *
 * Toggle `prefers-reduced-motion: reduce` in DevTools (or the OS setting)
 * and reload. The series will mount fully drawn with no enter/update
 * tween. The `useReducedMotion` hook inside the chart wrappers reads the
 * media query and forwards `isAnimationActive={false}` to every Recharts
 * series. The Playwright spec `tests/e2e/reduced-motion.spec.ts` asserts
 * this behavior end-to-end.
 */
export const ReducedMotionContract: Story = {
  name: "Reduced motion (contract)",
  parameters: {
    docs: {
      description: {
        story:
          "When the user has `prefers-reduced-motion: reduce`, the Chart wrappers pass `isAnimationActive={false}` to each series. Toggle the OS setting and reload to verify.",
      },
    },
  },
  render: () => (
    <ChartLine
      data={TIMELINE}
      xKey="hour"
      series={[
        { id: "queue", accessor: "queue", label: "Active" },
        { id: "dl", accessor: "dl", label: "Dead-letter" },
      ]}
      accessibleLabel="Queue depth chart respecting reduced-motion preference"
    />
  ),
};
