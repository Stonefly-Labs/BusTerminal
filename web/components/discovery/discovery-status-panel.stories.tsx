/**
 * Spec 009 / T060. DiscoveryStatusPanel stories — no-runs, in-flight,
 * succeeded, failed states.
 */

import { useEffect, useMemo } from "react";
import type { Meta, StoryObj } from "@storybook/nextjs";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { DiscoveryStatusPanel } from "./discovery-status-panel";

interface StoryArgs {
  state: "no-runs" | "in-flight" | "succeeded" | "failed";
}

function StoryShell({ state }: StoryArgs) {
  useEffect(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (window as any).__BT_DISCOVERY_PANEL_STATE__ = state;
  }, [state]);

  const client = useMemo(
    () => new QueryClient({ defaultOptions: { queries: { retry: false } } }),
    [],
  );
  return (
    <QueryClientProvider client={client}>
      <div className="p-6">
        <DiscoveryStatusPanel namespaceId="ns_demo" />
      </div>
    </QueryClientProvider>
  );
}

const meta: Meta<typeof StoryShell> = {
  title: "Discovery/DiscoveryStatusPanel",
  component: StoryShell,
  parameters: { layout: "padded" },
  argTypes: {
    state: { control: "radio", options: ["no-runs", "in-flight", "succeeded", "failed"] },
  },
};

export default meta;
type Story = StoryObj<typeof StoryShell>;

export const NoRunsYet: Story = { args: { state: "no-runs" } };
export const InFlight: Story = { args: { state: "in-flight" } };
export const Succeeded: Story = { args: { state: "succeeded" } };
export const Failed: Story = { args: { state: "failed" } };
