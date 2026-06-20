/**
 * Spec 009 / T059. DiscoverButton stories — enabled, disabled, in-flight,
 * success-toast, failure-toast states.
 *
 * `<DiscoverButton>` consumes hooks (auth, toast, role) + a TanStack Query
 * client. Each story wires lightweight mocks via the `useDiscoverButtonMocks`
 * decorator below so the surface renders without a live API.
 */

import { useEffect, useMemo } from "react";
import type { Meta, StoryObj } from "@storybook/nextjs";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { DiscoverButton } from "./discover-button";

type StoryArgs = {
  hasRole: boolean;
  coalesce: boolean;
  failClick: boolean;
};

function StoryShell({ hasRole, coalesce, failClick }: StoryArgs) {
  // Patch module-level hooks via window — Storybook runs CSR so we can mutate
  // globalThis to deliver mock impls. For simple visual stories this is
  // enough; the Vitest tests cover the wiring details. The write lives in
  // useEffect so the React Compiler's immutability rule stays satisfied.
  useEffect(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (window as any).__BT_DISCOVERY_MOCK__ = { hasRole, coalesce, failClick };
  }, [hasRole, coalesce, failClick]);

  const client = useMemo(
    () => new QueryClient({ defaultOptions: { queries: { retry: false } } }),
    [],
  );
  return (
    <QueryClientProvider client={client}>
      <div className="flex items-center justify-center p-10">
        <DiscoverButton namespaceId="ns_demo" />
      </div>
    </QueryClientProvider>
  );
}

const meta: Meta<typeof StoryShell> = {
  title: "Discovery/DiscoverButton",
  component: StoryShell,
  parameters: { layout: "centered" },
  argTypes: {
    hasRole: { control: "boolean" },
    coalesce: { control: "boolean" },
    failClick: { control: "boolean" },
  },
};

export default meta;
type Story = StoryObj<typeof StoryShell>;

export const Enabled: Story = {
  args: { hasRole: true, coalesce: false, failClick: false },
};

export const HiddenWithoutRole: Story = {
  args: { hasRole: false, coalesce: false, failClick: false },
};

export const Coalesced: Story = {
  args: { hasRole: true, coalesce: true, failClick: false },
};

export const FailureToast: Story = {
  args: { hasRole: true, coalesce: false, failClick: true },
};
