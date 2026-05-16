import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { axe } from "vitest-axe";

import { Toaster } from "./toast";

describe("Toaster", () => {
  it("is axe-clean when mounted", async () => {
    const { container } = render(<Toaster />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
