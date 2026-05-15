import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Separator } from "./separator";

describe("Separator", () => {
  it("is axe-clean", async () => {
    const { container } = render(<Separator />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
