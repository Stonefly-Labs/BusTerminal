import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Select, SelectTrigger, SelectValue } from "./select";
import { Label } from "./label";

describe("Select", () => {
  it("is axe-clean (closed state)", async () => {
    const { container } = render(
      <div>
        <Label htmlFor="sel">Environment</Label>
        <Select>
          <SelectTrigger id="sel">
            <SelectValue placeholder="Choose" />
          </SelectTrigger>
        </Select>
      </div>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
