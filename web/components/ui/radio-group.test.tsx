import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { RadioGroup, RadioGroupItem } from "./radio-group";
import { Label } from "./label";

describe("RadioGroup", () => {
  it("is axe-clean", async () => {
    const { container } = render(
      <RadioGroup defaultValue="a">
        <div>
          <RadioGroupItem id="a" value="a" />
          <Label htmlFor="a">A</Label>
        </div>
        <div>
          <RadioGroupItem id="b" value="b" />
          <Label htmlFor="b">B</Label>
        </div>
      </RadioGroup>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
