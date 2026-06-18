/**
 * Spec 009 / T079 / US2. Stories for `<ServiceAssociationFilter>`.
 */

import type { Meta, StoryObj } from "@storybook/nextjs";

import { ServiceAssociationFilter } from "./service-association-filter";

const meta: Meta<typeof ServiceAssociationFilter> = {
  title: "Discovery/Filters/ServiceAssociationFilter",
  component: ServiceAssociationFilter,
  parameters: { layout: "padded" },
};

export default meta;
type Story = StoryObj<typeof ServiceAssociationFilter>;

export const Empty: Story = {
  parameters: {
    nextjs: { router: { asPath: "/registry/search" } },
  },
};

export const ServiceSelected: Story = {
  parameters: {
    nextjs: {
      router: { asPath: "/registry/search?associatedServiceId=svc_alpha" },
    },
  },
};

export const ServiceAndRoles: Story = {
  parameters: {
    nextjs: {
      router: {
        asPath:
          "/registry/search?associatedServiceId=svc_alpha&associationRole=Owner&associationRole=Consumer",
      },
    },
  },
};
