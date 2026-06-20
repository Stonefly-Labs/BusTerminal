/**
 * Spec 006 / T103e [US1] [TEST]. Vitest test for the environment switcher.
 * Covers: first-visit alphabetical default + localStorage persistence + URL
 * propagation hook.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const routerReplace = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: routerReplace, push: vi.fn(), back: vi.fn() }),
  useSearchParams: () => new URLSearchParams(""),
  usePathname: () => "/registry",
}));

const acquireTokenMock = vi.fn(async () => "test-token");

vi.mock("@/hooks/use-acquire-token", () => ({
  useAcquireToken: () => acquireTokenMock,
}));

vi.mock("@/lib/registry/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/registry/api")>("@/lib/registry/api");
  return {
    ...actual,
    listEnvironments: vi.fn(),
  };
});

import { listEnvironments } from "@/lib/registry/api";
import {
  RegistryEnvSwitcher,
  REGISTRY_ENV_STORAGE_KEY,
} from "../registry-env-switcher";

function renderSwitcher() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <RegistryEnvSwitcher />
    </QueryClientProvider>,
  );
}

describe("RegistryEnvSwitcher", () => {
  beforeEach(() => {
    routerReplace.mockClear();
    localStorage.clear();
  });

  afterEach(() => {
    vi.mocked(listEnvironments).mockReset();
  });

  it("renders the switcher when environments are available", async () => {
    vi.mocked(listEnvironments).mockResolvedValueOnce(["prod", "dev", "qa"]);
    renderSwitcher();
    await waitFor(() => {
      expect(screen.getByTestId("registry-env-switcher")).toBeInTheDocument();
    });
  });

  it("picks the alphabetically-first environment on first visit", async () => {
    vi.mocked(listEnvironments).mockResolvedValueOnce(["prod", "dev", "qa"]);
    renderSwitcher();
    await waitFor(() => {
      expect(routerReplace).toHaveBeenCalled();
    });
    const lastCall = routerReplace.mock.calls[routerReplace.mock.calls.length - 1]?.[0] as string;
    expect(lastCall).toContain("environment=dev");
  });

  it("uses the persisted environment from localStorage when present", async () => {
    localStorage.setItem(REGISTRY_ENV_STORAGE_KEY, "prod");
    vi.mocked(listEnvironments).mockResolvedValueOnce(["prod", "dev", "qa"]);
    renderSwitcher();
    await waitFor(() => {
      expect(routerReplace).toHaveBeenCalled();
    });
    const lastCall = routerReplace.mock.calls[routerReplace.mock.calls.length - 1]?.[0] as string;
    expect(lastCall).toContain("environment=prod");
  });

  it("attaches the acquired bearer token to the environments call", async () => {
    // Regression guard: the env switcher must resolve a token via
    // useAcquireToken and pass it as accessToken, else it 401s under real
    // Entra auth and the registry has no selectable environment.
    vi.mocked(listEnvironments).mockResolvedValueOnce(["dev"]);
    renderSwitcher();
    await waitFor(() => {
      expect(listEnvironments).toHaveBeenCalledWith(
        expect.objectContaining({ accessToken: "test-token" }),
      );
    });
  });

  it("renders nothing if no environments are configured", async () => {
    vi.mocked(listEnvironments).mockResolvedValueOnce([]);
    renderSwitcher();
    await waitFor(() => {
      expect(screen.queryByTestId("registry-env-switcher-loading")).not.toBeInTheDocument();
    });
    expect(screen.queryByTestId("registry-env-switcher")).not.toBeInTheDocument();
  });
});
