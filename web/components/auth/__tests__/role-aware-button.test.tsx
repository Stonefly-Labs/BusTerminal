import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import { RoleAwareButton } from "@/components/auth/role-aware-button";
import { RoleContextProvider } from "@/components/auth/role-context";
import type { PlatformRole } from "@/lib/auth/role-permission-matrix";

vi.mock("@/hooks/use-roles", () => ({
  useRoles: () => {
    // Hook is overridden via context in render helpers below.
    return mockRoles.current;
  },
}));

const mockRoles: { current: ReadonlySet<PlatformRole> } = { current: new Set<PlatformRole>() };

function renderWith(roles: ReadonlySet<PlatformRole>, ui: React.ReactNode) {
  mockRoles.current = roles;
  return render(
    <RoleContextProvider value={{ effectiveRoles: roles, resolved: true }}>
      {ui}
    </RoleContextProvider>,
  );
}

describe("<RoleAwareButton />", () => {
  it("renders an enabled button when the caller is authorized", () => {
    renderWith(
      new Set<PlatformRole>(["BusTerminal.Operator"]),
      <RoleAwareButton operationClass="MutateDomain">Delete queue</RoleAwareButton>,
    );

    const button = screen.getByRole("button", { name: /delete queue/i });
    expect(button).toBeEnabled();
    expect(button).not.toHaveAttribute("aria-disabled");
  });

  it("renders a disabled button when the caller is unauthorized", () => {
    renderWith(
      new Set<PlatformRole>(["BusTerminal.Reader"]),
      <RoleAwareButton operationClass="MutateDomain">Delete queue</RoleAwareButton>,
    );

    const button = screen.getByTestId("role-aware-button-disabled");
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("aria-disabled", "true");
    expect(button).toHaveAttribute(
      "data-required-roles",
      "BusTerminal.Operator,BusTerminal.Admin",
    );
  });

  it("disables for an Administer action when caller holds Operator only", () => {
    renderWith(
      new Set<PlatformRole>(["BusTerminal.Operator"]),
      <RoleAwareButton operationClass="Administer">Configure tenant</RoleAwareButton>,
    );

    expect(screen.getByTestId("role-aware-button-disabled")).toBeDisabled();
  });

  it("enables Read operations for every assigned platform role", () => {
    for (const role of [
      "BusTerminal.Admin",
      "BusTerminal.Operator",
      "BusTerminal.Reader",
      "BusTerminal.Developer",
    ] as const) {
      const { unmount } = renderWith(
        new Set<PlatformRole>([role]),
        <RoleAwareButton operationClass="Read">View dashboard</RoleAwareButton>,
      );
      expect(screen.getByRole("button", { name: /view dashboard/i })).toBeEnabled();
      unmount();
    }
  });
});
