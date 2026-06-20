/**
 * Spec 009 / T113. ServiceAssociationEditor stories — empty, populated,
 * duplicate-attempt, server-error states.
 *
 * The component depends on TanStack Query + `useAcquireToken` + the
 * discovery API client. Stories stub the API surface via window-level
 * mocks so the dialog renders end-to-end without a live API. The dialog
 * starts open in each story so the body shape is visible without an
 * interaction.
 */

import { useEffect, useMemo } from "react";
import type { Meta, StoryObj } from "@storybook/nextjs";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { ServiceAssociationEditor } from "./service-association-editor";
import type { EntityServiceAssociation } from "@/lib/discovery/schemas";

const NOW = "2026-06-18T12:00:00.000Z";

const SAMPLE_ASSOCIATIONS: readonly EntityServiceAssociation[] = [
  { associationId: "esa_demo_1", serviceId: "svc_payments", role: "Owner", createdUtc: NOW, createdBy: "operator" },
  { associationId: "esa_demo_2", serviceId: "svc_inventory", role: "Consumer", createdUtc: NOW, createdBy: "operator" },
];

type StoryArgs = {
  initialAssociations: readonly EntityServiceAssociation[];
  forceError: "none" | "duplicate" | "server";
};

function StoryShell({ initialAssociations, forceError }: StoryArgs) {
  useEffect(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (window as any).__BT_ASSOC_EDITOR_MOCK__ = { forceError };
  }, [forceError]);
  const client = useMemo(
    () => new QueryClient({ defaultOptions: { queries: { retry: false } } }),
    [],
  );
  return (
    <QueryClientProvider client={client}>
      <div className="flex items-center justify-center p-10">
        <ServiceAssociationEditor
          entityId="pe_DEMOAAAAAAAAAAAAAAAAAAAA"
          initialAssociations={initialAssociations}
          etag={'"etag-demo"'}
          onMutated={() => {}}
        />
      </div>
    </QueryClientProvider>
  );
}

const meta: Meta<typeof StoryShell> = {
  title: "Discovery/ServiceAssociationEditor",
  component: StoryShell,
  args: {
    initialAssociations: [],
    forceError: "none",
  },
  parameters: {
    layout: "fullscreen",
  },
};

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {
  args: { initialAssociations: [] },
};

export const Populated: Story = {
  args: { initialAssociations: SAMPLE_ASSOCIATIONS },
};

export const DuplicateAttempt: Story = {
  args: {
    initialAssociations: SAMPLE_ASSOCIATIONS,
    forceError: "duplicate",
  },
};

export const ServerError: Story = {
  args: {
    initialAssociations: SAMPLE_ASSOCIATIONS,
    forceError: "server",
  },
};
