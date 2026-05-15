import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Checkbox } from "./checkbox";
import { Label } from "./label";

describe("Checkbox", () => {
  it("is axe-clean when paired with a label", async () => {
    const { container } = render(
      <div>
        <Checkbox id="cb" />
        <Label htmlFor="cb">Accept</Label>
      </div>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
