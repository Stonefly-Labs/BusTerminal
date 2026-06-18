/**
 * Spec 009 / T065 / US2. Component test for `<ServiceAssociationFilter>`.
 * Covers: text input for serviceId, role chip toggling, role disabled when
 * no service id, URL state.
 */

import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { ServiceAssociationFilter } from "./service-association-filter";

const replaceMock = vi.fn();
let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: (...args: unknown[]) => replaceMock(...args) }),
  usePathname: () => "/registry/search",
  useSearchParams: () => searchParams,
}));

describe("<ServiceAssociationFilter>", () => {
  beforeEach(() => {
    replaceMock.mockClear();
    searchParams = new URLSearchParams();
  });

  it("renders the service id input and role chips", () => {
    render(<ServiceAssociationFilter />);
    expect(screen.getByTestId("associated-service-input")).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Owner" })).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Producer" })).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Consumer" })).toBeInTheDocument();
  });

  it("disables role chips when no service id is present", () => {
    render(<ServiceAssociationFilter />);
    expect(screen.getByRole("checkbox", { name: "Owner" })).toBeDisabled();
  });

  it("enables role chips once a service id is set", () => {
    searchParams = new URLSearchParams("associatedServiceId=svc_x");
    render(<ServiceAssociationFilter />);
    expect(screen.getByRole("checkbox", { name: "Owner" })).not.toBeDisabled();
  });

  it("updates the URL when the service id input changes", () => {
    render(<ServiceAssociationFilter />);
    // fireEvent.change applies the value in one shot; userEvent.type fires
    // per-keystroke which races the URL-controlled value back to "".
    fireEvent.change(screen.getByTestId("associated-service-input"), {
      target: { value: "svc_alpha" },
    });
    expect(replaceMock).toHaveBeenCalled();
    const lastUrl = replaceMock.mock.calls.at(-1)?.[0] as string;
    expect(lastUrl).toContain("associatedServiceId=svc_alpha");
  });

  it("toggles role chips into the URL", async () => {
    searchParams = new URLSearchParams("associatedServiceId=svc_x");
    render(<ServiceAssociationFilter />);
    await userEvent.click(screen.getByRole("checkbox", { name: "Owner" }));
    const url = replaceMock.mock.calls[0]?.[0] as string;
    expect(url).toContain("associatedServiceId=svc_x");
    expect(url).toContain("associationRole=Owner");
  });

  it("supports selecting multiple roles", async () => {
    searchParams = new URLSearchParams(
      "associatedServiceId=svc_x&associationRole=Owner",
    );
    render(<ServiceAssociationFilter />);
    await userEvent.click(screen.getByRole("checkbox", { name: "Consumer" }));
    const url = replaceMock.mock.calls[0]?.[0] as string;
    expect(url).toContain("associationRole=Owner");
    expect(url).toContain("associationRole=Consumer");
  });
});
