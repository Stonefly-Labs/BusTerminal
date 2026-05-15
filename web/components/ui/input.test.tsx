import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Input } from "./input";
import { Label } from "./label";

describe("Input", () => {
  it("has no axe violations when paired with a label", async () => {
    const { container } = render(
      <div>
        <Label htmlFor="t-input">Topic name</Label>
        <Input id="t-input" />
      </div>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it("respects the disabled state", () => {
    const { getByRole } = render(<Input aria-label="x" disabled />);
    expect(getByRole("textbox")).toBeDisabled();
  });
});
