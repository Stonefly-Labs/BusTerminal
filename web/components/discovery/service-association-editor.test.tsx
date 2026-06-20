/**
 * Spec 009 / T099 / US4. Component test for `<ServiceAssociationEditor>`.
 * Covers:
 *   - Renders the trigger + opens the dialog.
 *   - Shows initial associations on open.
 *   - Submitting a new association calls addEntityAssociation.
 *   - 409 duplicate-conflict surfaces the inline error.
 *   - Remove button calls removeEntityAssociation.
 *   - Validation error on empty serviceId.
 */

import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { ServiceAssociationEditor } from "./service-association-editor";
import type { EntityServiceAssociation } from "@/lib/discovery/schemas";

const addEntityAssociationMock = vi.fn();
const removeEntityAssociationMock = vi.fn();
const listEntityAssociationsMock = vi.fn();
const acquireTokenMock = vi.fn(async () => null);

vi.mock("@/lib/discovery/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/discovery/api")>("@/lib/discovery/api");
  return {
    ...actual,
    addEntityAssociation: (...args: unknown[]) => addEntityAssociationMock(...args),
    removeEntityAssociation: (...args: unknown[]) => removeEntityAssociationMock(...args),
    listEntityAssociations: (...args: unknown[]) => listEntityAssociationsMock(...args),
  };
});

vi.mock("@/hooks/use-acquire-token", () => ({
  useAcquireToken: () => acquireTokenMock,
}));

const NOW = "2026-06-18T12:00:00.000Z";
const SEED_ASSOC: EntityServiceAssociation = {
  associationId: "esa_seed",
  serviceId: "svc_alpha",
  role: "Owner",
  createdUtc: NOW,
  createdBy: "operator",
};

function renderWithClient(initial: readonly EntityServiceAssociation[] = []) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ServiceAssociationEditor
        entityId="pe_TEST00000000000000000000"
        initialAssociations={initial}
        etag={"\"etag-1\""}
        onMutated={() => {}}
      />
    </QueryClientProvider>,
  );
}

describe("<ServiceAssociationEditor>", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listEntityAssociationsMock.mockResolvedValue([]);
  });

  it("renders the trigger when collapsed", () => {
    renderWithClient();
    expect(screen.getByTestId("service-associations-trigger")).toBeInTheDocument();
    expect(screen.queryByTestId("service-associations-body")).toBeNull();
  });

  it("opens the dialog and lists initial associations", async () => {
    renderWithClient([SEED_ASSOC]);
    await userEvent.click(screen.getByTestId("service-associations-trigger"));

    const body = await screen.findByTestId("service-associations-body");
    expect(within(body).getByText("svc_alpha")).toBeInTheDocument();
    // "Owner" appears in the badge AND in the role dropdown's option list,
    // so just confirm the seeded row's badge exists by combining svc + role.
    const seededRow = within(body).getByTestId("association-esa_seed");
    expect(seededRow).toHaveTextContent(/Owner/);
  });

  it("shows an empty state when there are no associations", async () => {
    renderWithClient([]);
    await userEvent.click(screen.getByTestId("service-associations-trigger"));
    expect(await screen.findByTestId("associations-empty")).toBeInTheDocument();
  });

  it("submits a new association via addEntityAssociation", async () => {
    addEntityAssociationMock.mockResolvedValueOnce({
      associationId: "esa_new",
      serviceId: "svc_new",
      role: "Consumer",
      createdUtc: NOW,
      createdBy: "operator",
    });
    listEntityAssociationsMock.mockResolvedValueOnce([
      SEED_ASSOC,
      { associationId: "esa_new", serviceId: "svc_new", role: "Consumer", createdUtc: NOW, createdBy: "operator" },
    ]);

    renderWithClient([SEED_ASSOC]);
    await userEvent.click(screen.getByTestId("service-associations-trigger"));

    await userEvent.type(screen.getByTestId("add-service-id-input"), "svc_new");
    await userEvent.click(screen.getByTestId("add-association-submit"));

    await waitFor(() => expect(addEntityAssociationMock).toHaveBeenCalledTimes(1));
    expect(addEntityAssociationMock).toHaveBeenCalledWith(
      "pe_TEST00000000000000000000",
      "\"etag-1\"",
      { serviceId: "svc_new", role: "Consumer" },
      {},
    );
  });

  it("surfaces 409 duplicate-conflict inline", async () => {
    addEntityAssociationMock.mockResolvedValueOnce({
      ok: false,
      conflict: { status: 409, body: undefined },
    });

    renderWithClient([SEED_ASSOC]);
    await userEvent.click(screen.getByTestId("service-associations-trigger"));
    await userEvent.type(screen.getByTestId("add-service-id-input"), "svc_alpha");
    await userEvent.click(screen.getByTestId("add-association-submit"));

    const error = await screen.findByTestId("service-associations-error");
    expect(error).toHaveTextContent(/already exists/i);
  });

  it("rejects an empty serviceId via Zod", async () => {
    renderWithClient();
    await userEvent.click(screen.getByTestId("service-associations-trigger"));
    await userEvent.click(screen.getByTestId("add-association-submit"));

    expect(addEntityAssociationMock).not.toHaveBeenCalled();
  });

  it("removes an existing association", async () => {
    removeEntityAssociationMock.mockResolvedValueOnce(undefined);
    listEntityAssociationsMock.mockResolvedValueOnce([]);

    renderWithClient([SEED_ASSOC]);
    await userEvent.click(screen.getByTestId("service-associations-trigger"));
    await userEvent.click(screen.getByTestId(`remove-${SEED_ASSOC.associationId}`));

    await waitFor(() => expect(removeEntityAssociationMock).toHaveBeenCalledTimes(1));
    expect(removeEntityAssociationMock).toHaveBeenCalledWith(
      "pe_TEST00000000000000000000",
      SEED_ASSOC.associationId,
      "\"etag-1\"",
      {},
    );
  });
});
