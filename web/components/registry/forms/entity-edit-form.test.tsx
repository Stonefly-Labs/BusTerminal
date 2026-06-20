/**
 * Spec 009 / T100 / US4. Component test for `<PublishedEntityEditForm>`.
 * Covers:
 *   - Renders the curated fields with the entity's current values.
 *   - Submitting calls `updateEntityMetadata` with the parsed body.
 *   - 412 conflict shows the conflict banner instead of toasting.
 *   - Archive button calls `archiveEntity`.
 *   - Denied users see the "not authorized" banner instead of the form.
 */

import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { PublishedEntityEditForm } from "./published-entity-edit-form";
import type { PublishedEntity } from "@/lib/discovery/schemas";

const updateEntityMetadataMock = vi.fn();
const archiveEntityMock = vi.fn();
const acquireTokenMock = vi.fn(async () => null);
const useRolesMock = vi.fn();
const useOwnedServicesMock = vi.fn();
const routerPush = vi.fn();
const routerBack = vi.fn();
const toastError = vi.fn();
const toastSuccess = vi.fn();

vi.mock("@/lib/discovery/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/discovery/api")>("@/lib/discovery/api");
  return {
    ...actual,
    updateEntityMetadata: (...args: unknown[]) => updateEntityMetadataMock(...args),
    archiveEntity: (...args: unknown[]) => archiveEntityMock(...args),
  };
});

vi.mock("@/hooks/use-acquire-token", () => ({
  useAcquireToken: () => acquireTokenMock,
}));

vi.mock("@/hooks/use-owned-services", () => ({
  useOwnedServices: () => useOwnedServicesMock(),
}));

vi.mock("@/hooks/use-roles", () => ({
  useRoles: () => useRolesMock(),
}));

vi.mock("@/hooks/use-toast", () => ({
  useToast: () => ({
    toast: {
      success: toastSuccess,
      error: toastError,
      info: vi.fn(),
    },
  }),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: routerPush, back: routerBack }),
}));

const NOW = "2026-06-18T12:00:00.000Z";
const ENTITY: PublishedEntity = {
  id: "pe_AAAAAAAAAAAAAAAAAAAAAAAA",
  schemaVersion: "1.1",
  entityType: "Queue",
  environment: "dev",
  namespaceId: "ns_test",
  name: "orders-inbox",
  displayName: "orders-inbox",
  compositeKey: "q:ns_test/orders-inbox",
  parentEntityId: null,
  description: "Existing description",
  businessPurpose: "Existing purpose",
  tags: ["tier:critical"],
  operationalNotes: "Existing notes",
  documentationLinks: [],
  contactInformation: { primaryContact: "team@example.com", escalationPath: undefined },
  lifecycleStatus: "Active",
  lifecycleStatusChangedUtc: NOW,
  firstDiscoveredUtc: NOW,
  lastSeenUtc: NOW,
  lastDiscoveryRunId: "dr_test",
  azureSourced: { lockDuration: "PT1M" },
  azureSourcedHash: "sha256:abc",
  serviceAssociations: [],
  associatedServiceIds: [],
  associationRoles: [],
};

function renderForm(entity = ENTITY) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <PublishedEntityEditForm entity={entity} etag={"\"etag-1\""} />
    </QueryClientProvider>,
  );
}

describe("<PublishedEntityEditForm>", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useRolesMock.mockReturnValue(new Set(["BusTerminal.Admin"]));
    useOwnedServicesMock.mockReturnValue({ data: new Set<string>(), isLoading: false, error: null });
  });

  it("renders curated fields populated from the entity", () => {
    renderForm();
    expect(screen.getByLabelText("Description")).toHaveValue("Existing description");
    expect(screen.getByLabelText("Business purpose")).toHaveValue("Existing purpose");
    expect(screen.getByLabelText(/Tags/)).toHaveValue("tier:critical");
    expect(screen.getByLabelText("Operational notes")).toHaveValue("Existing notes");
  });

  it("denies an unrelated reader and skips the form", () => {
    useRolesMock.mockReturnValue(new Set(["BusTerminal.Reader"]));
    useOwnedServicesMock.mockReturnValue({ data: new Set<string>(), isLoading: false, error: null });
    renderForm();
    expect(screen.getByTestId("published-entity-edit-denied")).toBeInTheDocument();
    expect(screen.queryByTestId("published-entity-edit-form")).toBeNull();
  });

  it("submits the form via updateEntityMetadata on save", async () => {
    updateEntityMetadataMock.mockResolvedValueOnce({
      ok: true,
      entity: { ...ENTITY, description: "Updated description" },
      etag: "\"etag-2\"",
    });

    renderForm();
    const desc = screen.getByLabelText("Description");
    await userEvent.clear(desc);
    await userEvent.type(desc, "Updated description");

    await userEvent.click(screen.getByTestId("save-button"));

    await waitFor(() => expect(updateEntityMetadataMock).toHaveBeenCalledTimes(1));
    const [calledId, calledEtag, calledBody] = updateEntityMetadataMock.mock.calls[0]!;
    expect(calledId).toBe(ENTITY.id);
    expect(calledEtag).toBe("\"etag-1\"");
    expect(calledBody).toMatchObject({ description: "Updated description" });
    expect(toastSuccess).toHaveBeenCalled();
  });

  it("shows the conflict banner on 412", async () => {
    updateEntityMetadataMock.mockResolvedValueOnce({
      ok: false,
      conflict: { status: 412, body: undefined },
    });

    renderForm();
    await userEvent.click(screen.getByTestId("save-button"));

    expect(await screen.findByTestId("conflict-banner")).toBeInTheDocument();
    expect(toastError).not.toHaveBeenCalled();
  });

  it("calls archiveEntity when the archive button is clicked", async () => {
    archiveEntityMock.mockResolvedValueOnce({
      ok: true,
      entity: { ...ENTITY, lifecycleStatus: "Archived" },
      etag: "\"etag-archived\"",
    });

    renderForm();
    await userEvent.click(screen.getByTestId("archive-entity-button"));

    await waitFor(() => expect(archiveEntityMock).toHaveBeenCalledTimes(1));
    expect(toastSuccess).toHaveBeenCalledWith("Archived", expect.any(Object));
  });

  it("disables the archive button when already archived", () => {
    renderForm({ ...ENTITY, lifecycleStatus: "Archived" });
    expect(screen.getByTestId("archive-entity-button")).toBeDisabled();
  });
});
