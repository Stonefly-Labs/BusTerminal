import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ThemeProvider } from "next-themes";

// The registry global-search trigger (mounted in NavigationHeader from T115)
// calls `useRouter` from next/navigation, which needs the App Router context.
// Stub it here so the NavigationShell tests stay focused on the shell itself.
vi.mock("next/navigation", async () => {
  const actual = await vi.importActual<typeof import("next/navigation")>("next/navigation");
  return {
    ...actual,
    useRouter: () => ({
      push: vi.fn(),
      replace: vi.fn(),
      back: vi.fn(),
      forward: vi.fn(),
      refresh: vi.fn(),
      prefetch: vi.fn(),
    }),
  };
});

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
