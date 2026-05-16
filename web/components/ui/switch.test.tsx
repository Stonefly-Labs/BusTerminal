import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Switch } from "./switch";
import { Label } from "./label";

describe("Switch", () => {
  it("is axe-clean when labeled", async () => {
    const { container } = render(
      <div>
        <Switch id="sw" />
        <Label htmlFor="sw">Toggle</Label>
      </div>,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
