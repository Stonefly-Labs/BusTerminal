/**
 * Spec 009 / T064 / US2. Component test for `<LifecycleFilter>`.
 * Covers: chip render per status, multi-select toggle behavior, URL state
 * round-trip via the mocked router.
 */

import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import { LifecycleFilter } from "./lifecycle-filter";

const replaceMock = vi.fn();
let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: (...args: unknown[]) => replaceMock(...args) }),
  usePathname: () => "/registry/search",
  useSearchParams: () => searchParams,
}));

describe("<LifecycleFilter>", () => {
  beforeEach(() => {
    replaceMock.mockClear();
    searchParams = new URLSearchParams();
  });

  it("renders one chip per lifecycle status", () => {
    render(<LifecycleFilter />);
    expect(screen.getByRole("checkbox", { name: "Active" })).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Missing" })).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Archived" })).toBeInTheDocument();
  });

  it("reflects URL state as aria-checked", () => {
    searchParams = new URLSearchParams("lifecycleStatus=Active&lifecycleStatus=Archived");
    render(<LifecycleFilter />);
    expect(screen.getByRole("checkbox", { name: "Active" })).toHaveAttribute("aria-checked", "true");
    expect(screen.getByRole("checkbox", { name: "Missing" })).toHaveAttribute("aria-checked", "false");
    expect(screen.getByRole("checkbox", { name: "Archived" })).toHaveAttribute("aria-checked", "true");
  });

  it("adds the status to the URL when clicked", async () => {
    render(<LifecycleFilter />);
    await userEvent.click(screen.getByRole("checkbox", { name: "Missing" }));
    expect(replaceMock).toHaveBeenCalledTimes(1);
    const url = replaceMock.mock.calls[0]?.[0] as string;
    expect(url).toContain("lifecycleStatus=Missing");
  });

  it("removes the status when clicking a selected chip", async () => {
    searchParams = new URLSearchParams("lifecycleStatus=Active");
    render(<LifecycleFilter />);
    await userEvent.click(screen.getByRole("checkbox", { name: "Active" }));
    const url = replaceMock.mock.calls[0]?.[0] as string;
    expect(url).not.toContain("lifecycleStatus=");
  });

  it("supports adding a second status without losing the first", async () => {
    searchParams = new URLSearchParams("lifecycleStatus=Active");
    render(<LifecycleFilter />);
    await userEvent.click(screen.getByRole("checkbox", { name: "Missing" }));
    const url = replaceMock.mock.calls[0]?.[0] as string;
    expect(url).toContain("lifecycleStatus=Active");
    expect(url).toContain("lifecycleStatus=Missing");
  });

  it("clears the page param when filters change", async () => {
    searchParams = new URLSearchParams("page=4");
    render(<LifecycleFilter />);
    await userEvent.click(screen.getByRole("checkbox", { name: "Active" }));
    const url = replaceMock.mock.calls[0]?.[0] as string;
    expect(url).not.toContain("page=");
  });
});
