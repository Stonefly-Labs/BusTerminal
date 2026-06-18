/**
 * Spec 009 / T079 / US2. Stories for `<EntityDiscoveryInfo>` showing the
 * three lifecycle states + a freshly-discovered entity.
 */

import type { Meta, StoryObj } from "@storybook/nextjs";

import { EntityDiscoveryInfo } from "./entity-discovery-info";

const meta: Meta<typeof EntityDiscoveryInfo> = {
  title: "Discovery/EntityDiscoveryInfo",
  component: EntityDiscoveryInfo,
  parameters: { layout: "padded" },
};

export default meta;
type Story = StoryObj<typeof EntityDiscoveryInfo>;

const now = "2026-06-17T14:32:00Z";
const lastWeek = "2026-06-10T09:00:00Z";

export const Active: Story = {
  args: {
    entity: {
      lifecycleStatus: "Active",
      firstDiscoveredUtc: lastWeek,
      lastSeenUtc: now,
      lastDiscoveryRunId: "dr_01HZAB7VMQ12345678901234",
    },
  },
};

export const Missing: Story = {
  args: {
    entity: {
      lifecycleStatus: "Missing",
      firstDiscoveredUtc: lastWeek,
      lastSeenUtc: "2026-06-16T03:00:00Z",
      lastDiscoveryRunId: "dr_01HZAB7VMQ12345678901235",
    },
  },
};

export const Archived: Story = {
  args: {
    entity: {
      lifecycleStatus: "Archived",
      firstDiscoveredUtc: lastWeek,
      lastSeenUtc: now,
      lastDiscoveryRunId: "dr_01HZAB7VMQ12345678901236",
    },
  },
};

export const FreshlyDiscovered: Story = {
  args: {
    entity: {
      lifecycleStatus: "Active",
      firstDiscoveredUtc: now,
      lastSeenUtc: now,
      lastDiscoveryRunId: "dr_01HZAB7VMQ12345678901237",
    },
  },
};
