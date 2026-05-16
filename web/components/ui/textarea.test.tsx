import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Textarea } from "./textarea";
import { Label } from "./label";

describe("Textarea", () => {
  it("is axe-clean when paired with a label", async () => {
    const { container } = render(
      <div>
        <Label htmlFor="t-area">Notes</Label>
        <Textarea id="t-area" />
      </div>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
