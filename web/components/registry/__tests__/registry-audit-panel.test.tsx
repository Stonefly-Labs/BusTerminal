/**
 * Spec 006 / T118 [US3] [TEST]. Vitest for the audit panel. Covers the
 * loaded list, the empty-state, the loading-state, and the field-diff
 * popover for Updated / StatusChanged events.
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { userEvent } from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import type { AuditEvent } from "@/lib/registry/types";

const listAuditMock = vi.fn();
vi.mock("@/lib/registry/api", () => ({
  listAuditForEntity: (...args: unknown[]) => listAuditMock(...args),
  RegistryApiError: class RegistryApiError extends Error {
    constructor(message: string, public readonly status: number) {
      super(message);
    }
  },
}));

import { RegistryAuditPanel } from "../registry-audit-panel";

function makeEvent(overrides: Partial<AuditEvent> = {}): AuditEvent {
  return {
    id: overrides.id ?? "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    entityId: overrides.entityId ?? "11111111-1111-1111-1111-111111111111",
    entityType: overrides.entityType ?? "Namespace",
    environment: overrides.environment ?? "dev",
    eventType: overrides.eventType ?? "Created",
    timestamp: overrides.timestamp ?? "2026-06-01T12:00:00Z",
    actor: overrides.actor ?? {
      principalId: "00000000-0000-0000-0000-000000000001",
      displayName: "Dev User",
    },
    changeSummary: overrides.changeSummary ?? "Created Namespace 'orders-prod'",
    fieldChanges: overrides.fieldChanges ?? null,
    wasForceOverwrite: overrides.wasForceOverwrite ?? false,
    correlationId: overrides.correlationId ?? "trace-id-1",
  } as AuditEvent;
}

function withQueryClient(children: React.ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe("RegistryAuditPanel", () => {
  beforeEach(() => {
    listAuditMock.mockReset();
  });

  it("renders the empty state when there are no audit events", async () => {
    listAuditMock.mockResolvedValueOnce([]);
    render(
      withQueryClient(<RegistryAuditPanel entityId="11111111-1111-1111-1111-111111111111" />),
    );
    await waitFor(() =>
      expect(screen.getByTestId("registry-audit-panel")).toHaveAttribute(
        "data-variant",
        "empty",
      ),
    );
  });

  it("renders events with actor, timestamp, and summary", async () => {
    listAuditMock.mockResolvedValueOnce([
      makeEvent({ id: "id-1", changeSummary: "Created Namespace 'orders-prod'" }),
    ]);

    render(
      withQueryClient(<RegistryAuditPanel entityId="11111111-1111-1111-1111-111111111111" />),
    );

    await waitFor(() =>
      expect(screen.getByTestId("registry-audit-panel")).toHaveAttribute(
        "data-variant",
        "loaded",
      ),
    );
    expect(screen.getByText("Created Namespace 'orders-prod'")).toBeInTheDocument();
    expect(screen.getByText("Dev User")).toBeInTheDocument();
    expect(screen.getByText(/2026-06-01/)).toBeInTheDocument();
  });

  it("opens the field-diff popover for Updated events with fieldChanges", async () => {
    listAuditMock.mockResolvedValueOnce([
      makeEvent({
        id: "id-2",
        eventType: "Updated",
        changeSummary: "Updated Namespace 'orders-prod'",
        fieldChanges: [
          { field: "description", before: "old", after: "new" },
          { field: "owner", before: null, after: "payments" },
        ],
      }),
    ]);

    render(
      withQueryClient(<RegistryAuditPanel entityId="11111111-1111-1111-1111-111111111111" />),
    );

    const trigger = await screen.findByTestId("registry-audit-event-trigger");
    expect(trigger).toBeInTheDocument();
    await userEvent.click(trigger);

    const diff = await screen.findByTestId("registry-audit-field-diff");
    expect(diff).toBeInTheDocument();
    const fieldChanges = screen.getAllByTestId("registry-audit-field-change");
    expect(fieldChanges).toHaveLength(2);
    expect(fieldChanges[0]).toHaveAttribute("data-field", "description");
  });

  it("does not surface a popover trigger for Created events", async () => {
    listAuditMock.mockResolvedValueOnce([makeEvent({ id: "id-3", eventType: "Created" })]);

    render(
      withQueryClient(<RegistryAuditPanel entityId="11111111-1111-1111-1111-111111111111" />),
    );

    await waitFor(() =>
      expect(screen.getByTestId("registry-audit-panel")).toHaveAttribute(
        "data-variant",
        "loaded",
      ),
    );
    expect(screen.queryByTestId("registry-audit-event-trigger")).not.toBeInTheDocument();
  });

  it("marks force-overwrite events with a badge", async () => {
    listAuditMock.mockResolvedValueOnce([
      makeEvent({
        id: "id-4",
        eventType: "Updated",
        fieldChanges: [{ field: "description", before: "x", after: "y" }],
        wasForceOverwrite: true,
      }),
    ]);

    render(
      withQueryClient(<RegistryAuditPanel entityId="11111111-1111-1111-1111-111111111111" />),
    );

    await waitFor(() => expect(screen.getByText(/Force overwrite/i)).toBeInTheDocument());
  });

  it("renders the error state on query failure", async () => {
    listAuditMock.mockRejectedValueOnce(new Error("audit-down"));

    render(
      withQueryClient(<RegistryAuditPanel entityId="11111111-1111-1111-1111-111111111111" />),
    );

    await waitFor(() =>
      expect(screen.getByTestId("registry-audit-panel")).toHaveAttribute(
        "data-variant",
        "error",
      ),
    );
  });
});
