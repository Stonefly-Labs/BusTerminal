import { describe, expect, it } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ThemeProvider } from "next-themes";

import { NavigationShell } from "@/components/layout/navigation-shell";
import { THEME_STORAGE_KEY } from "@/lib/theme-provider-constants";

function renderWithProviders(ui: React.ReactNode) {
  return render(
    <ThemeProvider
      attribute="class"
      defaultTheme="light"
      enableSystem={false}
      storageKey={THEME_STORAGE_KEY}
      disableTransitionOnChange
    >
      {ui}
    </ThemeProvider>,
  );
}

describe("<NavigationShell />", () => {
  it("renders the BusTerminal brand label in the header", () => {
    renderWithProviders(
      <NavigationShell userMenu={<button>User</button>}>
        <p>page-content</p>
      </NavigationShell>,
    );

    expect(screen.getByText("BusTerminal")).toBeInTheDocument();
  });

  it("renders the theme toggle button", () => {
    renderWithProviders(
      <NavigationShell userMenu={<button>User</button>}>
        <p>page-content</p>
      </NavigationShell>,
    );

    const toggle = screen.getByRole("button", { name: /switch to (dark|light) theme|toggle theme/i });
    expect(toggle).toBeInTheDocument();
    fireEvent.click(toggle);
    expect(toggle).toBeInTheDocument();
  });

  it("renders the supplied user-menu slot as a sibling of the brand", () => {
    renderWithProviders(
      <NavigationShell userMenu={<button data-testid="user-menu-slot">User</button>}>
        <p>page-content</p>
      </NavigationShell>,
    );

    expect(screen.getByTestId("user-menu-slot")).toBeInTheDocument();
  });

  it("renders the main content", () => {
    renderWithProviders(
      <NavigationShell userMenu={<button>User</button>}>
        <p data-testid="main-content">page-content</p>
      </NavigationShell>,
    );

    expect(screen.getByTestId("main-content")).toBeInTheDocument();
  });
});
