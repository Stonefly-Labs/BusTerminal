import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";

import { TopologyMiniMapPlaceholder } from "./topology-mini-map-placeholder";

describe("TopologyMiniMapPlaceholder", () => {
  it("is axe-clean", async () => {
    const { container } = render(<TopologyMiniMapPlaceholder />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it("renders inert (no interactive controls)", () => {
    render(<TopologyMiniMapPlaceholder />);
    expect(screen.queryByRole("button")).toBeNull();
    expect(screen.queryByRole("link")).toBeNull();
  });

  it("communicates the deferred-feature copy", () => {
    render(<TopologyMiniMapPlaceholder />);
    expect(screen.getByText(/future spec/i)).toBeInTheDocument();
  });
});
