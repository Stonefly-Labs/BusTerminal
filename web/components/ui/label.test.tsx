import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Label } from "./label";

describe("Label", () => {
  it("renders provided text", () => {
    render(<Label htmlFor="x">Field name</Label>);
    expect(screen.getByText("Field name")).toBeInTheDocument();
  });

  it("is axe-clean", async () => {
    const { container } = render(<Label htmlFor="x">Field name</Label>);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
