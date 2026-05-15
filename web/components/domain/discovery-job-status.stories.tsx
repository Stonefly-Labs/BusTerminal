import type { Meta, StoryObj } from "@storybook/nextjs";

import { DiscoveryJobStatus } from "./discovery-job-status";

const meta: Meta<typeof DiscoveryJobStatus> = {
  title: "Domain/DiscoveryJobStatus",
  component: DiscoveryJobStatus,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof DiscoveryJobStatus>;

const NOW = new Date("2026-05-15T12:00:00Z");
const FIVE_MIN_AGO = new Date(NOW.getTime() - 5 * 60_000);
const TWO_HOURS_AGO = new Date(NOW.getTime() - 2 * 3_600_000);

export const Queued: Story = {
  args: { state: "queued", now: NOW },
};

export const Running: Story = {
  args: { state: "running", startedAt: FIVE_MIN_AGO, now: NOW },
};

export const Succeeded: Story = {
  args: { state: "succeeded", startedAt: TWO_HOURS_AGO, now: NOW },
};

export const Failed: Story = {
  args: { state: "failed", startedAt: TWO_HOURS_AGO, now: NOW },
};

export const Rtl: Story = {
  args: { state: "running", startedAt: FIVE_MIN_AGO, now: NOW },
  decorators: [
    (Story) => (
      <div dir="rtl">
        <Story />
      </div>
    ),
  ],
};
