/**
 * Spec 006 / T069 [US1] [TEST]. Vitest tests for the conflict modal.
 * Covers: rendering the field diff, both action callbacks fire the right
 * handlers with the right payloads.
 */

import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { userEvent } from "@testing-library/user-event";

import type { ConflictResponse, RegistryEntity } from "@/lib/registry/types";

import { RegistryConflictModal } from "../registry-conflict-modal";

function makeCurrent(): RegistryEntity {
  return {
    id: "11111111-1111-1111-1111-111111111111",
    entityType: "Namespace",
    name: "orders-prod",
    environment: "dev",
    status: "Active",
    createdAtUtc: "2026-06-01T00:00:00Z",
    updatedAtUtc: "2026-06-01T01:00:00Z",
    source: "Manual",
    tags: [],
  } as RegistryEntity;
}

function makeConflict(): ConflictResponse {
  return {
    type: "https://busterminal.dev/probs/concurrency-conflict",
    title: "Concurrency conflict",
    status: 409,
    code: "ConcurrencyConflict",
    entityId: "11111111-1111-1111-1111-111111111111",
    currentVersion: '"new-etag"',
    submittedVersion: '"old-etag"',
    currentEntity: makeCurrent(),
    changedFields: [
      { field: "/description", currentValue: "server description", submittedValue: "my description" },
      { field: "/owner", currentValue: "team-a", submittedValue: "team-b" },
    ],
  };
}

describe("RegistryConflictModal", () => {
  it("renders one row per changed field", () => {
    render(
      <RegistryConflictModal
        open
        conflict={makeConflict()}
        onDiscard={() => {}}
        onForceOverwrite={() => {}}
        onClose={() => {}}
      />,
    );
    expect(screen.getByText("/description")).toBeInTheDocument();
    expect(screen.getByText("/owner")).toBeInTheDocument();
  });

  it("invokes onDiscard with the current entity when Discard is clicked", async () => {
    const onDiscard = vi.fn();
    render(
      <RegistryConflictModal
        open
        conflict={makeConflict()}
        onDiscard={onDiscard}
        onForceOverwrite={() => {}}
        onClose={() => {}}
      />,
    );
    await userEvent.click(screen.getByTestId("conflict-discard"));
    expect(onDiscard).toHaveBeenCalledTimes(1);
    expect(onDiscard.mock.calls[0]?.[0]).toMatchObject({ id: "11111111-1111-1111-1111-111111111111" });
  });

  it("invokes onForceOverwrite when Force overwrite is clicked", async () => {
    const onForce = vi.fn();
    render(
      <RegistryConflictModal
        open
        conflict={makeConflict()}
        onDiscard={() => {}}
        onForceOverwrite={onForce}
        onClose={() => {}}
      />,
    );
    await userEvent.click(screen.getByTestId("conflict-force-overwrite"));
    expect(onForce).toHaveBeenCalledTimes(1);
  });
});
