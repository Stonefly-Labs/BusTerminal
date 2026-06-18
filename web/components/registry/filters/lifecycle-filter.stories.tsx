/**
 * Spec 009 / T079 / US2. Stories for `<LifecycleFilter>`.
 *
 * The component reads from Next.js' `useSearchParams` so the stories rely on
 * the runtime mock that Storybook provides via the @storybook/nextjs preset.
 * Each story sets an initial URL so the chip state matches what users see.
 */

import type { Meta, StoryObj } from "@storybook/nextjs";

import { LifecycleFilter } from "./lifecycle-filter";

const meta: Meta<typeof LifecycleFilter> = {
  title: "Discovery/Filters/LifecycleFilter",
  component: LifecycleFilter,
  parameters: { layout: "padded" },
};

export default meta;
type Story = StoryObj<typeof LifecycleFilter>;

export const Empty: Story = {
  parameters: {
    nextjs: { router: { asPath: "/registry/search" } },
  },
};

export const ActiveSelected: Story = {
  parameters: {
    nextjs: { router: { asPath: "/registry/search?lifecycleStatus=Active" } },
  },
};

export const AllSelected: Story = {
  parameters: {
    nextjs: {
      router: {
        asPath:
          "/registry/search?lifecycleStatus=Active&lifecycleStatus=Missing&lifecycleStatus=Archived",
      },
    },
  },
};
